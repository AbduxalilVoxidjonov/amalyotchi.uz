using System.Linq.Expressions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Diary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Supervision.Diaries;

/// <summary>Kundaliklar navbati qoidasi — tekshirilmagan (admin hali baholamagan) yozuvlar: <c>submitted</c> yoki
/// <c>seen</c>. Sahifada ular birinchi turadi va baholash tugmalari faqat ularda ochiq; sidebar badge'i
/// (<c>GET /api/admin/nav</c> → <c>counts.diaries</c>) shu predikat bo'yicha sanaydi.</summary>
public static class DiaryQueue
{
    public static readonly Expression<Func<DiaryEntry, bool>> Unreviewed =
        d => d.Status == DiaryStatus.Submitted || d.Status == DiaryStatus.Seen;

    /// <summary><see cref="Unreviewed"/> ning inkori (rewrite/approved).</summary>
    public static readonly Expression<Func<DiaryEntry, bool>> Reviewed =
        d => d.Status != DiaryStatus.Submitted && d.Status != DiaryStatus.Seen;
}

/// <summary><c>GET /api/admin/diaries?status=&amp;limit=</c> — tekshirilmaganlar (submitted/seen) birinchi, keyin yangisi tepada.
/// <para><paramref name="Limit"/> — javobdagi eng ko'p yozuv (sukut <see cref="DefaultLimit"/>, 1..<see cref="MaxLimit"/>
/// oralig'iga keltiriladi). Avval cheklov yo'q edi: butun tarix (har talaba × har kun, matn bilan) bitta javobda ketardi —
/// yuzlab talabada bir necha MB JSON va sahifada minglab karta. Frontend "Ko'proq" tugmasi bilan limitni oshiradi;
/// javob uzunligi limitga teng bo'lsa — yana yozuv bor.</para></summary>
public sealed record GetDiariesQuery(DiaryStatus? Status, int? Limit = null) : IRequest<IReadOnlyList<DiaryReviewEntry>>
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 1000;
}

internal sealed class GetDiariesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetDiariesQuery, IReadOnlyList<DiaryReviewEntry>>
{
    public async Task<IReadOnlyList<DiaryReviewEntry>> Handle(GetDiariesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var query = db.DiaryEntries.AsNoTracking().InScope(scope);
        if (request.Status is { } status)
            query = query.Where(d => d.Status == status);

        var limit = Math.Clamp(request.Limit ?? GetDiariesQuery.DefaultLimit, 1, GetDiariesQuery.MaxLimit);

        // Proyeksiya talaba profili bilan INNER JOIN — profilsiz yozuv limitni "yemasin" (aks holda javob limitdan qisqa
        // chiqib, frontend "boshqa yozuv yo'q" deb o'ylaydi).
        query = query.Where(d => db.StudentProfiles.Any(p => p.UserId == d.StudentUserId));

        // Ikki indeksli so'rov (ix_diary_entries_submitted_at bo'yicha teskari skan, LIMIT'da to'xtaydi): avval tekshirilmaganlar,
        // joy qolsa — qolganlari. Bitta "CASE ... ORDER BY" so'rovi butun jadvalni saralardi (69k yozuvda ~116 ms → ~8 ms).
        var result = await PageAsync(query.Where(DiaryQueue.Unreviewed), limit, cancellationToken);
        if (result.Count < limit)
            result.AddRange(await PageAsync(query.Where(DiaryQueue.Reviewed), limit - result.Count, cancellationToken));

        return result;
    }

    private async Task<List<DiaryReviewEntry>> PageAsync(
        IQueryable<DiaryEntry> query, int take, CancellationToken cancellationToken)
    {
        var page = await query
            .OrderByDescending(d => d.SubmittedAt)
            .ThenByDescending(d => d.Id)
            .Take(take)
            .SelectReviewEntriesAsync(db, cancellationToken);

        // Proyeksiyadagi JOIN tartibni kafolatlamaydi — xotirada qayta tartiblanadi (≤ limit qator).
        return page.OrderByDescending(e => e.SubmittedAt).ThenByDescending(e => e.Id).ToList();
    }
}
