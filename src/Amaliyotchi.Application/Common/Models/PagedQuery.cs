namespace Amaliyotchi.Application.Common.Models;

/// <summary>Sahifalanadigan ro'yxat so'rovlarining bazasi: <c>?q&amp;page&amp;pageSize</c>.
/// Noto'g'ri qiymatlar xato bermaydi — standart qiymatga tushiriladi.</summary>
public abstract record PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private int _pageSize = DefaultPageSize;
    private int _page = 1;
    private string? _q;

    /// <summary>Matnli qidiruv (case-insensitive). Bo'sh bo'lsa null.</summary>
    public string? Q
    {
        get => _q;
        init => _q = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value is < 1 or > MaxPageSize ? DefaultPageSize : value;
    }

    public int Skip => (Page - 1) * PageSize;
}
