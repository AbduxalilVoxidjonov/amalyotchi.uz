using System.Linq.Expressions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Kontrakt <c>Student</c> + <c>hemisId</c>, <c>groupId</c>, <c>course</c>, <c>telegramLinked</c>, <c>suspiciousDays</c>.
/// <paramref name="Id"/> — talabaning <c>User.Id</c> (tyutor endpoint'laridagi <c>studentId</c> bilan bir xil).
/// <paramref name="Company"/> — talabaning HOZIRDA aktiv korxonasi nomi (<see cref="ActiveCompanyQueries"/>: tasdiqlangan
/// ariza, yopilmagan va bugun davom etayotgan davr, guruh hali davrga biriktirilgan); aktivi bo'lmasa <c>null</c> —
/// oldingi (yopilgan/tugagan davrdagi) korxonaga fallback yo'q.</summary>
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

/// <summary><c>GET /api/admin/students?q&amp;page&amp;pageSize&amp;facultyId&amp;directionId&amp;course</c> (<c>pageSize</c> ≤ 500,
/// boshqa ro'yxatlarda ≤ 100) — <c>q</c>: FISH,
/// HEMIS ID, telefon, guruh. Filtrlar ixtiyoriy va birga (AND) qo'llanadi; <c>total</c> filtrlangan natija bo'yicha.
/// <see cref="DirectionId"/> <see cref="FacultyId"/> ga tegishli bo'lmasa — shunchaki bo'sh natija. Variantlar —
/// <c>GET /api/admin/students/filters</c> (<see cref="GetAdminStudentFiltersQuery"/>).</summary>
public sealed record GetAdminStudentsQuery : PagedQuery, IRequest<Paged<StudentRow>>
{
    /// <summary>Admin talabalar jadvalida foydalanuvchi sahifa hajmini o'zi tanlaydi — 500 gacha.</summary>
    public override int MaxPageSizeLimit => ExtendedMaxPageSize;

    /// <summary>Guruh yo'nalishi kafedrasining fakulteti.</summary>
    public Guid? FacultyId { get; init; }

    /// <summary>Guruh yo'nalishi.</summary>
    public Guid? DirectionId { get; init; }

    /// <summary>Guruh kursi (<see cref="StudentGroup.MinCourse"/>..<see cref="StudentGroup.MaxCourse"/>).</summary>
    public int? Course { get; init; }
}

internal sealed class GetAdminStudentsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetAdminStudentsQuery, Paged<StudentRow>>
{
    /// <summary>Ro'yxatning sukut (filtrsiz) to'plami: talaba profili → guruh → yo'nalish → kafedra → fakultet
    /// zanjiri to'liq bo'lganlar. Sidebar hisoblagichi (<c>GET /api/admin/nav</c>) ham shu manbadan sanaydi.</summary>
    internal static IQueryable<AdminStudentSource> Source(IApplicationDbContext db)
        => from p in db.StudentProfiles.AsNoTracking()
           join g in db.StudentGroups on p.StudentGroupId equals g.Id
           join d in db.Directions on g.DirectionId equals d.Id
           join dept in db.Departments on d.DepartmentId equals dept.Id
           join f in db.Faculties on dept.FacultyId equals f.Id
           select new AdminStudentSource { Profile = p, User = p.User, Group = g, Faculty = f };

    public async Task<Paged<StudentRow>> Handle(GetAdminStudentsQuery request, CancellationToken cancellationToken)
    {
        var students = Source(db);

        if (request.FacultyId is { } facultyId)
            students = students.Where(x => x.Faculty.Id == facultyId);
        if (request.DirectionId is { } directionId)
            students = students.Where(x => x.Group.DirectionId == directionId);
        if (request.Course is { } course)
            students = students.Where(x => x.Group.Course == course);

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
            .Select(Seed)
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<StudentRow>.Empty(request);

        var rows = await ToRowsAsync(db, clock, page.Items, cancellationToken);
        return new Paged<StudentRow>(rows, page.Page, page.PageSize, page.Total);
    }

    /// <summary>Bitta talabaning ro'yxat qatori (ro'yxatdagi bilan aynan bir xil hisob) — masalan yaratilgandan keyin
    /// (<c>POST /api/admin/students</c> javobi). Topilmasa <c>null</c>.</summary>
    internal static async Task<StudentRow?> LoadRowAsync(
        IApplicationDbContext db, IClock clock, Guid studentUserId, CancellationToken cancellationToken)
    {
        var seeds = await Source(db).Where(x => x.User.Id == studentUserId).Select(Seed).ToListAsync(cancellationToken);
        return seeds.Count == 0 ? null : (await ToRowsAsync(db, clock, seeds, cancellationToken))[0];
    }

    private static readonly Expression<Func<AdminStudentSource, StudentRowSeed>> Seed =
        x => new StudentRowSeed
        {
            Id = x.User.Id,
            FullName = x.User.FullName,
            HemisId = x.Profile.HemisId,
            GroupId = x.Group.Id,
            Group = x.Group.Name,
            Course = x.Group.Course,
            Faculty = x.Faculty.Name,
            TelegramLinked = x.User.TelegramUserId != null
        };

    /// <summary>Bazadan olingan asosiy maydonlarga davomat foizi, shubhali kunlar, aktiv korxona va holatni qo'shadi.</summary>
    private static async Task<List<StudentRow>> ToRowsAsync(
        IApplicationDbContext db, IClock clock, IReadOnlyList<StudentRowSeed> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(s => s.Id).ToList();
        var calendar = await PracticeCalendar.LoadAsync(db, clock, cancellationToken);
        var today = calendar.Today;

        // Har talaba — guruhining sukut bo'yicha davri (davom etayotgan → oxirgi tugagan → kelgusi) kesimida.
        var periodIds = items.Select(s => calendar.For(s.GroupId)?.PeriodId).OfType<Guid>().Distinct().ToList();

        var attendance = await db.DailyAttendances
            .AsNoTracking()
            .Where(a => ids.Contains(a.StudentUserId) && periodIds.Contains(a.PeriodId))
            .GroupBy(a => new { a.StudentUserId, a.PeriodId })
            .Select(g => new
            {
                g.Key.StudentUserId,
                g.Key.PeriodId,
                Attended = g.Count(a => a.Date < today && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)),
                Excused = g.Count(a => a.Date < today && a.Status == AttendanceStatus.Excused),
                Suspicious = g.Count(a => a.IsSuspicious)
            })
            .ToDictionaryAsync(x => (x.StudentUserId, x.PeriodId), cancellationToken);

        // Korxona — faqat aktiv (hozir davom etayotgan davrdagi tasdiqlangan) arizadan; davomat davridan mustaqil.
        var companies = await db.LoadActiveCompaniesAsync(ids, today, cancellationToken);

        return items.Select(s =>
        {
            var periodId = calendar.For(s.GroupId)?.PeriodId ?? Guid.Empty;
            var stats = attendance.GetValueOrDefault((s.Id, periodId));
            var elapsed = calendar.ElapsedWorkDays(s.GroupId);
            var pct = PracticeCalendar.AttendancePct(stats?.Attended ?? 0, elapsed, stats?.Excused ?? 0);
            var suspicious = stats?.Suspicious ?? 0;

            var status = AdminStudentStatusRule.For(s.TelegramLinked, pct, elapsed, suspicious);

            return new StudentRow(
                s.Id, s.FullName, s.HemisId, s.GroupId, s.Group, s.Course, s.Faculty,
                companies.GetValueOrDefault(s.Id)?.Name,
                pct, suspicious, s.TelegramLinked, status);
        }).ToList();
    }
}

/// <summary>Ro'yxat qatorining bazadan olinadigan qismi (EF proyeksiyasi uchun init-xossali tur).</summary>
internal sealed class StudentRowSeed
{
    public required Guid Id { get; init; }
    public required string FullName { get; init; }
    public required string HemisId { get; init; }
    public required Guid GroupId { get; init; }
    public required string Group { get; init; }
    public required int Course { get; init; }
    public required string Faculty { get; init; }
    public required bool TelegramLinked { get; init; }
}

/// <summary>Admin talabalar ro'yxati manbasi qatori (EF kompozitsiyasi uchun init-xossali tur, konstruktor emas).</summary>
internal sealed class AdminStudentSource
{
    public required StudentProfile Profile { get; init; }
    public required User User { get; init; }
    public required StudentGroup Group { get; init; }
    public required Faculty Faculty { get; init; }
}
