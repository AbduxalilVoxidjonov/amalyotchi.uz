using Amaliyotchi.Domain.Attendance;

namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Talabaning davr bo'yicha davomat ko'rsatkichlari. <see cref="TotalDays"/> — o'tgan ish kunlari
/// (bugun — oyna yopilgan bo'lsa), sababli kunlar maxrajdan chiqariladi.</summary>
public sealed record StudentStats(
    int TotalDays,
    int AttendedDays,
    int LateDays,
    int ExcusedDays,
    int SuspiciousCount,
    double AttendancePct);

/// <summary>Kundalik ko'rsatkichlari: jami yozuvlar, baholanganlari va o'rtacha ball (baholanganlar bo'yicha).</summary>
public sealed record DiaryStats(int Count, int ScoredCount, double Avg)
{
    public static DiaryStats Empty { get; } = new(0, 0, 0);
}

public static class StudentStatsCalculator
{
    /// <summary>Davomat foizi = kelgan (keldi + kech keldi) / hisobga olinadigan ish kunlari × 100.
    /// <paramref name="rows"/> dan faqat <paramref name="period"/> ga tegishlilari olinadi.
    /// <paramref name="ownHours"/> — talabaning bugun amaldagi o'z ish vaqti (bugungi oyna shundan hisoblanadi).</summary>
    public static StudentStats ComputeAttendance(
        PeriodContext? period,
        IReadOnlyCollection<AttendanceSnapshot> rows,
        IReadOnlyCollection<(DateOnly From, DateOnly To)> approvedLeaves,
        DateOnly today,
        TimeOnly localNow,
        (TimeOnly Start, TimeOnly End)? ownHours = null)
    {
        if (period is null)
            return new StudentStats(0, 0, 0, 0, rows.Count(r => r.IsSuspicious), 0);

        // Faqat shu davr qatorlari — talabaning boshqa davrlari (kuzgi/bahorgi) aralashmasin.
        var periodId = period.Period.Id;
        rows = rows.Where(r => r.PeriodId == periodId).ToList();
        var suspicious = rows.Count(r => r.IsSuspicious);

        var byDate = rows.ToDictionary(r => r.Date);
        var countable = new HashSet<DateOnly>(ElapsedWorkDays(period, today, localNow, ownHours));

        // Oyna hali yopilmagan bo'lsa ham, bugun allaqachon belgilangan kun hisobga kiradi.
        foreach (var row in rows.Where(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late))
            countable.Add(row.Date);

        var attended = 0;
        var late = 0;
        var excused = 0;
        foreach (var date in countable)
        {
            var row = byDate.GetValueOrDefault(date);
            if (row?.Status == AttendanceStatus.Excused || (row is null && approvedLeaves.Any(l => date >= l.From && date <= l.To)))
            {
                excused++;
                continue;
            }

            if (row?.Status is AttendanceStatus.Present or AttendanceStatus.Late)
            {
                attended++;
                if (row.Status == AttendanceStatus.Late)
                    late++;
            }
        }

        var total = countable.Count - excused;
        var pct = total == 0 ? 0 : Math.Round(attended * 100d / total, 1, MidpointRounding.AwayFromZero);
        return new StudentStats(total, attended, late, excused, suspicious, pct);
    }

    /// <summary>Davr boshidan hisobga olinadigan (o'tgan) ish kunlari: kechagacha + bugun (check-in oynasi yopilgan
    /// bo'lsa), davr tugash sanasi bilan cheklangan. <see cref="ComputeAttendance"/> maxrajining asosi (sababli kunlar va
    /// bugun allaqachon belgilangan kun talaba bo'yicha qo'shimcha hisoblanadi). <paramref name="ownHours"/> — talabaning
    /// bugungi o'z ish vaqti (davr bo'yicha umumiy hisobda null — davr oynasi).</summary>
    public static IEnumerable<DateOnly> ElapsedWorkDays(
        PeriodContext period, DateOnly today, TimeOnly localNow, (TimeOnly Start, TimeOnly End)? ownHours = null)
    {
        var lastCountable = today < period.Period.EndDate ? today : period.Period.EndDate;
        foreach (var date in period.WorkDays(period.Period.StartDate, lastCountable))
        {
            if (date < today || period.IsWindowClosed(localNow, ownHours))
                yield return date;
        }
    }

    public static DiaryStats ComputeDiary(IReadOnlyCollection<int?> scores)
    {
        if (scores.Count == 0)
            return DiaryStats.Empty;

        var scored = scores.Where(s => s is not null).Select(s => s!.Value).ToList();
        var avg = scored.Count == 0 ? 0 : Math.Round(scored.Average(), 1, MidpointRounding.AwayFromZero);
        return new DiaryStats(scores.Count, scored.Count, avg);
    }
}
