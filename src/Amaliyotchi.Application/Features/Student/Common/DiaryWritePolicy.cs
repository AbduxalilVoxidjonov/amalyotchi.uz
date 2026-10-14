using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Talaba kundalik yozuvini yoza oladimi — <c>POST /api/student/diary</c>, <c>GET /api/student/today</c>
/// (<c>canWriteDiary</c>/<c>diaryBlockedReason</c>) va <c>GET /api/student/period-days</c> (<c>canSubmitDiary</c>) uchun
/// yagona qoida; hisob — <see cref="DiaryDayPolicy"/> (talabaning joriy guruh davrlari va bayramlar bilan).
/// <list type="bullet">
/// <item><see cref="Evaluate"/> — eski "bugun" yo'li (<c>date</c> berilmagan POST va <c>canWriteDiary</c>): davr ochiq va
/// bugunni o'z ichiga oladi, bugungi yozuv yo'q yoki <c>rewrite</c>; ish kuni sharti qo'yilmaydi (orqaga moslik).</item>
/// <item><see cref="EvaluateDay"/> — aniq kun (<c>date</c> berilgan POST va <c>canSubmitDiary</c>): qo'shimcha ravishda
/// kun ≤ bugun va ish kuni.</item>
/// </list>
/// Davr yakunlangan bo'lsa yozuvni qayta yozish ham taqiqlanadi.</summary>
internal static class DiaryWritePolicy
{
    public const string PeriodEndedMessage = DiaryDayPolicy.PeriodEndedMessage;
    public const string PeriodNotStartedMessage = DiaryDayPolicy.PeriodNotStartedMessage;
    public const string NoPeriodMessage = DiaryDayPolicy.NoPeriodMessage;
    public const string AlreadySubmittedMessage = DiaryDayPolicy.AlreadySubmittedTodayMessage;

    /// <summary>Bugungi kun uchun to'liq qaror (ish kuni shartisiz). <paramref name="todayEntryStatus"/> — bugungi yozuv holati.</summary>
    public static (bool CanWrite, string? Reason) Evaluate(StudentPractice practice, DiaryStatus? todayEntryStatus)
    {
        var decision = Decide(practice, practice.Today, todayEntryStatus, requireWorkDay: false);
        return (decision.CanWrite, decision.Reason);
    }

    /// <summary>Berilgan kun uchun to'liq qaror (kelajak, davr, ish kuni, mavjud yozuv).</summary>
    public static DiaryDayDecision EvaluateDay(StudentPractice practice, DateOnly date, DiaryStatus? entryStatus)
        => Decide(practice, date, entryStatus, requireWorkDay: true);

    /// <summary>Bugun davom etayotgan (yopilmagan, sanalar ichida) davr.</summary>
    public static PracticePeriod? OngoingPeriod(StudentPractice practice)
        => DiaryDayPolicy.OngoingPeriod(practice.Periods.GroupPeriods, practice.Today);

    /// <summary>POST (eski yo'l) uchun: davom etayotgan davrni qaytaradi yoki 400 (<see cref="DomainException"/>) tashlaydi.</summary>
    public static PracticePeriod RequireOngoingPeriod(StudentPractice practice)
        => OngoingPeriod(practice)
           ?? throw new DomainException(
               DiaryDayPolicy.PeriodBlockReason(practice.Periods.GroupPeriods, practice.Today) ?? NoPeriodMessage);

    private static DiaryDayDecision Decide(StudentPractice practice, DateOnly date, DiaryStatus? status, bool requireWorkDay)
        => DiaryDayPolicy.Evaluate(practice.Periods.GroupPeriods, practice.IsHoliday, practice.Today, date, status, requireWorkDay);
}
