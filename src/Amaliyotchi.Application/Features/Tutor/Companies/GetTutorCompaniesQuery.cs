using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary>Tyutor ko'radigan korxona qatori. <paramref name="Students"/> — faqat ko'lamdagi AKTIV talabalar,
/// <paramref name="TotalStudents"/> — butun tizim bo'yicha aktiv talabalar (STIR nazorati shu songa tayanadi, shu bois
/// <paramref name="OverLimit"/> ham undan hisoblanadi). <paramref name="AttendancePct"/> — ko'lamdagi
/// talabalarning jamlangan davomati: ∑kelgan / ∑hisobga olingan kun × 100 (1 kasr).</summary>
public sealed record TutorCompany(
    Guid Id,
    string Name,
    string Tin,
    string Address,
    double Lat,
    double Lng,
    int RadiusM,
    int Students,
    int TotalStudents,
    int MaxStudents,
    bool OverLimit,
    double AttendancePct,
    int SuspiciousDays,
    CompanyFlag? Flag);

/// <summary><c>GET /api/tutor/companies</c> — ko'lamdagi talabalar HOZIR aktiv amaliyot o'tayotgan korxonalar
/// (§4.7 aktiv korxona qoidasi), nom bo'yicha. Ko'lamda bitta ham aktiv talabasi bo'lmagan korxona ro'yxatga kirmaydi
/// (yopilgan/tugagan/kelgusi davr arizalari hisobga olinmaydi).</summary>
public sealed record GetTutorCompaniesQuery : IRequest<IReadOnlyList<TutorCompany>>;

internal sealed class GetTutorCompaniesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorCompaniesQuery, IReadOnlyList<TutorCompany>>
{
    public async Task<IReadOnlyList<TutorCompany>> Handle(GetTutorCompaniesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        // Faqat AKTIV joylashuvlar (§4.7): ko'lamdagi talaba hozir shu korxonada, ochiq davom etayotgan davrda.
        var placements = await CompanyQueries.LoadActivePlacementsAsync(db, scope, companyIds: null, today, cancellationToken);
        if (placements.Count == 0)
            return [];

        var companyIds = placements.Select(p => p.CompanyId).Distinct().ToArray();
        var studentIds = placements.Select(p => p.StudentUserId).Distinct().ToArray();

        // O'chirilgan korxonalar global filtr bilan tushib qoladi — shuning uchun ro'yxat shu so'rovdan quriladi.
        var companies = await db.Companies
            .AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Tin,
                c.Address,
                c.Location.Latitude,
                c.Location.Longitude,
                c.RadiusM
            })
            .ToListAsync(cancellationToken);

        if (companies.Count == 0)
            return [];

        // Har talabaning bitta aktiv joylashuvi bor — korxona statistikasi uning aktiv davri bo'yicha.
        var placementsByCompany = placements
            .GroupBy(p => p.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(p => (p.StudentUserId, p.PeriodId)).ToList());

        // STIR nazorati — butun tizim bo'yicha aktiv talabalar.
        var totals = (await CompanyQueries.LoadActivePlacementsAsync(db, scope: null, companyIds, today, cancellationToken))
            .GroupBy(p => p.CompanyId)
            .ToDictionary(g => g.Key, g => g.Count());

        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var periodIds = placements.Select(p => p.PeriodId).Distinct().ToArray();

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => studentIds.Contains(a.StudentUserId) && periodIds.Contains(a.PeriodId))
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.Status == LeaveRequestStatus.Approved
                    && studentIds.Contains(l.StudentUserId) && periodIds.Contains(l.PeriodId))
                .Select(l => new { l.StudentUserId, l.PeriodId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => (l.StudentUserId, l.PeriodId), l => (l.DateFrom, l.DateTo));

        var suspiciousByCompany = await CompanyQueries.CountSuspiciousDaysAsync(db, placements, cancellationToken);

        var stats = placements
            .Select(p => (p.StudentUserId, p.PeriodId))
            .Distinct()
            .ToDictionary(key => key, key => StudentStatsCalculator.ComputeAttendance(
                periods.ForPeriod(key.PeriodId),
                attendance[key.StudentUserId].ToList(),
                leaves[key].ToList(),
                today,
                localNow));

        var maxStudents = await CompanyQueries.LoadMaxStudentsAsync(db, cancellationToken);

        return companies
            .Select(c =>
            {
                var placements = placementsByCompany.GetValueOrDefault(c.Id) ?? [];
                var attended = placements.Sum(key => stats[key].AttendedDays);
                var countable = placements.Sum(key => stats[key].TotalDays);
                var pct = countable == 0 ? 0 : Math.Round(attended * 100d / countable, 1, MidpointRounding.AwayFromZero);
                var totalStudents = totals.GetValueOrDefault(c.Id);
                var overLimit = totalStudents > maxStudents;
                var suspiciousDays = suspiciousByCompany.GetValueOrDefault(c.Id);

                return new TutorCompany(
                    c.Id, c.Name, c.Tin, c.Address, c.Latitude, c.Longitude, c.RadiusM,
                    placements.Count, totalStudents, maxStudents, overLimit, pct, suspiciousDays,
                    CompanyFlags.Resolve(suspiciousDays, c.RadiusM, overLimit));
            })
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .ToList();
    }
}
