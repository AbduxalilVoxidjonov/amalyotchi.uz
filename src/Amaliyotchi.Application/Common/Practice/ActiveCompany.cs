using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Common.Practice;

/// <summary>Talabaning HOZIR amaliyot o'tayotgan korxonasi (tanlangan davrdan mustaqil) — JSON'da
/// <c>activeCompany: { id, name, periodId, periodName } | null</c>. Qoida — <see cref="ActiveCompanyQueries"/>.</summary>
public sealed record ActiveCompanyRef(Guid Id, string Name, Guid PeriodId, string PeriodName);

/// <summary>"Aktiv korxona" yagona qoidasi (ro'yxatlardagi <c>company</c> ustuni va profildagi <c>activeCompany</c>).
/// Talabaning arizasi quyidagilarning HAMMASIga mos bo'lishi kerak:
/// <list type="bullet">
/// <item><c>Status == Approved</c> (o'tkazilgan/rad etilgan/kutilayotgan/yakunlangan emas);</item>
/// <item>davr o'chirilmagan (global filtr) va yopilmagan (<c>Status != Closed</c>);</item>
/// <item>bugun (Toshkent) <c>StartDate &lt;= today &lt;= EndDate</c>;</item>
/// <item>talabaning joriy guruhi hali shu davrga biriktirilgan.</item>
/// </list>
/// Bir nechta mos kelsa — eng so'nggi <c>DecidedAt</c>. Mos ariza bo'lmasa — <c>null</c> (oldingi korxonaga
/// fallback YO'Q). Hammasi bitta SQL so'rovga tarjima qilinadi (N+1 yo'q).</summary>
internal static class ActiveCompanyQueries
{
    /// <summary>Aktiv (bugun amalda bo'lgan) tasdiqlangan arizalar — IQueryable, qo'shimcha filtr/proyeksiya uchun.</summary>
    public static IQueryable<PracticeApplication> ActiveApplications(this IApplicationDbContext db, DateOnly today)
        => db.PracticeApplications.Where(a =>
            a.Status == ApplicationStatus.Approved
            && db.PracticePeriods.Any(p =>
                p.Id == a.PeriodId
                && p.Status != PracticePeriodStatus.Closed
                && p.StartDate <= today
                && p.EndDate >= today
                && p.Groups.Any(g => db.StudentProfiles.Any(s =>
                    s.UserId == a.StudentUserId && s.StudentGroupId == g.StudentGroupId))));

    /// <summary>Talabalar bo'yicha aktiv korxona (lug'atda yo'q talaba — aktiv korxonasi yo'q). Bitta so'rov.</summary>
    public static async Task<IReadOnlyDictionary<Guid, ActiveCompanyRef>> LoadActiveCompaniesAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> studentIds, DateOnly today, CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
            return new Dictionary<Guid, ActiveCompanyRef>();

        var rows = await (from a in db.ActiveApplications(today).AsNoTracking()
                          where studentIds.Contains(a.StudentUserId)
                          join p in db.PracticePeriods on a.PeriodId equals p.Id
                          select new
                          {
                              a.StudentUserId,
                              a.DecidedAt,
                              a.SubmittedAt,
                              CompanyId = a.Company.Id,
                              CompanyName = a.Company.Name,
                              PeriodId = p.Id,
                              PeriodName = p.Name
                          })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.StudentUserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var r = g.OrderByDescending(x => x.DecidedAt).ThenByDescending(x => x.SubmittedAt).First();
                    return new ActiveCompanyRef(r.CompanyId, r.CompanyName, r.PeriodId, r.PeriodName);
                });
    }

    /// <summary>Bitta talabaning aktiv korxonasi yoki <c>null</c>.</summary>
    public static async Task<ActiveCompanyRef?> LoadActiveCompanyAsync(
        this IApplicationDbContext db, Guid studentUserId, DateOnly today, CancellationToken cancellationToken)
    {
        var map = await db.LoadActiveCompaniesAsync([studentUserId], today, cancellationToken);
        return map.GetValueOrDefault(studentUserId);
    }
}
