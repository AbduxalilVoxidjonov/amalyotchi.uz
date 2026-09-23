using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>GET /api/admin/practice-periods?status=planned|active|closed</c> → <see cref="PracticePeriodListItem"/>[]
/// (sahifalanmagan, <c>startDate</c> kamayish tartibida). <see cref="Status"/> — hisoblangan holat bo'yicha filtr
/// (<see cref="PracticePeriod.ResolveStatus"/>): <c>planned</c> = ochiq va boshlanishi bugundan keyin; <c>active</c> = ochiq va
/// boshlangan; <c>closed</c> = yopilgan.</summary>
public sealed record GetPracticePeriodsQuery : IRequest<IReadOnlyList<PracticePeriodListItem>>
{
    public PracticePeriodStatus? Status { get; init; }
}

internal sealed class GetPracticePeriodsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetPracticePeriodsQuery, IReadOnlyList<PracticePeriodListItem>>
{
    public async Task<IReadOnlyList<PracticePeriodListItem>> Handle(
        GetPracticePeriodsQuery request, CancellationToken cancellationToken)
    {
        var today = clock.LocalToday();
        var periods = db.PracticePeriods.AsNoTracking();

        periods = request.Status switch
        {
            PracticePeriodStatus.Closed => periods.Where(p => p.Status == PracticePeriodStatus.Closed),
            PracticePeriodStatus.Planned => periods.Where(p => p.Status != PracticePeriodStatus.Closed && p.StartDate > today),
            PracticePeriodStatus.Active => periods.Where(p => p.Status != PracticePeriodStatus.Closed && p.StartDate <= today),
            _ => periods
        };

        var rows = await periods
            .OrderByDescending(p => p.StartDate).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.StartDate,
                p.EndDate,
                p.Status,
                p.CreatedAt,
                GroupsCount = p.Groups.Count,
                StudentsCount = db.StudentProfiles.Count(s => s.Status == StudentStatus.Active
                    && p.Groups.Any(g => g.StudentGroupId == s.StudentGroupId))
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(p => new PracticePeriodListItem(
                p.Id, p.Name, p.StartDate, p.EndDate,
                PracticePeriod.ResolveStatus(p.Status, p.StartDate, today),
                p.GroupsCount, p.StudentsCount, p.CreatedAt))
            .ToList();
    }
}
