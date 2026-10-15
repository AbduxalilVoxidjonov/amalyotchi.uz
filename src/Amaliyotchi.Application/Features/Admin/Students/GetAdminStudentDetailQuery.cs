using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Student.Profile;
using Amaliyotchi.Application.Features.Supervision.Students;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>GET /api/admin/students/{id}</c> javobi — umumiy talaba profili bloklari
/// (<see cref="StudentDetail"/>) + adminga xos maydonlar: kafedra, Telegram bog'lanishi,
/// ro'yxatdagi holat va guruh identifikatori. <c>activeCompany</c> — tanlangan davrdan mustaqil aktiv korxona
/// (ro'yxatdagi <c>company</c> bilan bir xil qoida), <c>company</c> esa tanlangan davr bo'yicha.
/// <c>workHours</c> — admin belgilagan ish vaqti (<c>PUT /api/admin/students/{id}/work-hours</c>), kuchga kirish sanasi,
/// bugun amaldagi va sukut davri soatlari (tanlangan davrdan mustaqil).
/// v3.34: <c>mustChangePassword</c> — parol vaqtinchalik (admin bergan, talaba hali almashtirmagan; parol yo'q bo'lsa
/// <c>false</c>), <c>lastLoginAt</c> — oxirgi muvaffaqiyatli kirish (ilova, brauzer yoki Telegram; hech qachon → null).</summary>
public sealed record AdminStudentDetail(
    Guid Id,
    string Name,
    string HemisId,
    Guid GroupId,
    string Group,
    int Course,
    string Faculty,
    string Department,
    string Direction,
    StudentStatus Status,
    AdminStudentStatus AdminStatus,
    string? Phone,
    bool TelegramLinked,
    StudentState State,
    int SuspiciousCount,
    StudentCompany? Company,
    StudentApplication? Application,
    StudentPeriod? Period,
    AttendanceSummary Attendance,
    DiarySummary Diary,
    StudentGrade? Grade,
    IReadOnlyList<StudentPeriodOption> Periods,
    Guid? SelectedPeriodId,
    bool HasPassword,
    ActiveCompanyRef? ActiveCompany,
    StudentFaceDto Face,
    StudentWorkHoursDto WorkHours,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt);

/// <summary><c>GET /api/admin/students/{id}?periodId=</c> — talaba profili (davrga bog'liq bloklar tanlangan davr bo'yicha).
/// Talaba topilmasa yoki <c>periodId</c> talabaga tegishli bo'lmasa → 404.</summary>
public sealed record GetAdminStudentDetailQuery(Guid Id, Guid? PeriodId = null) : IRequest<AdminStudentDetail>;

/// <summary>Umumiy bloklarni <see cref="GetStudentDetailQuery"/> hisoblaydi (admin ko'lami cheklovsiz,
/// shuning uchun ayni handler adminga ham to'g'ri keladi — topilmasa 404 ni ham o'sha beradi),
/// bu yerda faqat adminga xos maydonlar qo'shiladi.</summary>
internal sealed class GetAdminStudentDetailQueryHandler(IApplicationDbContext db, ISender sender, IClock clock)
    : IRequestHandler<GetAdminStudentDetailQuery, AdminStudentDetail>
{
    public async Task<AdminStudentDetail> Handle(GetAdminStudentDetailQuery request, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetStudentDetailQuery(request.Id, request.PeriodId), cancellationToken);

        var org = await (from p in db.StudentProfiles.AsNoTracking()
                         where p.UserId == request.Id
                         join g in db.StudentGroups on p.StudentGroupId equals g.Id
                         join d in db.Directions on g.DirectionId equals d.Id
                         join dept in db.Departments on d.DepartmentId equals dept.Id
                         select new
                         {
                             GroupId = g.Id,
                             Department = dept.Name,
                             TelegramLinked = p.User.TelegramUserId != null,
                             p.User.MustChangePassword,
                             p.User.LastLoginAt
                         })
            .FirstAsync(cancellationToken);

        var adminStatus = AdminStudentStatusRule.For(
            org.TelegramLinked, profile.Attendance.AttendancePct, profile.Attendance.TotalDays, profile.Attendance.SuspiciousDays);

        return new AdminStudentDetail(
            profile.Id,
            profile.Name,
            profile.HemisId,
            org.GroupId,
            profile.Group,
            profile.Course,
            profile.Faculty,
            org.Department,
            profile.Direction,
            profile.Status,
            adminStatus,
            profile.Phone,
            org.TelegramLinked,
            profile.State,
            profile.SuspiciousCount,
            profile.Company,
            profile.Application,
            profile.Period,
            profile.Attendance,
            profile.Diary,
            profile.Grade,
            profile.Periods,
            profile.SelectedPeriodId,
            profile.HasPassword,
            profile.ActiveCompany,
            profile.Face,
            await StudentWorkHoursMapper.LoadAsync(db, profile.Id, clock.LocalToday(), cancellationToken),
            profile.HasPassword && org.MustChangePassword,
            org.LastLoginAt);
    }
}
