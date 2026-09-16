namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Admin ro'yxatlarida hisoblanadigan holatlar (kontrakt §6). JSON'da camelCase string.</summary>
public enum FacultyStatus
{
    Active = 1,
    /// <summary>Bugungi davomat <see cref="AdminThresholds.AttentionAttendancePct"/> dan past.</summary>
    Attention = 2
}

public enum TutorStatus
{
    Active = 1,
    /// <summary>Kutilayotgan ariza <see cref="AdminThresholds.PendingApplicationLateAfter"/> dan eski.</summary>
    Late = 2
}

/// <summary>Domain'dagi <c>StudentStatus</c> bilan adashmaslik uchun nomi boshqa.</summary>
public enum AdminStudentStatus
{
    Active = 1,
    /// <summary>Shubhali kunlar ≥ <see cref="AdminThresholds.FlaggedSuspiciousDays"/> yoki davomat &lt; <see cref="AdminThresholds.FlaggedAttendancePct"/>.</summary>
    Flagged = 2,
    /// <summary>Telegram hisobi bog'lanmagan.</summary>
    Unlinked = 3
}

/// <summary>Korxona bayrog'i. Ustuvorlik: <c>suspicious</c> → <c>tooManyStudents</c> → <c>largeRadius</c> → <c>null</c>.</summary>
public enum CompanyFlag
{
    /// <summary>Radius &gt; <see cref="AdminThresholds.LargeRadiusM"/>.</summary>
    LargeRadius = 1,
    /// <summary>Shu korxonadagi talabalarning shubhali davomat kunlari ≥ <see cref="AdminThresholds.SuspiciousCompanyEvents"/>.</summary>
    Suspicious = 2,
    /// <summary>Korxonaga biriktirilgan talabalar soni <c>maxStudentsPerCompany</c> sozlamasidan ko'p.</summary>
    TooManyStudents = 3
}
