using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.PracticePeriods;

/// <summary><c>GET /api/admin/practice-periods/{id}/groups/{groupId}/students</c> → <see cref="PeriodGroupStudents"/>:
/// guruh talabalarining shu davrdagi natijalari. Davr yo'q yoki guruh shu davrga biriktirilmagan → 404.</summary>
public sealed record GetPeriodGroupStudentsQuery(Guid Id, Guid GroupId) : IRequest<PeriodGroupStudents>;

internal sealed class GetPeriodGroupStudentsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetPeriodGroupStudentsQuery, PeriodGroupStudents>
{
    public async Task<PeriodGroupStudents> Handle(GetPeriodGroupStudentsQuery request, CancellationToken cancellationToken)
    {
        var data = await PeriodStatsCalculator.LoadAsync(db, clock, request.Id, request.GroupId, cancellationToken);
        var period = data.Period.Period;
        var group = data.Groups.FirstOrDefault()
            ?? throw new NotFoundException(PeriodStatsCalculator.GroupNotFoundMessage);

        return new PeriodGroupStudents(
            new PeriodStatsPeriodDto(period.Id, period.Name, period.EffectiveStatus(data.Today), period.StartDate, period.EndDate),
            new PeriodStatsGroupDto(group.Id, group.Code, group.Course, group.FacultyName, group.DirectionName),
            data.ElapsedWorkDays,
            PeriodStatsCalculator.Aggregate(data.Students, data.ElapsedWorkDays),
            data.Students.Select(s => s.Row).ToList());
    }
}
