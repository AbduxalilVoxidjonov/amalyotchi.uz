using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary>Kontrakt <c>Faculty</c> + qo'shimcha <c>code</c>, <c>tutors</c>. <paramref name="AttendancePct"/> — bugungi
/// (kutilgan talabalardan kelgan/kech kelganlar); bugun hech kim kutilmasa 0 va holat <c>active</c>.</summary>
public sealed record FacultyRow(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
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
    /// <summary>Ro'yxatning sukut (filtrsiz) to'plami — barcha fakultetlar (faol/nofaol). Sidebar hisoblagichi
    /// (<c>GET /api/admin/nav</c>) ham shu manbadan sanaydi, ro'yxat <c>total</c>i bilan mos bo'lishi uchun.</summary>
    internal static IQueryable<Faculty> Source(IApplicationDbContext db) => db.Faculties.AsNoTracking();

    public async Task<Paged<FacultyRow>> Handle(GetFacultiesQuery request, CancellationToken cancellationToken)
    {
        var faculties = Source(db);
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
                f.IsActive,
                Directions = f.Departments.SelectMany(dep => dep.Directions).Count(),
                Groups = f.Departments.SelectMany(dep => dep.Directions).SelectMany(d => d.Groups).Count()
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<FacultyRow>.Empty(request);

        var ids = page.Items.Select(f => f.Id).ToList();

        // Fakultet → kafedra → yo'nalish → guruh → talaba zanjiri (StudentGroup'da Direction navigatsiyasi yo'q — join).
        var studentsByFaculty = await (from p in db.StudentProfiles.AsNoTracking()
                                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                       join d in db.Directions on g.DirectionId equals d.Id
                                       join dept in db.Departments on d.DepartmentId equals dept.Id
                                       where ids.Contains(dept.FacultyId)
                                       group p by dept.FacultyId into grp
                                       select new { FacultyId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        var tutorsByFaculty = await (from a in db.TutorAssignments.AsNoTracking()
                                     join g in db.StudentGroups on a.StudentGroupId equals g.Id
                                     join d in db.Directions on g.DirectionId equals d.Id
                                     join dept in db.Departments on d.DepartmentId equals dept.Id
                                     where a.IsActive && ids.Contains(dept.FacultyId)
                                     group a by dept.FacultyId into grp
                                     select new { FacultyId = grp.Key, Count = grp.Select(a => a.TutorUserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.FacultyId, x => x.Count, cancellationToken);

        // Bugungi davomat — dashboard bilan bitta hisob (DailyAttendanceTally): faqat bugun davom etayotgan davr guruhlari,
        // foiz = keldi / (kutilgan − sababli).
        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var todayByFaculty = await DailyAttendanceTally.LoadByFacultyAsync(
            db, calendar, calendar.Today, calendar.GroupsExpectedToday, ids, withDiary: false, cancellationToken);

        var rows = page.Items.Select(f =>
        {
            var today = todayByFaculty.GetValueOrDefault(f.Id) ?? DayTally.Empty;
            var pct = today.Pct;
            var status = today.Expected > 0 && pct < AdminThresholds.AttentionAttendancePct
                ? FacultyStatus.Attention
                : FacultyStatus.Active;

            return new FacultyRow(
                f.Id, f.Name, f.Code, f.IsActive, f.Directions, f.Groups,
                studentsByFaculty.GetValueOrDefault(f.Id),
                tutorsByFaculty.GetValueOrDefault(f.Id),
                pct, status);
        }).ToList();

        return new Paged<FacultyRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
