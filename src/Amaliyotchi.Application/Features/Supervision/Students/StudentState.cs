using Amaliyotchi.Application.Features.Supervision.Common;
using Amaliyotchi.Domain.Grading;

namespace Amaliyotchi.Application.Features.Supervision.Students;

public enum StudentState
{
    Active = 1,
    RedFlag = 2,
    Suspicious = 3
}

/// <summary>Talaba holati qoidasi — profil (<c>GET /api/admin/students/{id}</c>) va korxona talabalari ro'yxati
/// bir xil hisoblasin.</summary>
public static class StudentStateRule
{
    /// <summary>Kamida shuncha shubhali kun bo'lsa "shubhali" holat.</summary>
    public const int SuspiciousMinCount = 1;

    /// <summary>Davomat &lt; 70% → <c>redFlag</c>; shubhali kunlar bor → <c>suspicious</c>; aks holda <c>active</c>.</summary>
    public static StudentState For(StudentStats stats)
        => stats.TotalDays > 0 && stats.AttendancePct < GradeThresholds.MinAttendancePct
            ? StudentState.RedFlag
            : stats.SuspiciousCount >= SuspiciousMinCount
                ? StudentState.Suspicious
                : StudentState.Active;
}
