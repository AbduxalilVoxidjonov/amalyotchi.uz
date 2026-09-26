using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Talabaning guruhiga biriktirilgan tyutor (bir nechta bo'lsa — FISH bo'yicha birinchisi).</summary>
public sealed record AdminStudentTutor(Guid Id, string FullName, string? Phone);

/// <summary><c>GET /api/admin/students/{id}</c> javobi — tyutor profili bilan bir xil bloklar
/// (<see cref="TutorStudentDetail"/>) + adminga xos maydonlar: kafedra, tyutor, Telegram bog'lanishi,
/// ro'yxatdagi holat va guruh identifikatori. <c>activeCompany</c> — tanlangan davrdan mustaqil aktiv korxona
/// (ro'yxatdagi <c>company</c> bilan bir xil qoida), <c>company</c> esa tanlangan davr bo'yicha.</summary>
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
    AdminStudentTutor? Tutor,
    StudentCompany? Company,
    StudentApplication? Application,
    StudentPeriod? Period,
    AttendanceSummary Attendance,
    DiarySummary Diary,
    StudentGrade? Grade,
    IReadOnlyList<StudentPeriodOption> Periods,
    Guid? SelectedPeriodId,
    bool HasPassword,
    ActiveCompanyRef? ActiveCompany);

/// <summary><c>GET /api/admin/students/{id}?periodId=</c> — talaba profili (davrga bog'liq bloklar tanlangan davr bo'yicha).
/// Talaba topilmasa yoki <c>periodId</c> talabaga tegishli bo'lmasa → 404.</summary>
public sealed record GetAdminStudentDetailQuery(Guid Id, Guid? PeriodId = null) : IRequest<AdminStudentDetail>;

/// <summary>Umumiy bloklarni <see cref="GetTutorStudentDetailQuery"/> hisoblaydi (admin ko'lami cheklovsiz,
/// shuning uchun ayni handler adminga ham to'g'ri keladi — topilmasa 404 ni ham o'sha beradi),
/// bu yerda faqat adminga xos maydonlar qo'shiladi.</summary>
internal sealed class GetAdminStudentDetailQueryHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<GetAdminStudentDetailQuery, AdminStudentDetail>
{
    public async Task<AdminStudentDetail> Handle(GetAdminStudentDetailQuery request, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetTutorStudentDetailQuery(request.Id, request.PeriodId), cancellationToken);

        var org = await (from p in db.StudentProfiles.AsNoTracking()
                         where p.UserId == request.Id
                         join g in db.StudentGroups on p.StudentGroupId equals g.Id
                         join d in db.Directions on g.DirectionId equals d.Id
                         join dept in db.Departments on d.DepartmentId equals dept.Id
                         select new
                         {
                             GroupId = g.Id,
                             Department = dept.Name,
                             TelegramLinked = p.User.TelegramUserId != null
                         })
            .FirstAsync(cancellationToken);

        var tutor = await (from a in db.TutorAssignments.AsNoTracking()
                           where a.IsActive && a.StudentGroupId == org.GroupId
                           join u in db.Users on a.TutorUserId equals u.Id
                           orderby u.FullName, u.Id
                           select new AdminStudentTutor(u.Id, u.FullName, u.PhoneNumber))
            .FirstOrDefaultAsync(cancellationToken);

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
            tutor,
            profile.Company,
            profile.Application,
            profile.Period,
            profile.Attendance,
            profile.Diary,
            profile.Grade,
            profile.Periods,
            profile.SelectedPeriodId,
            profile.HasPassword,
            profile.ActiveCompany);
    }
}
