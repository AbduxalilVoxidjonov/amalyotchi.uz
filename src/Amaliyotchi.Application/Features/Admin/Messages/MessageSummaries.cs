using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><see cref="MessageSummary"/> yig'ish: xabarlar + yetkazish sonlari (bitta GROUP BY so'rov,
/// <c>(message_id, status)</c> indeksi) + yuborgan admin ismi.</summary>
internal static class MessageSummaries
{
    /// <summary>Yuborgan foydalanuvchi topilmasa (masalan seed/tizim).</summary>
    public const string UnknownAuthor = "Noma'lum";

    public static async Task<IReadOnlyList<MessageSummary>> LoadAsync(
        IApplicationDbContext db, IReadOnlyList<BroadcastMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
            return [];

        var ids = messages.Select(m => m.Id).ToList();

        var counts = await db.BroadcastDeliveries
            .AsNoTracking()
            .Where(d => ids.Contains(d.MessageId))
            .GroupBy(d => new { d.MessageId, d.Status })
            .Select(g => new { g.Key.MessageId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var authorIds = messages.Select(m => m.CreatedBy).OfType<Guid>().Distinct().ToList();
        var authors = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return messages.Select(m =>
        {
            int Count(BroadcastDeliveryStatus status) =>
                counts.Where(c => c.MessageId == m.Id && c.Status == status).Sum(c => c.Count);

            var sent = Count(BroadcastDeliveryStatus.Sent);
            var failed = Count(BroadcastDeliveryStatus.Failed);
            var blocked = Count(BroadcastDeliveryStatus.Blocked);
            var pending = Count(BroadcastDeliveryStatus.Pending);
            var total = sent + failed + blocked + pending;

            return new MessageSummary(
                m.Id,
                m.Text,
                m.CreatedAt,
                m.CreatedBy is { } by && authors.TryGetValue(by, out var name) ? name : UnknownAuthor,
                m.AudienceLabel,
                m.AttachAppButton,
                BroadcastStatusRule.For(total, pending),
                total, sent, failed, blocked, pending);
        }).ToList();
    }

    public static async Task<MessageSummary> LoadOneAsync(IApplicationDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var message = await db.BroadcastMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException(NotFound);

        return (await LoadAsync(db, [message], cancellationToken))[0];
    }

    public const string NotFound = "Xabar topilmadi.";
}
