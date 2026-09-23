using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Companies;

/// <summary>Tyutor ko'radigan korxona qatori. <paramref name="Students"/> — faqat ko'lamdagi talabalar,
/// <paramref name="TotalStudents"/> — butun tizim bo'yicha (STIR nazorati shu songa tayanadi, shu bois
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

/// <summary><c>GET /api/tutor/companies</c> — ko'lamdagi talabalar biriktirilgan korxonalar, nom bo'yicha.
/// Ko'lamda bitta ham tasdiqlangan arizasi bo'lmagan korxona ro'yxatga kirmaydi.</summary>
public sealed record GetTutorCompaniesQuery : IRequest<IReadOnlyList<TutorCompany>>;

internal sealed class GetTutorCompaniesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorCompaniesQuery, IReadOnlyList<TutorCompany>>
{
    public async Task<IReadOnlyList<TutorCompany>> Handle(GetTutorCompaniesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var approved = await db.PracticeApplications
            .AsNoTracking()
            .InScope(scope)
            .Where(a => a.Status == ApplicationStatus.Approved)
            .Select(a => new { a.CompanyId, a.StudentUserId, a.PeriodId })
            .ToListAsync(cancellationToken);

        if (approved.Count == 0)
            return [];

        var companyIds = approved.Select(a => a.CompanyId).Distinct().ToList();
        var studentIds = approved.Select(a => a.StudentUserId).Distinct().ToList();

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

        var scopedByCompany = approved
            .GroupBy(a => a.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StudentUserId).Distinct().ToList());

        // Korxona statistikasi — shu korxonadagi arizaning O'Z davri bo'yicha (talaba kuzda bir korxonada,
        // bahorda boshqasida bo'lishi mumkin).
        var placementsByCompany = approved
            .GroupBy(a => a.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(a => (a.StudentUserId, a.PeriodId)).Distinct().ToList());

        // (talaba, davr) → korxona: shubhali kunlarni korxonaga bog'lash uchun.
        var companyByStudentPeriod = approved
            .GroupBy(a => (a.StudentUserId, a.PeriodId))
            .ToDictionary(g => g.Key, g => g.First().CompanyId);

        var totals = (await db.PracticeApplications
                .AsNoTracking()
                .Where(a => a.Status == ApplicationStatus.Approved && companyIds.Contains(a.CompanyId))
                .Select(a => new { a.CompanyId, a.StudentUserId })
                .ToListAsync(cancellationToken))
            .GroupBy(a => a.CompanyId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.StudentUserId).Distinct().Count());

        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var periodIds = approved.Select(a => a.PeriodId).Distinct().ToList();

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

        var suspiciousByCompany = new Dictionary<Guid, int>();
        var suspiciousRows = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(d => d.IsSuspicious && studentIds.Contains(d.StudentUserId))
            .Select(d => new { d.StudentUserId, d.PeriodId })
            .ToListAsync(cancellationToken);
        foreach (var row in suspiciousRows)
        {
            if (companyByStudentPeriod.TryGetValue((row.StudentUserId, row.PeriodId), out var companyId))
                suspiciousByCompany[companyId] = suspiciousByCompany.GetValueOrDefault(companyId) + 1;
        }

        var stats = approved
            .Select(a => (a.StudentUserId, a.PeriodId))
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
                var scoped = scopedByCompany.GetValueOrDefault(c.Id) ?? [];
                var placements = placementsByCompany.GetValueOrDefault(c.Id) ?? [];
                var attended = placements.Sum(key => stats[key].AttendedDays);
                var countable = placements.Sum(key => stats[key].TotalDays);
                var pct = countable == 0 ? 0 : Math.Round(attended * 100d / countable, 1, MidpointRounding.AwayFromZero);
                var totalStudents = totals.GetValueOrDefault(c.Id);
                var overLimit = totalStudents > maxStudents;
                var suspiciousDays = suspiciousByCompany.GetValueOrDefault(c.Id);

                return new TutorCompany(
                    c.Id, c.Name, c.Tin, c.Address, c.Latitude, c.Longitude, c.RadiusM,
                    scoped.Count, totalStudents, maxStudents, overLimit, pct, suspiciousDays,
                    CompanyFlags.Resolve(suspiciousDays, c.RadiusM, overLimit));
            })
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ThenBy(c => c.Id)
            .ToList();
    }
}
