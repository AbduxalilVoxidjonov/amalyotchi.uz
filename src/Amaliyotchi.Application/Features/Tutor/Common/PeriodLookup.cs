using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Amaliyot davri + uning vaqt qoidalari + bayramlar — "ish kuni" va oynalar shu yerdan hisoblanadi.</summary>
public sealed class PeriodContext
{
    private readonly IReadOnlyList<Holiday> _holidays;

    public PeriodContext(PracticePeriod period, CheckInRules rules, IReadOnlyList<Holiday> holidays)
    {
        Period = period;
        Rules = rules;
        _holidays = holidays;
    }

    public PracticePeriod Period { get; }
    public CheckInRules Rules { get; }

    public bool IsHoliday(DateOnly date) => _holidays.Any(h => h.AppliesTo(date));

    public bool IsWorkDay(DateOnly date) => Period.IsWorkDay(date, IsHoliday(date));

    /// <summary>[from, to] oralig'idagi ish kunlari (davr chegaralari bilan kesishgan holda).</summary>
    public IEnumerable<DateOnly> WorkDays(DateOnly from, DateOnly to)
    {
        var start = from > Period.StartDate ? from : Period.StartDate;
        var end = to < Period.EndDate ? to : Period.EndDate;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (IsWorkDay(date))
                yield return date;
        }
    }

    /// <summary>Bugungi kun statistikaga kiradimi — check-in oynasi yopilgan bo'lsa (10:30 dan keyin).</summary>
    public bool IsWindowClosed(TimeOnly localNow) => localNow >= Rules.WindowEnd;
}

/// <summary>Guruh → faol amaliyot davri xaritasi. Faol davrlar oz (bir vaqtda 1–2 ta), shuning uchun hammasi
/// bir marta yuklanadi; bir guruhga bir nechta faol davr bo'lsa — bugunni o'z ichiga olgani, aks holda eng yangisi.</summary>
public sealed class PeriodLookup
{
    private readonly Dictionary<Guid, PeriodContext> _byGroup;

    private PeriodLookup(Dictionary<Guid, PeriodContext> byGroup, IReadOnlyList<PeriodContext> all)
    {
        _byGroup = byGroup;
        All = all;
    }

    public IReadOnlyList<PeriodContext> All { get; }

    public IReadOnlyCollection<Guid> PeriodIds => All.Select(p => p.Period.Id).ToArray();

    public PeriodContext? ForGroup(Guid groupId) => _byGroup.GetValueOrDefault(groupId);

    public PeriodContext? ForPeriod(Guid periodId) => All.FirstOrDefault(p => p.Period.Id == periodId);

    public static async Task<PeriodLookup> LoadAsync(IApplicationDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var periods = await db.PracticePeriods
            .AsNoTracking()
            .Include(p => p.Groups)
            .Where(p => p.Status == PracticePeriodStatus.Active)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var minAccuracy = await LoadMinAccuracyAsync(db, cancellationToken);

        var contexts = periods
            .Select(p => new PeriodContext(p, p.Rules(minAccuracy), holidays))
            .ToList();

        var byGroup = new Dictionary<Guid, PeriodContext>();
        foreach (var ctx in contexts.OrderBy(c => c.Period.Contains(today) ? 0 : 1).ThenByDescending(c => c.Period.StartDate))
        {
            foreach (var group in ctx.Period.Groups)
                byGroup.TryAdd(group.StudentGroupId, ctx);
        }

        return new PeriodLookup(byGroup, contexts);
    }

    /// <summary>Bitta davr (id bo'yicha, faolligidan qat'i nazar) — ruxsat tasdiqlashda kerak.</summary>
    public static async Task<PeriodContext?> LoadPeriodAsync(IApplicationDbContext db, Guid periodId, CancellationToken cancellationToken)
    {
        var period = await db.PracticePeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);
        if (period is null)
            return null;

        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var minAccuracy = await LoadMinAccuracyAsync(db, cancellationToken);
        return new PeriodContext(period, period.Rules(minAccuracy), holidays);
    }

    private static async Task<double> LoadMinAccuracyAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var raw = await db.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == SettingKeys.MinGpsAccuracy)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : CheckInRules.Default.MinAccuracyM;
    }
}
