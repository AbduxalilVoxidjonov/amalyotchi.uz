using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Organization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Messages;

/// <summary><c>GET /api/admin/messages/recipients/groups?facultyId&amp;directionId&amp;course</c> — guruh tanlagichi uchun:
/// faqat Telegram ulangan faol talabasi bor guruhlar, nom bo'yicha. Filtrlar ixtiyoriy (AND).</summary>
public sealed record GetMessageRecipientGroupsQuery : IRequest<IReadOnlyList<MessageRecipientGroupOption>>
{
    public Guid? FacultyId { get; init; }
    public Guid? DirectionId { get; init; }
    public int? Course { get; init; }
}

public sealed class GetMessageRecipientGroupsQueryValidator : AbstractValidator<GetMessageRecipientGroupsQuery>
{
    public GetMessageRecipientGroupsQueryValidator()
    {
        RuleFor(x => x.Course)
            .InclusiveBetween(StudentGroup.MinCourse, StudentGroup.MaxCourse)
            .WithMessage($"Kurs {StudentGroup.MinCourse} va {StudentGroup.MaxCourse} oralig'ida bo'lishi kerak.")
            .When(x => x.Course is not null);
    }
}

internal sealed class GetMessageRecipientGroupsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMessageRecipientGroupsQuery, IReadOnlyList<MessageRecipientGroupOption>>
{
    public async Task<IReadOnlyList<MessageRecipientGroupOption>> Handle(
        GetMessageRecipientGroupsQuery request, CancellationToken cancellationToken)
    {
        var source = MessageRecipientQueries.Filter(
            MessageRecipientQueries.Source(db), q: null, request.FacultyId, request.DirectionId, groupId: null, request.Course);

        var groups = await source
            .Where(x => x.Group != null)
            .Select(x => new { x.Group!.Id, x.Group.Name })
            .Distinct()
            .ToListAsync(cancellationToken);

        return groups
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Id)
            .Select(g => new MessageRecipientGroupOption(g.Id, g.Name))
            .ToList();
    }
}
