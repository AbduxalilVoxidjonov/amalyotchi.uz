namespace Amaliyotchi.Application.Common.Models;

/// <summary>Kontraktdagi yagona sahifalash shakli: <c>{ items, page, pageSize, total }</c>.</summary>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public static Paged<T> Empty(PagedQuery query) => new([], query.Page, query.PageSize, 0);
}
