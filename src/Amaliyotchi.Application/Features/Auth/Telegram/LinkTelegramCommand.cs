using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Telegram;

/// <summary>Talaba Telegram Mini App'da birinchi marta: <c>initData</c> (imzo tekshiriladi) + HEMIS ID + parol
/// (login bilan AYNAN bir xil tekshiruv). Muvaffaqiyatda Telegram <c>user.id</c> talaba hisobiga bog'lanadi VA
/// sessiya beriladi. Shu id bilan allaqachon bog'langan bo'lsa — oddiy kirish (idempotent).</summary>
public sealed record LinkTelegramCommand(string InitData, string HemisId, string Password) : IRequest<AuthResultDto>;

internal sealed class LinkTelegramCommandHandler(
    IApplicationDbContext db,
    ITelegramInitDataValidator validator,
    AuthSessionService sessions,
    IAuditWriter audit,
    IClock clock)
    : IRequestHandler<LinkTelegramCommand, AuthResultDto>
{
    public const string StudentOnly = "Telegram faqat talaba hisobiga bog'lanadi.";
    public const string TelegramTaken = "Bu Telegram akkaunti boshqa hisobga bog'langan.";

    public async Task<AuthResultDto> Handle(LinkTelegramCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (!validator.TryValidate(request.InitData, now, out var tgUser, out var error) || tgUser is null)
        {
            throw await sessions.LoginFailedAsync(
                null, $"Telegram bog'lash: initData rad etildi: {error}",
                AuthSessionService.TelegramSignatureRejected, cancellationToken);
        }

        // Parol/faollik — login bilan bir xil (xato → audit + 403).
        var user = await sessions.VerifyPasswordAsync(request.HemisId, request.Password, now, cancellationToken);

        if (!user.IsStudent)
        {
            throw await sessions.LoginFailedAsync(
                user, $"Telegram bog'lash: xodim hisobi (telegramId={tgUser.Id})", StudentOnly, cancellationToken);
        }

        if (user.TelegramUserId == tgUser.Id)
        {
            if (tgUser.AllowsWriteToPm)
                user.ClearBotBlocked();
            return await sessions.IssueSessionAsync(user, now, auditReason: "Telegram", cancellationToken);
        }

        // Unikal indeks (telegram_user_id) o'chirilgan hisoblarni ham qamraydi — shuning uchun filtrsiz tekshiriladi.
        if (await IsTakenByOtherAsync(tgUser.Id, user.Id, cancellationToken))
        {
            await sessions.RecordFailureAsync(
                user, $"Telegram bog'lash: telegramId={tgUser.Id} boshqa hisobga bog'langan", cancellationToken);
            throw new ConflictException(TelegramTaken);
        }

        try
        {
            // initData'da telefon yo'q — talabaning mavjud telefoni saqlanadi.
            user.LinkTelegram(tgUser.Id, linkedAt: now);
        }
        catch (ConflictException)
        {
            await sessions.RecordFailureAsync(
                user, $"Telegram bog'lash: hisob boshqa Telegram'ga bog'langan (so'ralgan telegramId={tgUser.Id})",
                cancellationToken);
            throw;
        }

        // Yangi bog'langan hisob: oldingi (boshqa Telegram'dagi) blok belgisi ma'nosiz — bot yoza olishi
        // initData'dagi allows_write_to_pm bilan tasdiqlansa tozalanadi.
        if (tgUser.AllowsWriteToPm)
            user.ClearBotBlocked();

        await audit.WriteAsync(
            AuditAction.TelegramLinked, nameof(User), user.Id.ToString(),
            reason: $"telegramId={tgUser.Id}",
            userId: user.Id, userRole: user.Role,
            cancellationToken: cancellationToken);

        try
        {
            return await sessions.IssueSessionAsync(user, now, auditReason: "Telegram", cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Poyga: tekshiruvdan keyin boshqa so'rov shu Telegram id'ni bog'lab ulgurdi — unikal indeks ushladi.
            if (await IsTakenByOtherAsync(tgUser.Id, user.Id, cancellationToken))
                throw new ConflictException(TelegramTaken);
            throw;
        }
    }

    private Task<bool> IsTakenByOtherAsync(long telegramUserId, Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(u => u.TelegramUserId == telegramUserId && u.Id != userId, cancellationToken);
}
