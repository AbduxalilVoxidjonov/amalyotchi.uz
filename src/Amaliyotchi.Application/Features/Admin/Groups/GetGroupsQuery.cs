using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary>Guruhga biriktirilgan faol amaliyot davri (yo'q bo'lsa <c>null</c>).</summary>
public sealed record GroupPeriodDto(Guid Id, string Name, PracticePeriodStatus Status, DateOnly StartDate, DateOnly EndDate);

/// <summary>Kontrakt <c>Group</c> (<c>code</c> = guruh nomi "412-22") + fakultet, tyutor id, davr.
/// <paramref name="AttendancePct"/> — faol davr boshidan kechagacha (kelgan kunlar / (o'tgan ish kunlari − sababli)).</summary>
public sealed record GroupRow(
    Guid Id,
    string Code,
    int Course,
    string Direction,
    string Faculty,
    string FacultyCode,
    Guid? TutorId,
    string? Tutor,
    int Students,
    int AttendancePct,
    GroupPeriodDto? Period,
    bool IsActive);

/// <summary><c>GET /api/admin/groups?q&amp;page&amp;pageSize</c> — <c>q</c>: guruh, yo'nalish, fakultet yoki tyutor ismi.
/// Global ro'yxat — yo'nalish bo'yicha filtrlanmaydi.</summary>
public sealed record GetGroupsQuery : PagedQuery, IRequest<Paged<GroupRow>>;

internal sealed class GetGroupsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetGroupsQuery, Paged<GroupRow>>
{
    public Task<Paged<GroupRow>> Handle(GetGroupsQuery request, CancellationToken cancellationToken)
        => GroupRowQueries.LoadPagedAsync(db, clock, directionId: null, request, cancellationToken);
}

/// <summary><c>GET /api/admin/groups?q&amp;page&amp;pageSize</c> va <c>GET /api/admin/directions/{id}/groups</c>
/// bir xil qator shaklini (<see cref="GroupRow"/>) qaytaradi — hisoblash mantig'i shu yerda, ikkalasida takrorlanmasin.</summary>
internal static class GroupRowQueries
{
    public static async Task<Paged<GroupRow>> LoadPagedAsync(
        IApplicationDbContext db, IClock clock, Guid? directionId, PagedQuery request, CancellationToken cancellationToken)
    {
        var groups = from g in db.StudentGroups.AsNoTracking()
                     join d in db.Directions on g.DirectionId equals d.Id
                     join dept in db.Departments on d.DepartmentId equals dept.Id
                     join f in db.Faculties on dept.FacultyId equals f.Id
                     select new { Group = g, Direction = d, Faculty = f };

        if (directionId is { } id)
            groups = groups.Where(x => x.Direction.Id == id);

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            groups = groups.Where(x =>
                EF.Functions.Like(x.Group.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(x.Direction.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(x.Faculty.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(x.Faculty.Code.ToLower(), pattern, AdminSearch.Escape)
                || db.TutorAssignments.Any(a => a.StudentGroupId == x.Group.Id && a.IsActive
                    && db.Users.Any(u => u.Id == a.TutorUserId && EF.Functions.Like(u.FullName.ToLower(), pattern, AdminSearch.Escape))));
        }

        var page = await groups
            .OrderBy(x => x.Faculty.Name).ThenBy(x => x.Group.Name)
            .Select(x => new
            {
                x.Group.Id,
                x.Group.Name,
                x.Group.Course,
                x.Group.IsActive,
                Direction = x.Direction.Name,
                Faculty = x.Faculty.Name,
                FacultyCode = x.Faculty.Code,
                TutorId = db.TutorAssignments
                    .Where(a => a.StudentGroupId == x.Group.Id && a.IsActive)
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => (Guid?)a.TutorUserId)
                    .FirstOrDefault(),
                Students = db.StudentProfiles.Count(p => p.StudentGroupId == x.Group.Id)
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<GroupRow>.Empty(request);

        var groupIds = page.Items.Select(g => g.Id).ToList();
        var tutorIds = page.Items.Where(g => g.TutorId is not null).Select(g => g.TutorId!.Value).Distinct().ToList();

        var tutorNames = tutorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => tutorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var today = calendar.Today;

        var attendance = await (from a in db.DailyAttendances.AsNoTracking()
                                join p in db.StudentProfiles on a.StudentUserId equals p.UserId
                                where groupIds.Contains(p.StudentGroupId) && a.Date < today
                                group a by p.StudentGroupId into grp
                                select new
                                {
                                    GroupId = grp.Key,
                                    Attended = grp.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late),
                                    Excused = grp.Count(a => a.Status == AttendanceStatus.Excused)
                                })
            .ToDictionaryAsync(x => x.GroupId, cancellationToken);

        // Maxraj — faqat amaliyotga chiqqan (arizasi tasdiqlangan) talabalar; arizasiz talaba foizni tushirmaydi.
        var practicingByGroup = await (from app in db.PracticeApplications.AsNoTracking()
                                       join p in db.StudentProfiles on app.StudentUserId equals p.UserId
                                       where groupIds.Contains(p.StudentGroupId) && app.Status == ApplicationStatus.Approved
                                       group app by p.StudentGroupId into grp
                                       select new { GroupId = grp.Key, Count = grp.Select(a => a.StudentUserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        var rows = page.Items.Select(g =>
        {
            var practice = calendar.For(g.Id);
            var stats = attendance.GetValueOrDefault(g.Id);
            var pct = practice is null
                ? 0
                : PracticeCalendar.AttendancePct(
                    stats?.Attended ?? 0, practice.ElapsedWorkDays * practicingByGroup.GetValueOrDefault(g.Id), stats?.Excused ?? 0);

            return new GroupRow(
                g.Id, g.Name, g.Course, g.Direction, g.Faculty, g.FacultyCode,
                g.TutorId,
                g.TutorId is { } tutorId ? tutorNames.GetValueOrDefault(tutorId) : null,
                g.Students, pct,
                practice is null ? null : new GroupPeriodDto(practice.PeriodId, practice.PeriodName, practice.Status, practice.StartDate, practice.EndDate),
                g.IsActive);
        }).ToList();

        return new Paged<GroupRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
