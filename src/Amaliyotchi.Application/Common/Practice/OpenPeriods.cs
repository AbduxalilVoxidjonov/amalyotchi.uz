using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Application.Common.Practice;

/// <summary>"Ochiq davr" yagona qoidasi — ariza navbatlari (kutilayotgan, kechikkan) va shartnoma holatlari shu davrlar
/// bo'yicha sanaladi. Ochiq davr: o'chirilmagan (global filtr), yopilmagan (<c>Status != Closed</c>) va tugamagan
/// (<c>EndDate &gt;= today</c>) — ya'ni bugun davom etayotgan yoki kelgusi davr. Tugagan davr (<c>Closed</c> YOKI
/// <c>EndDate &lt; today</c>; sanasi tugamagan bo'lsa ham yopilgan davr tugagan hisoblanadi) — tarix, joriy
/// ko'rsatkichlarga kirmaydi (<see cref="PeriodSelection"/>, kundalik qoidasi bilan bir xil).</summary>
internal static class OpenPeriodQueries
{
    /// <summary>Bugun (<paramref name="today"/>, Toshkent) ochiq davrlar — IQueryable, boshqa so'rovlarga subquery sifatida.</summary>
    public static IQueryable<PracticePeriod> OpenPeriods(this IApplicationDbContext db, DateOnly today)
        => db.PracticePeriods.Where(p => p.Status != PracticePeriodStatus.Closed && p.EndDate >= today);

    /// <summary>Ochiq davrlardagi arizalar (holat filtri chaqiruvchida). Talaba profili o'chirilgan arizalar kirmaydi.
    /// <c>Transferred</c> — tarix: chaqiruvchi faqat aniq holatlarni (<c>Submitted</c>, <c>Approved</c> …) sanaydi.</summary>
    public static IQueryable<PracticeApplication> OpenPeriodApplications(this IApplicationDbContext db, DateOnly today)
    {
        var open = db.OpenPeriods(today);
        return db.PracticeApplications.Where(a =>
            open.Any(p => p.Id == a.PeriodId)
            && db.StudentProfiles.Any(s => s.UserId == a.StudentUserId));
    }
}
