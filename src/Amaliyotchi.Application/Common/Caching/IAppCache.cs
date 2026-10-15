namespace Amaliyotchi.Application.Common.Caching;

/// <summary>O'qish yo'llari uchun kesh (Infrastructure: HybridCache — L1 xotira + ixtiyoriy L2 Redis).
/// Kesh faqat tezlashtiradi, manba emas: Redis ishlamasa ham <paramref name="factory"/> bazadan o'qiydi.
/// <para><b>Bekor qilish</b> handler'larda yozilmaydi: <c>SaveChanges</c> interceptor'i o'zgargan entity turlariga qarab
/// tegishli <see cref="CacheTags"/> ni o'zi tozalaydi (yozish yo'llaridan birini "unutib" qo'yib bo'lmaydi). Shuning uchun
/// keshlanadigan qiymat faqat <see cref="CacheTags"/> da ko'rsatilgan jadvallarga bog'liq bo'lishi shart — yangi
/// bog'liqlik qo'shilsa, <c>CacheDependencies</c> ham yangilanadi.</para></summary>
public interface IAppCache
{
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan expiration,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>Teg(lar)ga bog'langan barcha yozuvlarni bekor qiladi.</summary>
    ValueTask InvalidateAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}

/// <summary>Kesh teglari — har biri bitta "bog'liqlik guruhi". Entity → teg xaritasi: Infrastructure <c>CacheDependencies</c>.</summary>
public static class CacheTags
{
    /// <summary>Sidebar badge'lari (<c>GET /api/admin/nav</c>).</summary>
    public const string AdminNav = "admin-nav";

    /// <summary>Admin bosh sahifasi (<c>GET /api/admin/dashboard</c>).</summary>
    public const string AdminDashboard = "admin-dashboard";

    /// <summary>Tizim sozlamalari, bayramlar, hujjat shablonlari.</summary>
    public const string Settings = "settings";

    /// <summary>Tuzilma ma'lumotnomalari: fakultet / kafedra / yo'nalish / guruh / o'quv yili (filtrlar, pickerlar).</summary>
    public const string Organization = "organization";

    public static readonly IReadOnlyList<string> All = [AdminNav, AdminDashboard, Settings, Organization];
}

/// <summary>Kesh kalitlari va muddatlari bitta joyda. Muddatlar qisqa — bekor qilish asosiy mexanizm, TTL esa
/// tranzaksiya poygasi yoki tashqi (EF'siz) yozuv bo'lsa ham eskirishni chegaralaydi.</summary>
public static class CacheKeys
{
    public const string Prefix = "v1:";

    /// <summary>Admin ko'lami cheklovsiz (hamma admin bir xil ma'lumot ko'radi) — kalitda foydalanuvchi yo'q.</summary>
    public const string AdminNav = Prefix + "admin:nav";
    public static readonly TimeSpan AdminNavTtl = TimeSpan.FromSeconds(60);

    /// <summary>Vaqtga bog'liq ("bugun/kecha" davomati, muddati o'tgan arizalar) — UTC daqiqa kalitda.</summary>
    public static string AdminDashboard(DateTimeOffset utcNow)
        => $"{Prefix}admin:dashboard:{utcNow.UtcDateTime:yyyy-MM-ddTHH:mm}";
    public static readonly TimeSpan AdminDashboardTtl = TimeSpan.FromSeconds(30);

    public const string AdminSettings = Prefix + "settings:admin";
    public static readonly TimeSpan SettingsTtl = TimeSpan.FromMinutes(5);

    public const string StudentFilters = Prefix + "org:student-filters";

    public static string StudentGroupOptions(Guid? facultyId, Guid? directionId, int? course)
        => $"{Prefix}org:group-options:{facultyId:N}:{directionId:N}:{course}";

    public static readonly TimeSpan OrganizationTtl = TimeSpan.FromMinutes(10);
}
