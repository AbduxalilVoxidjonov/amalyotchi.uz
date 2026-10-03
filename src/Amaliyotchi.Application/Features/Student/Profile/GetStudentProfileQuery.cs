using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Profile;

public sealed record StudentProfileTutorDto(string FullName, string? PhoneNumber);

public sealed record StudentProfilePeriodDto(
    Guid Id, string Name, PracticePeriodStatus Status, DateOnly StartDate, DateOnly EndDate);

public sealed record StudentProfileCompanyDto(Guid Id, string Name, string? Address);

/// <param name="ElapsedWorkDays">Davr boshidan o'tgan (hisobga olinadigan) ish kunlari, sababli kunlar bilan birga.</param>
/// <param name="AttendancePct">Davomat foizi (sababli kunlar maxrajdan chiqariladi).</param>
/// <param name="Total">Joriy umumiy ball (0–100); davr boshlanmagan bo'lsa 0.</param>
/// <param name="Grade">2–5 yoki null (davomat yetarli emas yoki davr boshlanmagan).</param>
/// <param name="Finalized">Tyutor bahoni yakunlagan.</param>
public sealed record StudentProfilePracticeDto(
    StudentProfilePeriodDto Period,
    StudentProfileCompanyDto? Company,
    int ElapsedWorkDays,
    double AttendancePct,
    int SuspiciousDays,
    double Total,
    int? Grade,
    bool Finalized);

/// <summary><c>GET /api/student/profile</c> javobi — talabaning shaxsiy kabineti (brauzer va TWA).
/// <c>practice</c> — sukut bo'yicha davr (davom etayotgan → oxirgi tugagan → eng yaqin kelgusi); davr yo'q → null.
/// <c>practices</c> — talabaning BARCHA davrlari (<c>GET /api/student/period-days</c> dagi <c>periods</c> bilan bir xil to'plam),
/// har biri o'z davri bo'yicha; tartib: davom etayotgan ochiq davr(lar) birinchi, keyin <c>startDate</c> kamayish tartibida.
/// Davr yo'q → bo'sh ro'yxat. <c>workHours</c> — talabaning o'z ish vaqti va bugun amaldagi soatlar
/// (<see cref="StudentWorkHoursDto"/>).</summary>
public sealed record StudentProfileDto(
    Guid Id,
    string FullName,
    string HemisId,
    string? PhoneNumber,
    string Faculty,
    string Department,
    string Direction,
    string Group,
    int Course,
    StudentProfileTutorDto? Tutor,
    bool TelegramLinked,
    bool HasPassword,
    bool MustChangePassword,
    StudentProfilePracticeDto? Practice,
    IReadOnlyList<StudentProfilePracticeDto> Practices,
    StudentWorkHoursDto WorkHours);

public sealed record GetStudentProfileQuery : IRequest<StudentProfileDto>;

/// <summary>Hisob-kitoblar tyutor/admin profilidagi bilan AYNAN bir xil bo'lishi uchun umumiy bloklarni
/// <see cref="GetTutorStudentDetailQuery"/> hisoblaydi (talaba ko'lami — faqat o'zi, <c>ScopeKind.Self</c>);
/// bu yerda faqat talaba kabinetiga xos maydonlar (kafedra, tyutor, parol holati, davr holati, yakunlanganlik) qo'shiladi.</summary>
internal sealed class GetStudentProfileQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, ISender sender, IClock clock)
    : IRequestHandler<GetStudentProfileQuery, StudentProfileDto>
{
    public async Task<StudentProfileDto> Handle(GetStudentProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var detail = await sender.Send(new GetTutorStudentDetailQuery(userId), cancellationToken);

        var org = await (from p in db.StudentProfiles.AsNoTracking()
                         where p.UserId == userId
                         join g in db.StudentGroups on p.StudentGroupId equals g.Id
                         join d in db.Directions on g.DirectionId equals d.Id
                         join dept in db.Departments on d.DepartmentId equals dept.Id
                         select new
                         {
                             GroupId = g.Id,
                             Department = dept.Name,
                             TelegramLinked = p.User.TelegramUserId != null,
                             p.User.MustChangePassword
                         })
            .FirstAsync(cancellationToken);

        // Admin profilidagi qoida: guruhga biriktirilgan faol tyutor, bir nechta bo'lsa FISH bo'yicha birinchisi.
        var tutor = await (from a in db.TutorAssignments.AsNoTracking()
                           where a.IsActive && a.StudentGroupId == org.GroupId
                           join u in db.Users on a.TutorUserId equals u.Id
                           orderby u.FullName, u.Id
                           select new StudentProfileTutorDto(u.FullName, u.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);

        var today = clock.LocalToday();
        var options = detail.Periods;

        // Yakunlangan baholar — barcha davrlar uchun bitta so'rov.
        var periodIds = options.Select(o => o.Id).ToList();
        var finalizedIds = periodIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.PracticeGrades.AsNoTracking()
                .Where(g => g.StudentUserId == userId && periodIds.Contains(g.PeriodId) && g.FinalizedAt != null)
                .Select(g => g.PeriodId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Har davr ko'rsatkichlari tyutor/admin profili bilan AYNAN bir manbadan (GetTutorStudentDetailQuery, periodId bilan).
        // Sukut davri uchun yuqoridagi `detail` qayta ishlatiladi; qolgan har davr uchun bitta qo'shimcha chaqiruv
        // (cheklangan sondagi so'rovlar). Talaba davrlari soni amalda 1–3 ta — so'rovlar soni davrlar soniga chiziqli, lekin kichik.
        var details = new Dictionary<Guid, TutorStudentDetail>();
        if (detail.Period is { } defaultPeriod)
            details[defaultPeriod.Id] = detail;
        foreach (var option in options)
        {
            if (!details.ContainsKey(option.Id))
                details[option.Id] = await sender.Send(new GetTutorStudentDetailQuery(userId, option.Id), cancellationToken);
        }

        var practices = options
            .OrderByDescending(o => IsOngoing(o, today))
            .ThenByDescending(o => o.StartDate)
            .ThenBy(o => o.Name, StringComparer.Ordinal)
            .Select(o => ToPractice(details[o.Id], o.Status, finalizedIds.Contains(o.Id)))
            .OfType<StudentProfilePracticeDto>()
            .ToList();

        // `practice` — sukut davri (o'zgarmagan qoida); holati PracticePeriod.ResolveStatus bilan.
        StudentProfilePracticeDto? practice = null;
        if (detail.Period is { } period)
        {
            var status = await db.PracticePeriods.AsNoTracking()
                .Where(p => p.Id == period.Id)
                .Select(p => p.Status)
                .FirstAsync(cancellationToken);
            practice = ToPractice(detail, PracticePeriod.ResolveStatus(status, period.StartDate, today), finalizedIds.Contains(period.Id));
        }

        var workHours = await StudentWorkHoursMapper.LoadAsync(db, userId, today, cancellationToken);

        return new StudentProfileDto(
            detail.Id,
            detail.Name,
            detail.HemisId,
            detail.Phone,
            detail.Faculty,
            org.Department,
            detail.Direction,
            detail.Group,
            detail.Course,
            tutor,
            org.TelegramLinked,
            detail.HasPassword,
            org.MustChangePassword,
            practice,
            practices,
            workHours);
    }

    /// <summary>Davom etayotgan ochiq davr: yopilmagan, boshlangan va tugash sanasi o'tmagan.</summary>
    private static bool IsOngoing(StudentPeriodOption option, DateOnly today)
        => option.Status == PracticePeriodStatus.Active && option.StartDate <= today && today <= option.EndDate;

    /// <summary>Bitta davr bo'yicha tyutor profili bloklaridan talaba kabineti elementini yig'adi.</summary>
    private static StudentProfilePracticeDto? ToPractice(TutorStudentDetail d, PracticePeriodStatus status, bool finalized)
        => d.Period is not { } period
            ? null
            : new StudentProfilePracticeDto(
                new StudentProfilePeriodDto(period.Id, period.Name, status, period.StartDate, period.EndDate),
                d.Company is { } company ? new StudentProfileCompanyDto(company.Id, company.Name, company.Address) : null,
                d.Attendance.TotalDays + d.Attendance.ExcusedDays,
                d.Attendance.AttendancePct,
                d.Attendance.SuspiciousDays,
                d.Grade?.Total ?? 0,
                d.Grade?.Grade,
                finalized);
}
