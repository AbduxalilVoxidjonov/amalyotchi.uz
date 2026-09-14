using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Leave;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Davomat statistikasi (talaba uchun). <c>DaysTotal</c> — hisobga olingan ish kunlari (o'tganlar + bugun,
/// agar bugun yozuv bo'lsa yoki check-in oynasi yopilgan bo'lsa). <c>AttendancePct</c> — sababli kunlar
/// maxrajdan chiqariladi: keldi / (jami − sababli).</summary>
internal sealed record AttendanceStats(
    int DaysTotal, int DaysPresent, int Late, int Excused, int Absent, double AttendancePct);

internal sealed record DiaryStats(int Count, double AvgScore);

/// <summary>Kun holati va statistikani bitta joyda hisoblaydi — today, kalendar, portfolio bir xil natija beradi.</summary>
internal static class AttendanceCalendar
{
    /// <summary>Kun holati. Tartib: bazadagi yozuv → tasdiqlangan ruxsat → ish kuni emas → kelajak →
    /// bugun (oyna yopilgan bo'lsa kelmadi, aks holda kutilmoqda) → o'tgan kun kelmadi.</summary>
    public static CalendarDayStatus DayStatus(
        StudentPractice practice,
        DateOnly date,
        DateOnly today,
        TimeOnly localNow,
        DailyAttendance? row,
        IReadOnlyList<LeaveRequest> approvedLeaves)
    {
        if (row is not null)
        {
            var mapped = FromAttendance(row.Status);
            if (mapped is not null)
                return mapped.Value;
        }

        if (approvedLeaves.Any(l => l.Covers(date)))
            return CalendarDayStatus.Excused;

        if (!practice.IsWorkDay(date))
            return date > today && (practice.Period is null || !practice.Period.Contains(date))
                ? CalendarDayStatus.Future
                : CalendarDayStatus.DayOff;

        if (date > today)
            return CalendarDayStatus.Future;

        if (date == today)
            return localNow >= practice.Rules.WindowEnd ? CalendarDayStatus.Absent : CalendarDayStatus.Pending;

        return CalendarDayStatus.Absent;
    }

    /// <summary>Kalendar holatini bugungi davomat holatiga keltiradi (kelajak → kutilmoqda).</summary>
    public static AttendanceStatus ToAttendanceStatus(CalendarDayStatus status) => status switch
    {
        CalendarDayStatus.Present => AttendanceStatus.Present,
        CalendarDayStatus.Late => AttendanceStatus.Late,
        CalendarDayStatus.Absent => AttendanceStatus.Absent,
        CalendarDayStatus.Excused => AttendanceStatus.Excused,
        CalendarDayStatus.DayOff => AttendanceStatus.DayOff,
        _ => AttendanceStatus.Pending
    };

    public static AttendanceStats ComputeStats(
        StudentPractice practice,
        DateOnly today,
        TimeOnly localNow,
        IReadOnlyList<DailyAttendance> rows,
        IReadOnlyList<LeaveRequest> approvedLeaves)
    {
        var byDate = rows.ToDictionary(r => r.Date);
        int total = 0, present = 0, late = 0, excused = 0, absent = 0;

        // Ish kunlari + yozuvi bor kunlar (tyutor dam olish kuniga qo'lda "keldi" qo'ygan bo'lishi mumkin).
        var dates = new SortedSet<DateOnly>(practice.WorkDaysUntil(today));
        foreach (var row in rows)
        {
            if (row.Date <= today && practice.Period is not null && practice.Period.Contains(row.Date))
                dates.Add(row.Date);
        }

        foreach (var date in dates)
        {
            var row = byDate.GetValueOrDefault(date);
            if (date == today && row is null && localNow < practice.Rules.WindowEnd)
                continue; // bugun hali hal bo'lmagan

            total++;
            switch (DayStatus(practice, date, today, localNow, row, approvedLeaves))
            {
                case CalendarDayStatus.Present:
                    present++;
                    break;
                case CalendarDayStatus.Late:
                    present++;
                    late++;
                    break;
                case CalendarDayStatus.Excused:
                    excused++;
                    break;
                default:
                    absent++;
                    break;
            }
        }

        var denominator = total - excused;
        var pct = denominator <= 0 ? 0 : Math.Round(present * 100d / denominator, 1, MidpointRounding.AwayFromZero);
        return new AttendanceStats(total, present, late, excused, absent, Math.Clamp(pct, 0, 100));
    }

    public static DiaryStats ComputeDiaryStats(IEnumerable<DiaryEntry> entries)
    {
        var list = entries.ToList();
        var scores = list.Where(e => e.Score is not null).Select(e => e.Score!.Value).ToList();
        var avg = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero);
        return new DiaryStats(list.Count, avg);
    }

    private static CalendarDayStatus? FromAttendance(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => CalendarDayStatus.Present,
        AttendanceStatus.Late => CalendarDayStatus.Late,
        AttendanceStatus.Absent => CalendarDayStatus.Absent,
        AttendanceStatus.Excused => CalendarDayStatus.Excused,
        AttendanceStatus.DayOff => CalendarDayStatus.DayOff,
        _ => null
    };
}
