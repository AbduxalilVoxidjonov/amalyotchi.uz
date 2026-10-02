using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Organization;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>GET /api/admin/messages/recipients?q&amp;facultyId&amp;directionId&amp;groupId&amp;course&amp;page&amp;pageSize</c>
/// (<c>pageSize</c> ≤ 500) — Telegram ulangan faol talabalar, FISH bo'yicha. <c>q</c>: FISH, HEMIS ID yoki Telegram ID.
/// Filtr variantlari — <c>GET /api/admin/students/filters</c> va <c>GET /api/admin/messages/recipients/groups</c>.</summary>
public sealed record GetMessageRecipientsQuery : PagedQuery, IRequest<Paged<MessageRecipientRow>>
{
    public override int MaxPageSizeLimit => ExtendedMaxPageSize;

    public Guid? FacultyId { get; init; }
    public Guid? DirectionId { get; init; }
    public Guid? GroupId { get; init; }
    public int? Course { get; init; }
}

public sealed class GetMessageRecipientsQueryValidator : PagedQueryValidator<GetMessageRecipientsQuery>
{
    public GetMessageRecipientsQueryValidator()
    {
        RuleFor(x => x.Course)
            .InclusiveBetween(StudentGroup.MinCourse, StudentGroup.MaxCourse)
            .WithMessage($"Kurs {StudentGroup.MinCourse} va {StudentGroup.MaxCourse} oralig'ida bo'lishi kerak.")
            .When(x => x.Course is not null);
    }
}

internal sealed class GetMessageRecipientsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMessageRecipientsQuery, Paged<MessageRecipientRow>>
{
    public async Task<Paged<MessageRecipientRow>> Handle(GetMessageRecipientsQuery request, CancellationToken cancellationToken)
    {
        var source = MessageRecipientQueries.Filter(
            MessageRecipientQueries.Source(db),
            request.Q, request.FacultyId, request.DirectionId, request.GroupId, request.Course);

        return await source
            .OrderBy(x => x.User.FullName).ThenBy(x => x.User.Id)
            .Select(x => new MessageRecipientRow(
                x.User.Id,
                x.User.FullName,
                x.Profile != null ? x.Profile.HemisId : null,
                x.User.TelegramUserId!.Value,
                x.User.TelegramLinkedAt,
                x.User.TelegramBotBlockedAt != null,
                x.Faculty != null ? x.Faculty.Name : null,
                x.Direction != null ? x.Direction.Name : null,
                x.Group != null ? x.Group.Name : null,
                x.Group != null ? x.Group.Course : null))
            .ToPagedAsync(request, cancellationToken);
    }
}
