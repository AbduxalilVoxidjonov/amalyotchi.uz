using Amaliyotchi.Application.Common.Interfaces;
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
/// <c>practice</c> — sukut bo'yicha davr (davom etayotgan → oxirgi tugagan → eng yaqin kelgusi); davr yo'q → null.</summary>
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
    StudentProfilePracticeDto? Practice);

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

        StudentProfilePracticeDto? practice = null;
        if (detail.Period is { } period)
        {
            var status = await db.PracticePeriods.AsNoTracking()
                .Where(p => p.Id == period.Id)
                .Select(p => p.Status)
                .FirstAsync(cancellationToken);

            var finalized = await db.PracticeGrades.AsNoTracking()
                .AnyAsync(g => g.StudentUserId == userId && g.PeriodId == period.Id && g.FinalizedAt != null, cancellationToken);

            practice = new StudentProfilePracticeDto(
                new StudentProfilePeriodDto(
                    period.Id, period.Name,
                    PracticePeriod.ResolveStatus(status, period.StartDate, clock.LocalToday()),
                    period.StartDate, period.EndDate),
                detail.Company is { } company ? new StudentProfileCompanyDto(company.Id, company.Name, company.Address) : null,
                detail.Attendance.TotalDays + detail.Attendance.ExcusedDays,
                detail.Attendance.AttendancePct,
                detail.Attendance.SuspiciousDays,
                detail.Grade?.Total ?? 0,
                detail.Grade?.Grade,
                finalized);
        }

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
            practice);
    }
}
