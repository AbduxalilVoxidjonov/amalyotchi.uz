using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Kontrakt <c>Student</c> + <c>hemisId</c>, <c>groupId</c>, <c>course</c>, <c>telegramLinked</c>, <c>suspiciousDays</c>.
/// <paramref name="Id"/> — talabaning <c>User.Id</c> (tyutor endpoint'laridagi <c>studentId</c> bilan bir xil).
/// <paramref name="Company"/> — tasdiqlangan arizadagi korxona (eng so'nggisi), yo'q bo'lsa <c>null</c>.</summary>
public sealed record StudentRow(
    Guid Id,
    string FullName,
    string HemisId,
    Guid GroupId,
    string Group,
    int Course,
    string Faculty,
    string? Company,
    int AttendancePct,
    int SuspiciousDays,
    bool TelegramLinked,
    AdminStudentStatus Status);

/// <summary><c>GET /api/admin/students?q&amp;page&amp;pageSize</c> — <c>q</c>: FISH, HEMIS ID, telefon, guruh.</summary>
public sealed record GetAdminStudentsQuery : PagedQuery, IRequest<Paged<StudentRow>>;

internal sealed class GetAdminStudentsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetAdminStudentsQuery, Paged<StudentRow>>
{
    public async Task<Paged<StudentRow>> Handle(GetAdminStudentsQuery request, CancellationToken cancellationToken)
    {
        var students = from p in db.StudentProfiles.AsNoTracking()
                       join g in db.StudentGroups on p.StudentGroupId equals g.Id
                       join d in db.Directions on g.DirectionId equals d.Id
                       join f in db.Faculties on d.FacultyId equals f.Id
                       select new { Profile = p, User = p.User, Group = g, Faculty = f };

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            students = students.Where(x =>
                EF.Functions.Like(x.User.FullName.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(x.Profile.HemisId, pattern, AdminSearch.Escape)
                || (x.User.PhoneNumber != null && EF.Functions.Like(x.User.PhoneNumber, pattern, AdminSearch.Escape))
                || EF.Functions.Like(x.Group.Name.ToLower(), pattern, AdminSearch.Escape));
        }

        var page = await students
            .OrderBy(x => x.User.FullName).ThenBy(x => x.Profile.HemisId)
            .Select(x => new
            {
                x.User.Id,
                x.User.FullName,
                x.Profile.HemisId,
                GroupId = x.Group.Id,
                Group = x.Group.Name,
                x.Group.Course,
                Faculty = x.Faculty.Name,
                TelegramLinked = x.User.TelegramUserId != null,
                Company = db.PracticeApplications
                    .Where(a => a.StudentUserId == x.User.Id && a.Status == ApplicationStatus.Approved)
                    .OrderByDescending(a => a.DecidedAt)
                    .Select(a => a.Company.Name)
                    .FirstOrDefault()
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<StudentRow>.Empty(request);

        var ids = page.Items.Select(s => s.Id).ToList();
        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var today = calendar.Today;

        var attendance = await db.DailyAttendances
            .AsNoTracking()
            .Where(a => ids.Contains(a.StudentUserId))
            .GroupBy(a => a.StudentUserId)
            .Select(g => new
            {
                StudentId = g.Key,
                Attended = g.Count(a => a.Date < today && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)),
                Excused = g.Count(a => a.Date < today && a.Status == AttendanceStatus.Excused),
                Suspicious = g.Count(a => a.IsSuspicious)
            })
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);

        var rows = page.Items.Select(s =>
        {
            var stats = attendance.GetValueOrDefault(s.Id);
            var elapsed = calendar.ElapsedWorkDays(s.GroupId);
            var pct = PracticeCalendar.AttendancePct(stats?.Attended ?? 0, elapsed, stats?.Excused ?? 0);
            var suspicious = stats?.Suspicious ?? 0;

            var status = !s.TelegramLinked
                ? AdminStudentStatus.Unlinked
                : suspicious >= AdminThresholds.FlaggedSuspiciousDays || (elapsed > 0 && pct < AdminThresholds.FlaggedAttendancePct)
                    ? AdminStudentStatus.Flagged
                    : AdminStudentStatus.Active;

            return new StudentRow(
                s.Id, s.FullName, s.HemisId, s.GroupId, s.Group, s.Course, s.Faculty, s.Company,
                pct, suspicious, s.TelegramLinked, status);
        }).ToList();

        return new Paged<StudentRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
