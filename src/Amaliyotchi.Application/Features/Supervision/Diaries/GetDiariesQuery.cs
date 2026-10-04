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
}

/// <summary><c>GET /api/admin/diaries?status=</c> — tekshirilmaganlar (submitted/seen) birinchi, keyin yangisi tepada.</summary>
public sealed record GetDiariesQuery(DiaryStatus? Status) : IRequest<IReadOnlyList<DiaryReviewEntry>>;

internal sealed class GetDiariesQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetDiariesQuery, IReadOnlyList<DiaryReviewEntry>>
{
    public async Task<IReadOnlyList<DiaryReviewEntry>> Handle(GetDiariesQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var query = db.DiaryEntries.AsNoTracking().InScope(scope);
        if (request.Status is { } status)
            query = query.Where(d => d.Status == status);

        return await query
            .OrderBy(d => d.Status == DiaryStatus.Submitted || d.Status == DiaryStatus.Seen ? 0 : 1)
            .ThenByDescending(d => d.SubmittedAt)
            .SelectReviewEntriesAsync(db, cancellationToken);
    }
}
