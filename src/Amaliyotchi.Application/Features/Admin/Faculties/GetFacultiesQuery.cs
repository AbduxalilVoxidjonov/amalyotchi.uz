using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary>Kontrakt <c>Faculty</c> + qo'shimcha <c>code</c>, <c>tutors</c>. <paramref name="AttendancePct"/> — bugungi
/// (kutilgan talabalardan kelgan/kech kelganlar); bugun hech kim kutilmasa 0 va holat <c>active</c>.</summary>
public sealed record FacultyRow(
    Guid Id,
    string Name,
    string Code,
    int Directions,
    int Groups,
    int Students,
    int Tutors,
    int AttendancePct,
    FacultyStatus Status);

/// <summary><c>GET /api/admin/faculties?q&amp;page&amp;pageSize</c> — <c>q</c>: nom yoki kod.</summary>
public sealed record GetFacultiesQuery : PagedQuery, IRequest<Paged<FacultyRow>>;

internal sealed class GetFacultiesQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetFacultiesQuery, Paged<FacultyRow>>
{
    public async Task<Paged<FacultyRow>> Handle(GetFacultiesQuery request, CancellationToken cancellationToken)
    {
        var faculties = db.Faculties.AsNoTracking();
        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            faculties = faculties.Where(f =>
                EF.Functions.Like(f.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(f.Code.ToLower(), pattern, AdminSearch.Escape));
        }

        var page = await faculties
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Code,
                Directions = f.Directions.Count,
                Groups = f.Directions.SelectMany(d => d.Groups).Count()
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<FacultyRow>.Empty(request);

        var ids = page.Items.Select(f => f.Id).ToList();

        // Fakultet → yo'nalish → guruh → talaba zanjiri (StudentGroup'da Direction navigatsiyasi yo'q — join).
        var studentsByFaculty = await (from p in db.StudentProfiles.AsNoTracking()
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       where ids.Contains(d.FacultyId)
                                       group p by d.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        var tutorsByFaculty = await (from a in db.TutorAssignments.AsNoTracking()
                                     join g in db.StudentGroups on a.StudentGroupId equals g.Id
                                     join d in db.Directions on g.DirectionId equals d.Id
                                     where a.IsActive && ids.Contains(d.FacultyId)
                                     group a by d.FacultyId into grp
                                     select new { FacultyId = grp.Key, Count = grp.Select(a => a.TutorUserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var expectedGroups = calendar.GroupsExpectedToday.ToList();
        var today = calendar.Today;

        var expectedByFaculty = new Dictionary<Guid, int>();
        var attendedByFaculty = new Dictionary<Guid, int>();
        if (expectedGroups.Count > 0)
        {
            expectedByFaculty = await (from p in db.StudentProfiles.AsNoTracking()
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       where expectedGroups.Contains(p.StudentGroupId) && ids.Contains(d.FacultyId)
                                       group p by d.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
                .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

            attendedByFaculty = await (from a in db.DailyAttendances.AsNoTracking()
                                       join p in db.StudentProfiles on a.StudentUserId equals p.UserId
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       where a.Date == today
                                             && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                                             && expectedGroups.Contains(p.StudentGroupId) && ids.Contains(d.FacultyId)
                                       group a by d.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
                .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);
        }

        var rows = page.Items.Select(f =>
        {
            var expected = expectedByFaculty.GetValueOrDefault(f.Id);
            var pct = PracticeCalendar.AttendancePct(attendedByFaculty.GetValueOrDefault(f.Id), expected, 0);
            var status = expected > 0 && pct < AdminThresholds.AttentionAttendancePct
                ? FacultyStatus.Attention
                : FacultyStatus.Active;

            return new FacultyRow(
                f.Id, f.Name, f.Code, f.Directions, f.Groups,
                studentsByFaculty.GetValueOrDefault(f.Id),
                tutorsByFaculty.GetValueOrDefault(f.Id),
                pct, status);
        }).ToList();

        return new Paged<FacultyRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
