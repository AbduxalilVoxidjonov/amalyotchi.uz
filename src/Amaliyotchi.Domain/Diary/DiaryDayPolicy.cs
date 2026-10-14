using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Domain.Diary;

/// <summary>Kundalik yozuvini rad etish sababi turi (<see cref="DiaryDayPolicy"/>).</summary>
public enum DiaryDayBlock
{
    None = 0,

    /// <summary>Kun bugundan keyin.</summary>
    Future = 1,

    /// <summary>Kun hech qaysi ochiq (yopilmagan) guruh davriga kirmaydi.</summary>
    Period = 2,

    /// <summary>Kun davr bo'yicha ish kuni emas (dam olish kuni yoki bayram).</summary>
    NotWorkDay = 3,

    /// <summary>Shu kunga yozuv bor va u <c>rewrite</c> holatida emas (→ 409).</summary>
    AlreadySubmitted = 4
}

/// <summary>Bitta kun uchun qaror: yozish mumkinmi, yozuv qaysi davrga bog'lanadi, aks holda sabab.</summary>
public readonly record struct DiaryDayDecision(PracticePeriod? Period, DiaryDayBlock Block, string? Reason)
{
    public bool CanWrite => Block == DiaryDayBlock.None;
}

/// <summary>Kundalik yozuvini <b>berilgan kun</b> uchun yuborish qoidasi — <c>POST /api/student/diary</c>
/// (<c>date</c> bilan yoki bugun), <c>GET /api/student/today</c> (<c>canWriteDiary</c>) va
/// <c>GET /api/student/period-days</c> (<c>canSubmitDiary</c>) uchun yagona manba. Tartib:
/// <list type="number">
/// <item>kun ≤ bugun (Toshkent), aks holda <see cref="FutureMessage"/>;</item>
/// <item>kun joriy guruhning ochiq (yopilmagan) davri ichida (<see cref="PeriodSelection.Ongoing{T}"/>), aks holda
/// <see cref="PeriodBlockReason"/>;</item>
/// <item>kun shu davr bo'yicha ish kuni (<see cref="PracticePeriod.IsWorkDay"/> — hafta kuni va bayram emas), aks holda
/// <see cref="NotWorkDayMessage"/> (<paramref name="requireWorkDay"/> = false — eski "bugun" yo'li, tekshirilmaydi);</item>
/// <item>shu kunga yozuv yo'q yoki <c>rewrite</c> holatida, aks holda "allaqachon yuborilgan".</item>
/// </list></summary>
public static class DiaryDayPolicy
{
    public const string FutureMessage = "Kelajakdagi kun uchun hisobot yuborib bo'lmaydi.";
    public const string NotWorkDayMessage = "Dam olish kuni uchun hisobot yuborilmaydi.";
    public const string PeriodEndedMessage = "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.";
    public const string PeriodNotStartedMessage = "Amaliyot davri hali boshlanmagan.";
    public const string NoPeriodMessage = "Faol amaliyot davri yo'q — hisobot yozib bo'lmaydi.";

    /// <summary>Bugungi kun uchun (eski matn — TWA bilan moslik).</summary>
    public const string AlreadySubmittedTodayMessage = "Bugungi hisobot allaqachon yuborilgan.";

    /// <summary>O'tgan kun uchun.</summary>
    public const string AlreadySubmittedDayMessage = "Bu kun uchun hisobot allaqachon yuborilgan.";

    /// <summary><paramref name="date"/> ni o'z ichiga olgan ochiq (yopilmagan) davr — yozuv shu davrga bog'lanadi.</summary>
    public static PracticePeriod? OngoingPeriod(IReadOnlyCollection<PracticePeriod> groupPeriods, DateOnly date)
        => PeriodSelection.Ongoing(groupPeriods, date, PeriodSpan.Of);

    /// <summary>Davr bo'yicha rad etish sababi; <paramref name="date"/> ochiq davr ichida bo'lsa <c>null</c>.
    /// Tartib: kunni o'z ichiga olgan yopilgan davr → yakunlangan; kelgusi davr → hali boshlanmagan;
    /// tugagan/yopilgan davr → yakunlangan; davr umuman yo'q → <see cref="NoPeriodMessage"/>.</summary>
    public static string? PeriodBlockReason(IReadOnlyCollection<PracticePeriod> groupPeriods, DateOnly date)
    {
        if (OngoingPeriod(groupPeriods, date) is not null)
            return null;
        if (groupPeriods.Count == 0)
            return NoPeriodMessage;
        if (groupPeriods.Any(p => p.Status == PracticePeriodStatus.Closed && p.Contains(date)))
            return PeriodEndedMessage;
        if (PeriodSelection.Upcoming(groupPeriods, date, PeriodSpan.Of) is not null)
            return PeriodNotStartedMessage;
        // Tugagan yoki yopilgan (boshlanmasdan yopilgani ham) — yakunlangan.
        return PeriodEndedMessage;
    }

    /// <param name="groupPeriods">Talabaning joriy guruhi davrlari (yopilganlari ham).</param>
    /// <param name="isHoliday">Kun bayrammi.</param>
    /// <param name="today">Bugun (Toshkent).</param>
    /// <param name="date">Hisobot kuni.</param>
    /// <param name="existingStatus">Shu kungi yozuv holati (yo'q bo'lsa <c>null</c>).</param>
    /// <param name="requireWorkDay">Ish kuni shartini tekshirish (<c>date</c> aniq berilganda va period-days'da — true).</param>
    public static DiaryDayDecision Evaluate(
        IReadOnlyCollection<PracticePeriod> groupPeriods,
        Func<DateOnly, bool> isHoliday,
        DateOnly today,
        DateOnly date,
        DiaryStatus? existingStatus,
        bool requireWorkDay = true)
    {
        if (date > today)
            return new(null, DiaryDayBlock.Future, FutureMessage);

        var period = OngoingPeriod(groupPeriods, date);
        if (period is null)
            return new(null, DiaryDayBlock.Period, PeriodBlockReason(groupPeriods, date) ?? NoPeriodMessage);

        if (requireWorkDay && !period.IsWorkDay(date, isHoliday(date)))
            return new(period, DiaryDayBlock.NotWorkDay, NotWorkDayMessage);

        if (existingStatus is { } status && status != DiaryStatus.Rewrite)
            return new(period, DiaryDayBlock.AlreadySubmitted,
                date == today ? AlreadySubmittedTodayMessage : AlreadySubmittedDayMessage);

        return new(period, DiaryDayBlock.None, null);
    }

    /// <summary>Kech yuborilgan: <paramref name="submittedAt"/> ning Toshkent sanasi hisobot kunidan keyin.</summary>
    public static bool IsLate(DateOnly date, DateTimeOffset submittedAt)
        => PracticeTime.LocalDate(submittedAt) > date;
}
