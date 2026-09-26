using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Admin tomonidan korxonaga biriktirish uchun umumiy qoidalar — ommaviy
/// (<see cref="AssignStudentsToCompanyCommand"/>) va bitta talaba (<see cref="ReassignStudentCompanyCommand"/>) uchun.</summary>
internal static class StudentPlacement
{
    /// <summary>Admin biriktirganda qaror izohi (izoh berilmasa) — talaba profilida shu matn ko'rinadi.</summary>
    public const string DefaultComment = "Admin tomonidan biriktirildi.";

    /// <summary>Talabaning davrdagi "joriy" arizasi: <see cref="ApplicationStatus.Transferred"/> (o'tkazilgan) arizalar
    /// hisobga olinmaydi; tasdiqlangani ustun, aks holda eng so'nggisi.</summary>
    public static PracticeApplication? Current(IEnumerable<PracticeApplication> applications)
        => applications
            .Where(a => a.Status != ApplicationStatus.Transferred)
            .OrderBy(a => a.Status == ApplicationStatus.Approved ? 0 : 1)
            .ThenByDescending(a => a.SubmittedAt)
            .FirstOrDefault();

    /// <summary>Korxonaga darhol tasdiqlangan ariza (talaba ariza bermaydi, tyutor moderatsiyasi talab qilinmaydi).</summary>
    public static PracticeApplication CreateApproved(
        Guid studentUserId, Guid periodId, Company company, Guid adminId, string? comment, DateTimeOffset now)
    {
        var application = PracticeApplication.Create(
            studentUserId, periodId, company.Id, company.RadiusM, contractFileId: null, submittedAt: now);
        application.Approve(
            adminId, company.RadiusM, [], string.IsNullOrWhiteSpace(comment) ? DefaultComment : comment, now);
        return application;
    }
}
