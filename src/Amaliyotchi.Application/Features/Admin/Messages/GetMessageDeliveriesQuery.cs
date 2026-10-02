using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Messaging;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>GET /api/admin/messages/{id}/deliveries?status&amp;q&amp;page&amp;pageSize</c> (<c>pageSize</c> ≤ 500) — yetkazishlar,
/// FISH bo'yicha. <c>status</c> ixtiyoriy; <c>q</c>: FISH yoki HEMIS ID. Xabar topilmasa → 404.
/// Talaba keyinroq o'chirilgan/faolsizlantirilgan bo'lsa ham qator ko'rinadi (tarix).</summary>
public sealed record GetMessageDeliveriesQuery : PagedQuery, IRequest<Paged<MessageDeliveryRow>>
{
    public override int MaxPageSizeLimit => ExtendedMaxPageSize;

    public Guid Id { get; init; }
    public BroadcastDeliveryStatus? Status { get; init; }
}

public sealed class GetMessageDeliveriesQueryValidator : PagedQueryValidator<GetMessageDeliveriesQuery>
{
    public GetMessageDeliveriesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Yetkazish holati noto'g'ri.").When(x => x.Status is not null);
    }
}

internal sealed class GetMessageDeliveriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMessageDeliveriesQuery, Paged<MessageDeliveryRow>>
{
    public async Task<Paged<MessageDeliveryRow>> Handle(GetMessageDeliveriesQuery request, CancellationToken cancellationToken)
    {
        if (!await db.BroadcastMessages.AnyAsync(m => m.Id == request.Id, cancellationToken))
            throw new NotFoundException(MessageSummaries.NotFound);

        var rows = from d in db.BroadcastDeliveries.AsNoTracking()
                   where d.MessageId == request.Id
                   join u in db.Users.IgnoreQueryFilters() on d.RecipientUserId equals u.Id
                   join p in db.StudentProfiles on u.Id equals p.UserId into profiles
                   from p in profiles.DefaultIfEmpty()
                   select new { Delivery = d, User = u, Profile = p };

        if (request.Status is { } status)
            rows = rows.Where(x => x.Delivery.Status == status);

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            rows = rows.Where(x =>
                EF.Functions.Like(x.User.FullName.ToLower(), pattern, AdminSearch.Escape)
                || (x.Profile != null && EF.Functions.Like(x.Profile.HemisId, pattern, AdminSearch.Escape)));
        }

        return await rows
            .OrderBy(x => x.User.FullName).ThenBy(x => x.Delivery.Id)
            .Select(x => new MessageDeliveryRow(
                x.User.Id,
                x.User.FullName,
                x.Profile != null ? x.Profile.HemisId : null,
                x.Delivery.Status,
                x.Delivery.Error,
                x.Delivery.SentAt))
            .ToPagedAsync(request, cancellationToken);
    }
}
