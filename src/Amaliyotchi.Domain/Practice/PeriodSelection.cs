namespace Amaliyotchi.Domain.Practice;

/// <summary>Davr tanlash maqsadi — bitta guruhda bir o'quv yilida bir nechta (kesishmaydigan) davr bo'lishi mumkin
/// (masalan kuzgi va bahorgi), shuning uchun "qaysi davr" savoli sirtga bog'liq. Qoida — <see cref="PeriodSelection"/>.</summary>
public enum PeriodPurpose
{
    /// <summary>Faqat davom etayotgan davr (check-in/check-out, kundalik yozish).</summary>
    Ongoing = 1,

    /// <summary>Statistika va profilning sukut bo'yicha davri: davom etayotgan → oxirgi tugagan → eng yaqin kelgusi.</summary>
    Default = 2,

    /// <summary>Amaliyot joyiga ariza: davom etayotgan → eng yaqin kelgusi (talaba bahorgi davrga oldindan ariza beradi).</summary>
    Enrollment = 3,

    /// <summary>Talaba ilovasidagi "bugun"/"joy" ko'rinishi: davom etayotgan → eng yaqin kelgusi → oxirgi tugagan
    /// (ikki davr oralig'ida "davr hali boshlanmagan: …" ko'rsatish uchun).</summary>
    Current = 4
}

/// <summary>Davrning tanlash uchun kerakli qismi (entity yoki so'rov proyeksiyasidan).</summary>
public readonly record struct PeriodSpan(DateOnly StartDate, DateOnly EndDate, bool IsClosed)
{
    public static PeriodSpan Of(PracticePeriod period) =>
        new(period.StartDate, period.EndDate, period.Status == PracticePeriodStatus.Closed);
}

/// <summary>Yagona davr tanlash qoidasi. Guruh davrlari (o'chirilmaganlari, yopilganlari ham) va sana D (Toshkent) uchun:
/// <list type="bullet">
/// <item><b>ongoing</b> — D ni o'z ichiga olgan, yopilmagan davr (kesishish taqiqlangan — ko'pi bilan bitta);</item>
/// <item><b>lastEnded</b> — tugagan davrlardan eng so'nggisi (<c>EndDate</c> bo'yicha): <c>EndDate &lt; D</c>, yoki
/// muddatidan oldin yopilgan (<c>Closed</c>, <c>StartDate &lt;= D</c>) — yopilganlari ham kiradi;</item>
/// <item><b>upcoming</b> — <c>StartDate &gt; D</c> bo'lgan eng yaqin yopilmagan davr.</item>
/// </list>
/// Sirtlar <see cref="PeriodPurpose"/> orqali tartibni tanlaydi. Barcha iste'molchilar (tyutor/admin statistikasi,
/// talaba ilovasi, check-in, ariza) shu yerdan foydalanadi.</summary>
public static class PeriodSelection
{
    public static T? Ongoing<T>(IEnumerable<T> periods, DateOnly date, Func<T, PeriodSpan> span) where T : class
        => periods.FirstOrDefault(p =>
        {
            var s = span(p);
            return !s.IsClosed && s.StartDate <= date && s.EndDate >= date;
        });

    public static T? LastEnded<T>(IEnumerable<T> periods, DateOnly date, Func<T, PeriodSpan> span) where T : class
        => periods
            .Where(p =>
            {
                var s = span(p);
                return s.EndDate < date || (s.IsClosed && s.StartDate <= date);
            })
            .OrderByDescending(p => span(p).EndDate)
            .ThenByDescending(p => span(p).StartDate)
            .FirstOrDefault();

    public static T? Upcoming<T>(IEnumerable<T> periods, DateOnly date, Func<T, PeriodSpan> span) where T : class
        => periods
            .Where(p =>
            {
                var s = span(p);
                return !s.IsClosed && s.StartDate > date;
            })
            .OrderBy(p => span(p).StartDate)
            .FirstOrDefault();

    public static T? Select<T>(IEnumerable<T> periods, DateOnly date, PeriodPurpose purpose, Func<T, PeriodSpan> span)
        where T : class
    {
        var list = periods as IReadOnlyCollection<T> ?? periods.ToList();
        return purpose switch
        {
            PeriodPurpose.Ongoing => Ongoing(list, date, span),
            PeriodPurpose.Default => Ongoing(list, date, span) ?? LastEnded(list, date, span) ?? Upcoming(list, date, span),
            PeriodPurpose.Enrollment => Ongoing(list, date, span) ?? Upcoming(list, date, span),
            PeriodPurpose.Current => Ongoing(list, date, span) ?? Upcoming(list, date, span) ?? LastEnded(list, date, span),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
        };
    }

    public static PracticePeriod? Select(IEnumerable<PracticePeriod> periods, DateOnly date, PeriodPurpose purpose)
        => Select(periods, date, purpose, PeriodSpan.Of);

    /// <summary>Davr <paramref name="date"/> kuni davom etayaptimi (yopilmagan va sanalar ichida).</summary>
    public static bool IsOngoing(PracticePeriod period, DateOnly date)
        => period.Status != PracticePeriodStatus.Closed && period.Contains(date);
}
