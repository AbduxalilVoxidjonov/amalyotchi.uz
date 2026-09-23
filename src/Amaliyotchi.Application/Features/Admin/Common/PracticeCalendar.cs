using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Guruhning sukut bo'yicha amaliyot davri (davr — guruh bo'yicha qisqacha). <paramref name="Status"/> — ko'rinadigan
/// holat (<see cref="PracticePeriod.ResolveStatus"/>: planned / active / closed).
/// <paramref name="ElapsedWorkDays"/> — davr boshidan kechagacha (bugun kirmaydi, davr oxiridan oshmaydi) o'tgan ish kunlari,
/// davomat foizining maxraji. <paramref name="ExpectedToday"/> / <paramref name="ExpectedYesterday"/> — shu kun bu guruh uchun
/// ish kuni (yopilmagan davr ichida, ish kuni, bayram emas).</summary>
public sealed record GroupPractice(
    Guid PeriodId,
    string PeriodName,
    PracticePeriodStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    int ElapsedWorkDays,
    bool ExpectedToday,
    bool ExpectedYesterday);

/// <summary>Davrlar + bayramlar bir marta yuklanib, admin so'rovlari uchun "qaysi guruhdan bugun davomat kutiladi"
/// va "davomat foizi maxraji" savollariga xotirada javob beradi. Davrlar soni kichik (o'nlab), talabalar jadvaliga tegmaydi.
/// Har guruh uchun davr <see cref="PeriodSelection"/> qoidasi bilan tanlanadi (<see cref="PeriodPurpose.Default"/>:
/// davom etayotgan → oxirgi tugagan → eng yaqin kelgusi) — ikki davr oralig'ida statistika tugagan kuzgi davr bo'yicha
/// qoladi, kelajakdagi bo'sh davrga o'tib ketmaydi.</summary>
public sealed class PracticeCalendar
{
    private readonly Dictionary<Guid, GroupPractice> _byGroup;

    private PracticeCalendar(DateOnly today, Dictionary<Guid, GroupPractice> byGroup)
    {
        Today = today;
        _byGroup = byGroup;
    }

    public DateOnly Today { get; }

    public IReadOnlyDictionary<Guid, GroupPractice> ByGroup => _byGroup;

    /// <summary>Bugun davomat kutiladigan guruhlar (bo'sh bo'lishi mumkin — dam olish kuni yoki davr yo'q).</summary>
    public IReadOnlyList<Guid> GroupsExpectedToday { get; private set; } = [];

    public IReadOnlyList<Guid> GroupsExpectedYesterday { get; private set; } = [];

    public GroupPractice? For(Guid groupId) => _byGroup.GetValueOrDefault(groupId);

    public int ElapsedWorkDays(Guid groupId) => _byGroup.TryGetValue(groupId, out var p) ? p.ElapsedWorkDays : 0;

    /// <summary>Davomat foizi: kelgan kunlar / (o'tgan ish kunlari − sababli kunlar). Maxraj 0 bo'lsa 0.</summary>
    public static int AttendancePct(int attended, int elapsedWorkDays, int excused)
    {
        var denominator = elapsedWorkDays - excused;
        if (denominator <= 0)
            return 0;
        return Math.Clamp((int)Math.Round(attended * 100.0 / denominator, MidpointRounding.AwayFromZero), 0, 100);
    }

    public static async Task<PracticeCalendar> LoadAsync(IApplicationDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var today = clock.LocalToday();

        var periods = await db.PracticePeriods
            .AsNoTracking()
            .Select(p => new PeriodRow(
                p.Id,
                p.Name,
                p.Status,
                p.StartDate,
                p.EndDate,
                p.WorkDays,
                p.Groups.Select(g => g.StudentGroupId).ToList()))
            .ToListAsync(cancellationToken);

        var byGroup = new Dictionary<Guid, GroupPractice>();
        if (periods.Count == 0)
            return new PracticeCalendar(today, byGroup);

        var holidays = await db.Holidays
            .AsNoTracking()
            .Select(h => new { h.Date, h.IsRecurring })
            .ToListAsync(cancellationToken);

        bool IsHoliday(DateOnly date) => holidays.Any(h => h.IsRecurring
            ? h.Date.Month == date.Month && h.Date.Day == date.Day
            : h.Date == date);

        var practices = new Dictionary<Guid, GroupPractice>();
        GroupPractice Practice(PeriodRow period)
        {
            if (practices.TryGetValue(period.Id, out var cached))
                return cached;

            var elapsed = 0;
            var lastPast = today.AddDays(-1) < period.EndDate ? today.AddDays(-1) : period.EndDate;
            for (var date = period.StartDate; date <= lastPast; date = date.AddDays(1))
            {
                if (period.WorkDays.Includes(date) && !IsHoliday(date))
                    elapsed++;
            }

            bool ExpectedOn(DateOnly date) => period.Status != PracticePeriodStatus.Closed
                && date >= period.StartDate && date <= period.EndDate
                && period.WorkDays.Includes(date) && !IsHoliday(date);

            var practice = new GroupPractice(
                period.Id, period.Name, PracticePeriod.ResolveStatus(period.Status, period.StartDate, today),
                period.StartDate, period.EndDate, elapsed, ExpectedOn(today), ExpectedOn(today.AddDays(-1)));
            practices[period.Id] = practice;
            return practice;
        }

        foreach (var group in periods.SelectMany(p => p.GroupIds.Select(g => (GroupId: g, Period: p))).GroupBy(x => x.GroupId))
        {
            var selected = PeriodSelection.Select(
                group.Select(x => x.Period).ToList(), today, PeriodPurpose.Default,
                p => new PeriodSpan(p.StartDate, p.EndDate, p.Status == PracticePeriodStatus.Closed));
            if (selected is not null)
                byGroup[group.Key] = Practice(selected);
        }

        return new PracticeCalendar(today, byGroup)
        {
            GroupsExpectedToday = byGroup.Where(kv => kv.Value.ExpectedToday).Select(kv => kv.Key).ToList(),
            GroupsExpectedYesterday = byGroup.Where(kv => kv.Value.ExpectedYesterday).Select(kv => kv.Key).ToList()
        };
    }

    private sealed record PeriodRow(
        Guid Id,
        string Name,
        PracticePeriodStatus Status,
        DateOnly StartDate,
        DateOnly EndDate,
        WorkDays WorkDays,
        List<Guid> GroupIds);
}
