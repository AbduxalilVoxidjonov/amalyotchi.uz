namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Talabaning admin ro'yxatidagi holati — ro'yxat (<c>GET /api/admin/students</c>) va profil
/// (<c>GET /api/admin/students/{id}</c>) bir xil hisoblasin.</summary>
public static class AdminStudentStatusRule
{
    /// <summary>Telegram bog'lanmagan → <c>unlinked</c>; shubhali kunlar yoki past davomat → <c>flagged</c>;
    /// aks holda <c>active</c>. <paramref name="countedDays"/> 0 bo'lsa davomat foizi hisobga olinmaydi
    /// (amaliyot hali boshlanmagan — 0% "past davomat" emas).</summary>
    public static AdminStudentStatus For(bool telegramLinked, double attendancePct, int countedDays, int suspiciousDays)
        => !telegramLinked
            ? AdminStudentStatus.Unlinked
            : suspiciousDays >= AdminThresholds.FlaggedSuspiciousDays
              || (countedDays > 0 && attendancePct < AdminThresholds.FlaggedAttendancePct)
                ? AdminStudentStatus.Flagged
                : AdminStudentStatus.Active;
}
