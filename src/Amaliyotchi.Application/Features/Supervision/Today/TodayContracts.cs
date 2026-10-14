using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Supervision.Tracking;
using Amaliyotchi.Domain.Attendance;

namespace Amaliyotchi.Application.Features.Supervision.Today;

/// <summary><c>?status=</c> filtri: enum holatlari + <c>suspicious</c> (bayroq bo'yicha).</summary>
public enum TodayFilter
{
    Present = 1,
    Late = 2,
    Absent = 3,
    Excused = 4,
    Pending = 5,
    Suspicious = 6
}

/// <summary><c>?presence=</c> filtri (live joylashuv): hozir tashqarida yoki signal yo'q.</summary>
public enum TodayPresenceFilter
{
    Outside = 1,
    SignalLost = 2
}

public enum TodayAlertKind
{
    OutOfRadius = 1,
    NotCheckedIn = 2,
    // 3 — NewLeaveRequests: ruxsat so'rash moduli olib tashlandi (24.09.2026); raqam qayta ishlatilmasin.
    NewApplications = 4
}

public enum DiaryState
{
    Written = 1,
    Pending = 2
}

public sealed record TodayStats(int Present, int Late, int Absent, int Excused, int Pending, int Diaries, int Total);

/// <param name="MaxDistanceM">Faqat <c>outOfRadius</c> uchun — bugungi eng uzoq rad etilgan urinish (m).</param>
public sealed record TodayAlert(TodayAlertKind Kind, int Count, string Href, double? MaxDistanceM = null);

/// <param name="Presence">Live joylashuv holati (docs/LIVE-JOYLASHUV.md); null — bugun check-in yo'q yoki kuzatuv o'chiq.</param>
public sealed record AttendanceRow(
    Guid StudentId,
    string Name,
    string Group,
    string? Company,
    string? CheckIn,
    string? CheckOut,
    DiaryState? Diary,
    double? DistanceM,
    bool OutOfRadius,
    AttendanceStatus Status,
    bool Suspicious,
    bool Manual,
    bool AutoClosed,
    PresenceDto? Presence = null);

public sealed record TodayResponse(DateOnly Date, TodayStats Stats, IReadOnlyList<TodayAlert> Alerts, Paged<AttendanceRow> Rows);
