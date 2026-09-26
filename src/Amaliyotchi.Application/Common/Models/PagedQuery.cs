namespace Amaliyotchi.Application.Common.Models;

/// <summary>Sahifalanadigan ro'yxat so'rovlarining bazasi: <c>?q&amp;page&amp;pageSize</c>.
/// Noto'g'ri qiymatlar xato bermaydi — standart qiymatga tushiriladi (<c>pageSize</c> 1..<see cref="MaxPageSizeLimit"/>
/// oralig'idan tashqarida bo'lsa → <see cref="DefaultPageSize"/>).</summary>
public abstract record PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Kengaytirilgan chegara — alohida ro'yxatlar (masalan admin talabalar) <see cref="MaxPageSizeLimit"/> ni
    /// shu qiymatga override qiladi.</summary>
    public const int ExtendedMaxPageSize = 500;

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

    /// <summary>Shu so'rov turi uchun <see cref="PageSize"/> ning yuqori chegarasi. Sukut — <see cref="MaxPageSize"/>;
    /// so'rov turi override qilib kattalashtirishi mumkin. Faqat o'qiladi — query string'dan bog'lanmaydi.</summary>
    public virtual int MaxPageSizeLimit => MaxPageSize;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 || value > MaxPageSizeLimit ? DefaultPageSize : value;
    }

    public int Skip => (Page - 1) * PageSize;
}
