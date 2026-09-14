using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Common.Models;

public static class QueryableExtensions
{
    /// <summary>Umumiy sonni hisoblab, so'ralgan sahifani yuklaydi. Tartiblash chaqiruvchida bo'lishi shart —
    /// aks holda sahifalar orasida qatorlar takrorlanishi mumkin.</summary>
    public static async Task<Paged<T>> ToPagedAsync<T>(
        this IQueryable<T> source,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var total = await source.CountAsync(cancellationToken);
        if (total == 0)
            return Paged<T>.Empty(query);

        var items = await source
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new Paged<T>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Xotiradagi ro'yxat uchun (kichik, oldindan hisoblangan natijalar).</summary>
    public static Paged<T> ToPaged<T>(this IReadOnlyCollection<T> source, PagedQuery query)
    {
        if (source.Count == 0)
            return Paged<T>.Empty(query);

        var items = source.Skip(query.Skip).Take(query.PageSize).ToList();
        return new Paged<T>(items, query.Page, query.PageSize, source.Count);
    }
}
