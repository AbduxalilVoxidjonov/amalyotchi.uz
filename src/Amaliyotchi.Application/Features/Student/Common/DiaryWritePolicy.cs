using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Talaba bugun kundalik yozuvi yoza oladimi — <c>POST /api/student/diary</c> va
/// <c>GET /api/student/today</c> (<c>canWriteDiary</c>/<c>diaryBlockedReason</c>) uchun yagona qoida.
/// Yozish mumkin ⇔ joriy guruh davrlaridan biri ochiq (yopilmagan) va bugunni o'z ichiga oladi
/// (<see cref="PeriodSelection.Ongoing{T}"/>) <b>va</b> bugungi yozuv yo'q yoki <c>rewrite</c> holatida.
/// Davr yakunlangan bo'lsa bugungi yozuvni qayta yozish ham taqiqlanadi.</summary>
internal static class DiaryWritePolicy
{
    public const string PeriodEndedMessage = "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.";
    public const string PeriodNotStartedMessage = "Amaliyot davri hali boshlanmagan.";
    public const string NoPeriodMessage = "Faol amaliyot davri yo'q — hisobot yozib bo'lmaydi.";
    public const string AlreadySubmittedMessage = "Bugungi hisobot allaqachon yuborilgan.";

    /// <summary>Bugun davom etayotgan (yopilmagan, sanalar ichida) davr — yozuv shu davrga bog'lanadi.</summary>
    public static PracticePeriod? OngoingPeriod(StudentPractice practice)
        => PeriodSelection.Ongoing(practice.Periods.GroupPeriods, practice.Today, PeriodSpan.Of);

    /// <summary>Davr bo'yicha rad etish sababi; davom etayotgan davr bo'lsa <c>null</c>.
    /// Tartib: bugunni o'z ichiga olgan yopilgan davr → yakunlangan; kelgusi davr → hali boshlanmagan;
    /// tugagan/yopilgan davr → yakunlangan; davr umuman yo'q → <see cref="NoPeriodMessage"/>.</summary>
    public static string? PeriodBlockReason(StudentPractice practice)
    {
        var periods = practice.Periods.GroupPeriods;
        var today = practice.Today;

        if (OngoingPeriod(practice) is not null)
            return null;
        if (periods.Count == 0)
            return NoPeriodMessage;
        if (periods.Any(p => p.Status == PracticePeriodStatus.Closed && p.Contains(today)))
            return PeriodEndedMessage;
        if (PeriodSelection.Upcoming(periods, today, PeriodSpan.Of) is not null)
            return PeriodNotStartedMessage;
        // Tugagan yoki yopilgan (boshlanmasdan yopilgani ham) — yakunlangan.
        return PeriodEndedMessage;
    }

    /// <summary>To'liq qaror: davr qoidasi, keyin bugungi yozuv holati (yuborilgan va <c>rewrite</c> emas → 409 bo'ladi).
    /// <paramref name="todayEntryStatus"/> — talabaning bugungi yozuvi holati (yo'q bo'lsa <c>null</c>).</summary>
    public static (bool CanWrite, string? Reason) Evaluate(StudentPractice practice, DiaryStatus? todayEntryStatus)
    {
        if (PeriodBlockReason(practice) is { } reason)
            return (false, reason);
        if (todayEntryStatus is { } status && status != DiaryStatus.Rewrite)
            return (false, AlreadySubmittedMessage);
        return (true, null);
    }

    /// <summary>POST uchun: davom etayotgan davrni qaytaradi yoki 400 (<see cref="DomainException"/>) tashlaydi.</summary>
    public static PracticePeriod RequireOngoingPeriod(StudentPractice practice)
        => OngoingPeriod(practice)
           ?? throw new DomainException(PeriodBlockReason(practice) ?? NoPeriodMessage);
}
