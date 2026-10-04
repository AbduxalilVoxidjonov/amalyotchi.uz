using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Supervision.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Reports;

/// <param name="Scope">Ko'lam yorlig'i — doim "Barcha fakultetlar" (faqat admin).</param>
/// <param name="Groups">Har doim bo'sh (tarixiy maydon — kontrakt mosligi uchun saqlangan).</param>
public sealed record ReportFilter(DateOnly? DateFrom, DateOnly? DateTo, string Scope, IReadOnlyList<string> Groups, int StudentCount);

/// <param name="Formats">"pdf" / "xlsx".</param>
/// <param name="Available">Fayl generatsiyasi hozircha yo'q (M14) — <c>false</c>, sabab <see cref="Note"/> da.</param>
public sealed record ReportCard(string Id, string Name, IReadOnlyList<string> Formats, string Desc, bool Available, string? Note);

public sealed record ReportsCatalog(ReportFilter Filter, IReadOnlyList<ReportCard> Reports);

/// <summary><c>GET /api/reports</c> (Admin) — hisobotlar katalogi: filtr (barcha guruhlarning sukut bo'yicha
/// davrlari sanalari — davom etayotgan → oxirgi tugagan → kelgusi, <c>PeriodLookup.ForGroup</c>; ko'lam) + kartalar.
/// Yuklab olish (<c>/api/reports/{id}/download</c>) keyingi bosqichda.</summary>
public sealed record GetReportsCatalogQuery : IRequest<ReportsCatalog>;

internal sealed class GetReportsCatalogQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetReportsCatalogQuery, ReportsCatalog>
{
    public const string AllFacultiesScope = "Barcha fakultetlar";
    private const string NotAvailableNote = "Fayl generatsiyasi keyingi bosqichda (M14) qo'shiladi.";

    private static readonly ReportCard[] Cards =
    [
        new("attendance", "Davomat hisoboti", ["xlsx"],
            "Davr bo'yicha har talabaning kunlik davomati: keldi / kech keldi / kelmadi / sababli, foiz.", false, NotAvailableNote),
        new("portfolio", "Talaba portfoliosi", ["pdf"],
            "Kundalik hisobotlar, davomat statistikasi, baholar va rahbar xulosasi — bitta PDF.", false, NotAvailableNote),
        new("company-reference", "Korxona tavsifnomasi", ["pdf"],
            "Korxona rahbari imzolaydigan tavsifnoma shabloni (talaba ma'lumotlari to'ldirilgan).", false, NotAvailableNote),
        new("diaries", "Kundaliklar yig'masi", ["pdf", "xlsx"],
            "Davr bo'yicha barcha kundalik yozuvlari va ularning baholari.", false, NotAvailableNote)
    ];

    public async Task<ReportsCatalog> Handle(GetReportsCatalogQuery request, CancellationToken cancellationToken)
    {
        // Faqat admin (AdminOnly) — ko'lam doim cheklovsiz: barcha guruhlar va talabalar.
        var lookup = await PeriodLookup.LoadAsync(db, clock.LocalToday(), cancellationToken);
        var selected = lookup.GroupIds
            .Select(lookup.ForGroup)
            .Where(p => p is not null)
            .Select(p => p!.Period)
            .ToList();
        var range = selected.Count == 0
            ? null
            : new { From = selected.Min(p => p.StartDate), To = selected.Max(p => p.EndDate) };

        var studentCount = await db.StudentProfiles.AsNoTracking().CountAsync(cancellationToken);

        return new ReportsCatalog(
            new ReportFilter(range?.From, range?.To, AllFacultiesScope, [], studentCount),
            Cards);
    }
}
