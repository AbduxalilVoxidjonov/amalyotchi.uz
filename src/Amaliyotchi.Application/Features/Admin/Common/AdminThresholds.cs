namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Admin ro'yxatlaridagi holat/bayroq chegaralari — bitta joyda, testlar ham shunga tayanadi.</summary>
public static class AdminThresholds
{
    /// <summary>Fakultet: bugungi davomat shundan past → <c>attention</c>.</summary>
    public const int AttentionAttendancePct = 70;

    /// <summary>Ariza: shundan uzoq javobsiz kutilayotgan ariza — muddati o'tgan (<c>applicationsOverdue</c>).</summary>
    public static readonly TimeSpan PendingApplicationLateAfter = TimeSpan.FromHours(48);

    /// <summary>Talaba: davomat shundan past → <c>flagged</c>.</summary>
    public const int FlaggedAttendancePct = 70;

    /// <summary>Talaba: shubhali kunlar soni shundan boshlab → <c>flagged</c>.</summary>
    public const int FlaggedSuspiciousDays = 2;

    /// <summary>Korxona: radius shundan katta → <c>largeRadius</c>.</summary>
    public const int LargeRadiusM = 500;

    /// <summary>Korxona: shubhali davomat kunlari shundan boshlab → <c>suspicious</c>.</summary>
    public const int SuspiciousCompanyEvents = 3;

    /// <summary>Dashboard'da ko'rsatiladigan so'nggi audit yozuvlari soni.</summary>
    public const int DashboardAuditCount = 8;
}
