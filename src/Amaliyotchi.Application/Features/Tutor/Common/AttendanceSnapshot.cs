using Amaliyotchi.Domain.Attendance;

namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Davomat qatorining o'qish uchun yengil nusxasi (tracked entity emas).</summary>
public sealed record AttendanceSnapshot(
    Guid StudentUserId,
    Guid PeriodId,
    DateOnly Date,
    AttendanceStatus Status,
    DateTimeOffset? CheckInAt,
    double? CheckInDistanceM,
    DateTimeOffset? CheckOutAt,
    bool AutoClosed,
    bool IsSuspicious,
    bool IsManual);

internal static class AttendanceSnapshotQueries
{
    public static IQueryable<AttendanceSnapshot> SelectSnapshot(this IQueryable<DailyAttendance> query)
        => query.Select(a => new AttendanceSnapshot(
            a.StudentUserId, a.PeriodId, a.Date, a.Status, a.CheckInAt, a.CheckInDistanceM, a.CheckOutAt,
            a.AutoClosed, a.IsSuspicious, a.IsManual));
}

/// <summary>Bazada qatori yo'q kun uchun holat: dam olish / sababli (ruxsat) / kelmadi / kutilmoqda.</summary>
public static class AttendanceStatusResolver
{
    public static AttendanceStatus Resolve(
        PeriodContext? period, DateOnly date, DateOnly today, TimeOnly localNow, bool hasApprovedLeave)
    {
        if (period is null || !period.IsWorkDay(date))
            return AttendanceStatus.DayOff;
        if (hasApprovedLeave)
            return AttendanceStatus.Excused;
        if (date < today)
            return AttendanceStatus.Absent;
        if (date > today)
            return AttendanceStatus.Pending;
        return period.IsWindowClosed(localNow) ? AttendanceStatus.Absent : AttendanceStatus.Pending;
    }

    /// <summary>Kalendar katagi: qator bo'lsa undan, bo'lmasa hisoblab; kelajakdagi ish kuni — <c>future</c>.</summary>
    public static CalendarDayStatus ResolveCalendar(
        PeriodContext? period, DateOnly date, DateOnly today, TimeOnly localNow, AttendanceSnapshot? row, bool hasApprovedLeave)
    {
        if (row is not null)
            return ToCalendar(row.Status);

        if (period is null || !period.IsWorkDay(date))
            return CalendarDayStatus.DayOff;
        if (hasApprovedLeave)
            return CalendarDayStatus.Excused;
        if (date > today)
            return CalendarDayStatus.Future;

        return ToCalendar(Resolve(period, date, today, localNow, hasApprovedLeave: false));
    }

    public static CalendarDayStatus ToCalendar(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => CalendarDayStatus.Present,
        AttendanceStatus.Late => CalendarDayStatus.Late,
        AttendanceStatus.Absent => CalendarDayStatus.Absent,
        AttendanceStatus.Excused => CalendarDayStatus.Excused,
        AttendanceStatus.DayOff => CalendarDayStatus.DayOff,
        _ => CalendarDayStatus.Pending
    };
}
