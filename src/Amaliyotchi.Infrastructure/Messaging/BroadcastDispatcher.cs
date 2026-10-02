using System.Diagnostics;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Messaging;
using Amaliyotchi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Messaging;

/// <summary>Bitta "tick" natijasi. <see cref="PauseFor"/> — Telegram 429 bergan: fon tsikli shuncha kutadi.</summary>
public sealed record BroadcastDispatchResult(int Claimed, int Sent, int Blocked, int Failed, int Deferred, TimeSpan? PauseFor)
{
    public static readonly BroadcastDispatchResult Idle = new(0, 0, 0, 0, 0, null);
}

/// <summary>"Xabarlar" yetkazish dispetcheri — DB-navbat (<c>broadcast_deliveries</c>). <see cref="RunOnceAsync"/> bitta partiyani
/// qayta ishlaydi (testlar to'g'ridan-to'g'ri chaqiradi); tsikl — <see cref="BroadcastDispatcherService"/>.
/// <para><b>Navbatdan olish</b>: bitta qisqa avtokommit so'rov — <c>UPDATE … SET next_attempt_at = now + ijara WHERE id IN
/// (SELECT … status = pending AND next_attempt_at &lt;= now ORDER BY next_attempt_at LIMIT n FOR UPDATE SKIP LOCKED) RETURNING id</c>.
/// Hozir dispetcher bitta (faqat API host'ida, bot polling kabi), lekin ijara tufayli ikkinchi instansiya bir qatorni
/// ikki marta olmaydi va yuborish davomida uzun tranzaksiya ochiq turmaydi. Jarayon yiqilsa, ijara tugagach
/// (<see cref="LeaseDuration"/>) qator yana olinadi (at-least-once: Telegram qabul qilib, holat yozilmay qolgan kamdan-kam
/// holatda takroriy yuborish mumkin).</para>
/// <para><b>Natijalar</b>: muvaffaqiyat → sent (+ talaba bloki tozalanadi); 403 / 400 chat not found → blocked
/// (+ <c>User.TelegramBotBlockedAt</c>); 429 → <c>retry_after</c> bo'yicha kechiktirish (urinish hisoblanmaydi), partiyaning
/// qolgani ham shu vaqtga suriladi va tsikl to'xtab turadi (global pauza); tarmoq/5xx → urinish++, eksponensial backoff,
/// 5 urinishdan keyin failed; boshqa xato yoki token sozlanmagan → failed. Global tezlik ≤ <see cref="MessagingOptions.MessagesPerSecond"/>.</para>
/// <para>To'xtatishda (shutdown) olingan, lekin yuborilmagan qatorlarning ijarasi bo'shatiladi — restart'dan keyin darhol davom etadi.</para>
/// </summary>
public sealed class BroadcastDispatcher(
    IServiceScopeFactory scopeFactory,
    ITelegramMessenger messenger,
    IClock clock,
    IOptions<MessagingOptions> options,
    ILogger<BroadcastDispatcher> logger)
{
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _tick = new(1, 1);
    private readonly SemaphoreSlim _rate = new(1, 1);
    private long _nextSendTimestamp;

    public async Task<BroadcastDispatchResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await _tick.WaitAsync(cancellationToken);
        try
        {
            return await RunCoreAsync(cancellationToken);
        }
        finally
        {
            _tick.Release();
        }
    }

    private async Task<BroadcastDispatchResult> RunCoreAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = clock.UtcNow;
        var ids = await ClaimAsync(db, now, Math.Clamp(settings.BatchSize, 1, 500), cancellationToken);
        if (ids.Count == 0)
            return BroadcastDispatchResult.Idle;

        var deliveries = await db.BroadcastDeliveries
            .Where(d => ids.Contains(d.Id))
            .OrderBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var messageIds = deliveries.Select(d => d.MessageId).Distinct().ToList();
        var messages = await db.BroadcastMessages
            .AsNoTracking()
            .Where(m => messageIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Text, m.AttachAppButton })
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        int sent = 0, blocked = 0, failed = 0, deferred = 0;
        TimeSpan? pause = null;
        var index = 0;

        try
        {
            for (; index < deliveries.Count; index++)
            {
                var delivery = deliveries[index];
                if (!messages.TryGetValue(delivery.MessageId, out var message))
                {
                    delivery.MarkFailed("Xabar topilmadi");
                    failed++;
                    await db.SaveChangesAsync(CancellationToken.None);
                    continue;
                }

                await WaitForRateAsync(settings.MessagesPerSecond, cancellationToken);
                var result = await messenger.SendTextAsync(delivery.ChatId, message.Text, message.AttachAppButton, cancellationToken);
                var at = clock.UtcNow;

                switch (result.Outcome)
                {
                    case TelegramSendOutcome.Sent:
                        delivery.MarkSent(at, result.MessageId);
                        sent++;
                        await SetUserBlockedAsync(db, delivery.RecipientUserId, blockedAt: null, cancellationToken);
                        break;

                    case TelegramSendOutcome.Blocked:
                        delivery.MarkBlocked(result.Error ?? TelegramMessenger.BlockedError);
                        blocked++;
                        await SetUserBlockedAsync(db, delivery.RecipientUserId, blockedAt: at, cancellationToken);
                        break;

                    case TelegramSendOutcome.RateLimited:
                        var wait = result.RetryAfter is { } retry && retry > TimeSpan.Zero ? retry : TimeSpan.FromSeconds(1);
                        pause = wait;
                        // Shu va partiyaning qolgan qatorlari — retry_after dan keyin (urinish hisoblanmaydi).
                        foreach (var rest in deliveries.Skip(index))
                        {
                            rest.Defer(at + wait);
                            deferred++;
                        }

                        logger.LogWarning(
                            "Xabarlar: Telegram 429 — {Seconds} s pauza, {Count} ta yetkazish kechiktirildi.",
                            (int)Math.Ceiling(wait.TotalSeconds), deferred);
                        index = deliveries.Count;
                        break;

                    case TelegramSendOutcome.TransientError:
                        delivery.RegisterTransientFailure(result.Error ?? "Vaqtinchalik xato", at);
                        if (delivery.Status == BroadcastDeliveryStatus.Failed)
                            failed++;
                        logger.LogWarning(
                            "Xabarlar: yetkazish {DeliveryId} vaqtinchalik xato ({Error}), urinish {Attempts}.",
                            delivery.Id, result.Error, delivery.Attempts);
                        break;

                    default:
                        delivery.MarkFailed(result.Error ?? "Yuborib bo'lmadi");
                        failed++;
                        logger.LogWarning("Xabarlar: yetkazish {DeliveryId} muvaffaqiyatsiz ({Error}).", delivery.Id, result.Error);
                        break;
                }

                // Har yetkazishdan keyin saqlanadi: jarayon to'xtasa ham yuborilganlar qayta yuborilmaydi.
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseAsync(db, deliveries.Skip(index), CancellationToken.None);
            throw;
        }
        catch
        {
            // Kutilmagan xato (masalan DB): olingan, lekin qayta ishlanmagan qatorlar ijarasi tugashini kutib qolmasin.
            await ReleaseAsync(db, deliveries.Skip(index + 1), CancellationToken.None);
            throw;
        }

        return new BroadcastDispatchResult(ids.Count, sent, blocked, failed, deferred, pause);
    }

    private static async Task<List<Guid>> ClaimAsync(AppDbContext db, DateTimeOffset now, int batchSize, CancellationToken ct)
    {
        var lease = now + LeaseDuration;
        var pending = (int)BroadcastDeliveryStatus.Pending;
        return await db.Database.SqlQuery<Guid>($"""
            UPDATE broadcast_deliveries SET next_attempt_at = {lease}
            WHERE id IN (
                SELECT id FROM broadcast_deliveries
                WHERE status = {pending} AND next_attempt_at <= {now}
                ORDER BY next_attempt_at, id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED)
            RETURNING id AS "Value"
            """).ToListAsync(ct);
    }

    /// <summary>Ijarani bo'shatish: qatorlar darhol yana olinadigan bo'ladi (holat o'zgarmaydi).</summary>
    private async Task ReleaseAsync(AppDbContext db, IEnumerable<BroadcastDelivery> rest, CancellationToken ct)
    {
        try
        {
            var now = clock.UtcNow;
            var any = false;
            foreach (var delivery in rest)
            {
                if (delivery.Status != BroadcastDeliveryStatus.Pending)
                    continue;
                delivery.Defer(now);
                any = true;
            }

            if (any)
                await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Xabarlar: ijarani bo'shatib bo'lmadi ({Type}) — {Lease} dan keyin qayta olinadi.",
                ex.GetType().Name, LeaseDuration);
        }
    }

    /// <summary>Talabaning "bot bloklangan" belgisi. To'plamli <c>UPDATE</c> (faqat qiymat haqiqatan o'zgarganda): foydalanuvchi
    /// qatorini yuklamaydi — har muvaffaqiyatli yuborishda <c>users</c> ga yozilmaydi, audit jurnali "Updated" shovqini bilan
    /// to'lmaydi. Ma'nosi domen metodlari (<c>User.MarkBotBlocked</c>/<c>ClearBotBlocked</c>) bilan bir xil: birinchi blok
    /// vaqti saqlanadi.</summary>
    private static Task<int> SetUserBlockedAsync(AppDbContext db, Guid userId, DateTimeOffset? blockedAt, CancellationToken ct)
        => blockedAt is { } at
            ? db.Users.Where(u => u.Id == userId && u.TelegramBotBlockedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.TelegramBotBlockedAt, at), ct)
            : db.Users.Where(u => u.Id == userId && u.TelegramBotBlockedAt != null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.TelegramBotBlockedAt, (DateTimeOffset?)null), ct);

    /// <summary>Global tezlik: ketma-ket yuborishlar orasida kamida 1/<paramref name="perSecond"/> s.</summary>
    private async Task WaitForRateAsync(int perSecond, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(1.0 / Math.Clamp(perSecond, 1, 1000));
        await _rate.WaitAsync(ct);
        try
        {
            var nowTs = Stopwatch.GetTimestamp();
            if (_nextSendTimestamp > nowTs)
            {
                var wait = Stopwatch.GetElapsedTime(nowTs, _nextSendTimestamp);
                await Task.Delay(wait, ct);
                nowTs = Stopwatch.GetTimestamp();
            }

            _nextSendTimestamp = nowTs + (long)(interval.TotalSeconds * Stopwatch.Frequency);
        }
        finally
        {
            _rate.Release();
        }
    }
}
