using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Common.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Companies;

/// <summary>Kontrakt v2 <c>Company</c>: STIR xom (9 raqam), <paramref name="Students"/> — korxonada HOZIR aktiv amaliyot
/// o'tayotgan talabalar (§4.7 aktiv korxona qoidasi), <paramref name="SuspiciousDays"/> — shu talabalarning aktiv davrdagi
/// shubhali davomat kunlari.
/// <paramref name="MaxStudents"/> — <c>maxStudentsPerCompany</c> sozlamasi, <paramref name="OverLimit"/> — chegaradan oshgani.
/// Bayroq ustuvorligi: <c>suspicious</c> → <c>tooManyStudents</c> → <c>largeRadius</c> → <c>null</c>.</summary>
public sealed record CompanyRow(
    Guid Id,
    string Name,
    string Tin,
    string Activity,
    string Address,
    int RadiusM,
    int Students,
    int SuspiciousDays,
    bool IsActive,
    int MaxStudents,
    bool OverLimit,
    CompanyFlag? Flag);

/// <summary><c>GET /api/admin/companies?q&amp;page&amp;pageSize</c> — <c>q</c>: nom, STIR, manzil.</summary>
public sealed record GetCompaniesQuery : PagedQuery, IRequest<Paged<CompanyRow>>;

internal sealed class GetCompaniesQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetCompaniesQuery, Paged<CompanyRow>>
{
    public async Task<Paged<CompanyRow>> Handle(GetCompaniesQuery request, CancellationToken cancellationToken)
    {
        var companies = db.Companies.AsNoTracking();
        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            companies = companies.Where(c =>
                EF.Functions.Like(c.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(c.Tin, pattern, AdminSearch.Escape)
                || EF.Functions.Like(c.Address.ToLower(), pattern, AdminSearch.Escape));
        }

        var page = await companies
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Tin,
                c.Activity,
                c.Address,
                c.RadiusM,
                c.IsActive
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<CompanyRow>.Empty(request);

        // Sonlar — faqat sahifadagi korxonalar uchun, aktiv qoida bo'yicha (§4.7).
        var companyIds = page.Items.Select(c => c.Id).ToArray();
        var placements = await CompanyQueries.LoadActivePlacementsAsync(db, scope: null, companyIds, clock.LocalToday(), cancellationToken);
        var studentsByCompany = placements.GroupBy(p => p.CompanyId).ToDictionary(g => g.Key, g => g.Count());
        var suspiciousByCompany = await CompanyQueries.CountSuspiciousDaysAsync(db, placements, cancellationToken);

        var maxStudents = await CompanyQueries.LoadMaxStudentsAsync(db, cancellationToken);

        var rows = page.Items.Select(c =>
        {
            var students = studentsByCompany.GetValueOrDefault(c.Id);
            var suspiciousDays = suspiciousByCompany.GetValueOrDefault(c.Id);
            var overLimit = students > maxStudents;
            return new CompanyRow(
                c.Id, c.Name, c.Tin, c.Activity, c.Address, c.RadiusM, students, suspiciousDays, c.IsActive,
                maxStudents, overLimit,
                CompanyFlags.Resolve(suspiciousDays, c.RadiusM, overLimit));
        }).ToList();

        return new Paged<CompanyRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
