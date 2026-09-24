using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>GET /api/admin/practice-periods/{id}/stats</c> → <see cref="PracticePeriodStats"/>: davrdagi har guruh va
/// jami ko'rsatkichlar (faqat shu davr yozuvlari). Davr yo'q (yoki o'chirilgan) → 404.</summary>
public sealed record GetPracticePeriodStatsQuery(Guid Id) : IRequest<PracticePeriodStats>;

internal sealed class GetPracticePeriodStatsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetPracticePeriodStatsQuery, PracticePeriodStats>
{
    public async Task<PracticePeriodStats> Handle(GetPracticePeriodStatsQuery request, CancellationToken cancellationToken)
    {
        var data = await PeriodStatsCalculator.LoadAsync(db, clock, request.Id, groupId: null, cancellationToken);
        var byGroup = data.Students.ToLookup(s => s.GroupId);

        var groups = data.Groups
            .Select(g => PeriodGroupStats.From(
                g.Id, g.Code, g.Course, g.DirectionName,
                PeriodStatsCalculator.Aggregate(byGroup[g.Id].ToList(), data.ElapsedWorkDays)))
            .ToList();

        return new PracticePeriodStats(
            data.Period.Period.Id,
            data.ElapsedWorkDays,
            data.Period.Period.RequiredDays,
            PeriodStatsCalculator.Aggregate(data.Students, data.ElapsedWorkDays),
            groups);
    }
}
