using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Guruhga biriktirilgan faol amaliyot davri (davr — guruh bo'yicha qisqacha).
/// <paramref name="ElapsedWorkDays"/> — davr boshidan kechagacha (bugun kirmaydi) o'tgan ish kunlari, davomat foizining maxraji.
/// <paramref name="ExpectedToday"/> / <paramref name="ExpectedYesterday"/> — shu kun bu guruh uchun ish kuni (davr ichida, ish kuni, bayram emas).</summary>
public sealed record GroupPractice(
    Guid PeriodId,
    string PeriodName,
    PracticePeriodStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    int ElapsedWorkDays,
    bool ExpectedToday,
    bool ExpectedYesterday);

/// <summary>Faol davrlar + bayramlar bir marta yuklanib, admin so'rovlari uchun "qaysi guruhdan bugun davomat kutiladi"
/// va "davomat foizi maxraji" savollariga xotirada javob beradi. Davrlar soni kichik (o'nlab), talabalar jadvaliga tegmaydi.
/// Bir guruh bir nechta faol davrda bo'lsa — o'tgan ish kunlari ko'proq bo'lgani olinadi.</summary>
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

    /// <summary>Faol davrlarning id'lari (ariza "yo'q"ligini aniqlash uchun).</summary>
    public IReadOnlyList<Guid> ActivePeriodIds => _byGroup.Values.Select(p => p.PeriodId).Distinct().ToList();

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
            .Where(p => p.Status == PracticePeriodStatus.Active && p.StartDate <= today)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Status,
                p.StartDate,
                p.EndDate,
                p.WorkDays,
                GroupIds = p.Groups.Select(g => g.StudentGroupId).ToList()
            })
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

        foreach (var period in periods)
        {
            var elapsed = 0;
            var lastPast = today.AddDays(-1) < period.EndDate ? today.AddDays(-1) : period.EndDate;
            for (var date = period.StartDate; date <= lastPast; date = date.AddDays(1))
            {
                if (period.WorkDays.Includes(date) && !IsHoliday(date))
                    elapsed++;
            }

            bool ExpectedOn(DateOnly date) => date >= period.StartDate && date <= period.EndDate
                && period.WorkDays.Includes(date) && !IsHoliday(date);

            var practice = new GroupPractice(
                period.Id, period.Name, period.Status, period.StartDate, period.EndDate, elapsed,
                ExpectedOn(today), ExpectedOn(today.AddDays(-1)));
            foreach (var groupId in period.GroupIds)
            {
                if (!byGroup.TryGetValue(groupId, out var existing) || existing.ElapsedWorkDays < elapsed)
                    byGroup[groupId] = practice;
            }
        }

        return new PracticeCalendar(today, byGroup)
        {
            GroupsExpectedToday = byGroup.Where(kv => kv.Value.ExpectedToday).Select(kv => kv.Key).ToList(),
            GroupsExpectedYesterday = byGroup.Where(kv => kv.Value.ExpectedYesterday).Select(kv => kv.Key).ToList()
        };
    }
}
