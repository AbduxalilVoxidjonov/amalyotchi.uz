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

/// <summary>Guruh → amaliyot davrlari xaritasi. Davrlar oz (o'nlab), shuning uchun hammasi (o'chirilmaganlari,
/// yopilganlari ham — tarix va statistika uchun) bir marta yuklanadi. Guruhning qaysi davri kerakligi
/// <see cref="PeriodSelection"/> qoidasi bilan tanlanadi: <see cref="ForGroup(Guid)"/> — sukut bo'yicha
/// (davom etayotgan → oxirgi tugagan → eng yaqin kelgusi).</summary>
public sealed class PeriodLookup
{
    private readonly Dictionary<Guid, List<PeriodContext>> _byGroup;
    private readonly Dictionary<Guid, PeriodContext?> _defaults = [];

    private PeriodLookup(DateOnly today, Dictionary<Guid, List<PeriodContext>> byGroup, IReadOnlyList<PeriodContext> all)
    {
        Today = today;
        _byGroup = byGroup;
        All = all;
    }

    public DateOnly Today { get; }

    public IReadOnlyList<PeriodContext> All { get; }

    /// <summary>Kamida bitta davrga biriktirilgan guruhlar.</summary>
    public IReadOnlyCollection<Guid> GroupIds => _byGroup.Keys;

    /// <summary>Guruhning sukut bo'yicha davri (<see cref="PeriodPurpose.Default"/>).</summary>
    public PeriodContext? ForGroup(Guid groupId)
    {
        if (!_defaults.TryGetValue(groupId, out var ctx))
        {
            ctx = ForGroup(groupId, PeriodPurpose.Default);
            _defaults[groupId] = ctx;
        }

        return ctx;
    }

    public PeriodContext? ForGroup(Guid groupId, PeriodPurpose purpose)
        => _byGroup.TryGetValue(groupId, out var list)
            ? PeriodSelection.Select(list, Today, purpose, c => PeriodSpan.Of(c.Period))
            : null;

    /// <summary>Kalendar katagi uchun: <paramref name="date"/> ni o'z ichiga olgan guruh davri (yopilgani ham — tarix),
    /// bo'lmasa sukut bo'yicha davr.</summary>
    public PeriodContext? ForGroupOn(Guid groupId, DateOnly date)
        => (_byGroup.TryGetValue(groupId, out var list) ? list.FirstOrDefault(c => c.Period.Contains(date)) : null)
           ?? ForGroup(groupId);

    /// <summary>Berilgan guruhlarning sukut bo'yicha davrlari id'lari — so'rovlarni oldindan toraytirish uchun.
    /// Natijani talaba bo'yicha albatta <c>ForGroup(groupId).Period.Id</c> bilan ham filtrlash kerak.</summary>
    public IReadOnlyCollection<Guid> DefaultPeriodIds(IEnumerable<Guid> groupIds)
        => groupIds.Distinct()
            .Select(ForGroup)
            .Where(c => c is not null)
            .Select(c => c!.Period.Id)
            .Distinct()
            .ToArray();

    public PeriodContext? ForPeriod(Guid periodId) => All.FirstOrDefault(p => p.Period.Id == periodId);

    public static async Task<PeriodLookup> LoadAsync(IApplicationDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var periods = await db.PracticePeriods
            .AsNoTracking()
            .Include(p => p.Groups)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var minAccuracy = await LoadMinAccuracyAsync(db, cancellationToken);

        var contexts = periods
            .Select(p => new PeriodContext(p, p.Rules(minAccuracy), holidays))
            .ToList();

        var byGroup = new Dictionary<Guid, List<PeriodContext>>();
        foreach (var ctx in contexts)
        {
            foreach (var group in ctx.Period.Groups)
            {
                if (!byGroup.TryGetValue(group.StudentGroupId, out var list))
                    byGroup[group.StudentGroupId] = list = [];
                list.Add(ctx);
            }
        }

        return new PeriodLookup(today, byGroup, contexts);
    }

    /// <summary>Tayyor entity uchun kontekst (qoidalar + bayramlar).</summary>
    public static async Task<PeriodContext> ContextAsync(IApplicationDbContext db, PracticePeriod period, CancellationToken cancellationToken)
    {
        var holidays = await db.Holidays.AsNoTracking().ToListAsync(cancellationToken);
        var minAccuracy = await LoadMinAccuracyAsync(db, cancellationToken);
        return new PeriodContext(period, period.Rules(minAccuracy), holidays);
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
