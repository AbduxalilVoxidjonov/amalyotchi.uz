using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>POST /api/admin/messages/{id}/retry</c> → 200 <see cref="MessageSummary"/>: <c>failed</c> yetkazishlar
/// <c>pending</c> ga qaytadi (urinishlar nollanadi). <c>blocked</c> larga tegilmaydi. <c>failed</c> yo'q bo'lsa ham 200
/// (o'zgarishsiz). Xabar topilmasa → 404.</summary>
public sealed record RetryMessageCommand(Guid Id) : IRequest<MessageSummary>;

internal sealed class RetryMessageCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<RetryMessageCommand, MessageSummary>
{
    public async Task<MessageSummary> Handle(RetryMessageCommand request, CancellationToken cancellationToken)
    {
        if (!await db.BroadcastMessages.AnyAsync(m => m.Id == request.Id, cancellationToken))
            throw new NotFoundException(MessageSummaries.NotFound);

        var now = clock.UtcNow;
        var failed = await db.BroadcastDeliveries
            .Where(d => d.MessageId == request.Id && d.Status == BroadcastDeliveryStatus.Failed)
            .ToListAsync(cancellationToken);

        var requeued = failed.Count(d => d.Requeue(now));
        if (requeued > 0)
        {
            await audit.WriteAsync(
                AuditAction.BroadcastMessageRetried, nameof(BroadcastMessage), request.Id.ToString(),
                reason: $"Qayta navbatga: {requeued} ta yetkazish",
                cancellationToken: cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await MessageSummaries.LoadOneAsync(db, request.Id, cancellationToken);
    }
}
