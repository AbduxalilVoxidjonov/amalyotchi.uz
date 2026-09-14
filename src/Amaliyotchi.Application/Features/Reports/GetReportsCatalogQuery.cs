using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Reports;

/// <param name="Scope">Ko'lam yorlig'i: tyutor — guruhlar ("412-22, 413-22"), admin — "Barcha fakultetlar".</param>
/// <param name="Groups">Tyutor guruhlari (admin uchun bo'sh).</param>
public sealed record ReportFilter(DateOnly? DateFrom, DateOnly? DateTo, string Scope, IReadOnlyList<string> Groups, int StudentCount);

/// <param name="Formats">"pdf" / "xlsx".</param>
/// <param name="Available">Fayl generatsiyasi hozircha yo'q (M14) — <c>false</c>, sabab <see cref="Note"/> da.</param>
public sealed record ReportCard(string Id, string Name, IReadOnlyList<string> Formats, string Desc, bool Available, string? Note);

public sealed record ReportsCatalog(ReportFilter Filter, IReadOnlyList<ReportCard> Reports);

/// <summary><c>GET /api/reports</c> (Admin, Tyutor) — hisobotlar katalogi: filtr (faol davr sanalari, ko'lam) + kartalar.
/// Yuklab olish (<c>/api/reports/{id}/download</c>) keyingi bosqichda.</summary>
public sealed record GetReportsCatalogQuery : IRequest<ReportsCatalog>;

internal sealed class GetReportsCatalogQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetReportsCatalogQuery, ReportsCatalog>
{
    public const string AllFacultiesScope = "Barcha fakultetlar";
    private const string NotAvailableNote = "Fayl generatsiyasi keyingi bosqichda (M14) qo'shiladi.";

    private static readonly ReportCard[] Cards =
    [
        new("attendance", "Davomat hisoboti", ["xlsx"],
            "Davr bo'yicha har talabaning kunlik davomati: keldi / kech keldi / kelmadi / sababli, foiz.", false, NotAvailableNote),
        new("portfolio", "Talaba portfoliosi", ["pdf"],
            "Kundalik hisobotlar, davomat statistikasi, baholar va tyutor xulosasi — bitta PDF.", false, NotAvailableNote),
        new("company-reference", "Korxona tavsifnomasi", ["pdf"],
            "Korxona rahbari imzolaydigan tavsifnoma shabloni (talaba ma'lumotlari to'ldirilgan).", false, NotAvailableNote),
        new("diaries", "Kundaliklar yig'masi", ["pdf", "xlsx"],
            "Davr bo'yicha barcha kundalik yozuvlari va tyutor baholari.", false, NotAvailableNote)
    ];

    public async Task<ReportsCatalog> Handle(GetReportsCatalogQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var periodsQuery = db.PracticePeriods.AsNoTracking().Where(p => p.Status == PracticePeriodStatus.Active);
        if (scope.Kind == ScopeKind.Groups)
            periodsQuery = periodsQuery.Where(p => p.Groups.Any(g => scope.StudentGroupIds.Contains(g.StudentGroupId)));

        var range = await periodsQuery
            .GroupBy(_ => 1)
            .Select(g => new { From = g.Min(p => p.StartDate), To = g.Max(p => p.EndDate) })
            .FirstOrDefaultAsync(cancellationToken);

        List<string> groups = [];
        string scopeLabel;
        int studentCount;

        if (scope.IsUnrestricted)
        {
            scopeLabel = AllFacultiesScope;
            studentCount = await db.StudentProfiles.AsNoTracking().CountAsync(cancellationToken);
        }
        else
        {
            groups = await db.StudentGroups.AsNoTracking()
                .Where(g => scope.StudentGroupIds.Contains(g.Id))
                .OrderBy(g => g.Name)
                .Select(g => g.Name)
                .ToListAsync(cancellationToken);
            scopeLabel = string.Join(", ", groups);
            studentCount = await db.StudentProfiles.AsNoTracking().InScope(scope).CountAsync(cancellationToken);
        }

        return new ReportsCatalog(
            new ReportFilter(range?.From, range?.To, scopeLabel, groups, studentCount),
            Cards);
    }
}
