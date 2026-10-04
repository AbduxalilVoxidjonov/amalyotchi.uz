using System.Security.Cryptography;
using System.Text;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Amaliyotchi.Infrastructure.Identity;

/// <summary>Access token "security stamp" tekshiruvi: hisob o'chirilsa/faolsizlantirilsa yoki parol, login
/// (HEMIS ID), rol, fakultet yoki bosh admin belgisi o'zgarsa — avval berilgan access tokenlar darhol ishlamay qoladi.
/// Stamp alohida ustunsiz (migratsiyasiz) shu maydonlardan hisoblanadi: <c>PasswordHash</c> har almashishda yangi tuz
/// bilan yaratiladi, shuning uchun parol (hatto o'sha qiymatga) qayta o'rnatilsa ham stamp o'zgaradi.
/// Har so'rovda og'ir query bo'lmasligi uchun natija <see cref="IMemoryCache"/> da qisqa muddat saqlanadi;
/// foydalanuvchi yozuvi o'zgarganda kesh <see cref="UserSessionCacheInterceptor"/> orqali darhol tozalanadi.</summary>
public sealed class UserSessionValidator(AppDbContext db, IMemoryCache cache)
{
    public const string StampClaim = "sstamp";

    /// <summary>Kesh muddati — interceptor ko'rmaydigan o'zgarishlar (masalan to'g'ridan-to'g'ri SQL) uchun yuqori chegara.</summary>
    public static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private const string Inactive = "";

    public static string CacheKey(Guid userId) => $"user-session-stamp:{userId:N}";

    public static string ComputeStamp(User user)
        => ComputeStamp(user.PasswordHash, user.HemisId, user.Role, user.FacultyId, user.IsSuperAdmin);

    /// <summary><paramref name="isSuperAdmin"/> faqat <c>true</c> bo'lganda material'ga qo'shiladi — oddiy foydalanuvchilarning
    /// stamp'i (va deploy oldidan berilgan tokenlari) o'zgarmaydi; bosh admin belgisi o'zgarsa token qayta olinadi va
    /// <c>is_super_admin</c> claim'i yangilanadi.</summary>
    public static string ComputeStamp(string? passwordHash, string? hemisId, UserRole role, Guid? facultyId, bool isSuperAdmin)
    {
        var material = string.Join('\n', passwordHash ?? "-", hemisId ?? "-", role.ToString(), facultyId?.ToString("N") ?? "-");
        if (isSuperAdmin)
            material += "\nsuper-admin";
        // 128 bit yetarli: stamp sir emas (tokenning o'zi imzolangan), faqat o'zgarishni aniqlash uchun.
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material)), 0, 16);
    }

    public async Task<bool> IsCurrentAsync(Guid userId, string? stamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(stamp))
            return false;

        var current = await cache.GetOrCreateAsync(CacheKey(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.PasswordHash, u.HemisId, u.Role, u.FacultyId, u.IsSuperAdmin })
                .FirstOrDefaultAsync(cancellationToken);

            // O'chirilgan (soft delete filtri) yoki faolsiz hisob — hech qanday token qabul qilinmaydi.
            return user is null || !user.IsActive
                ? Inactive
                : ComputeStamp(user.PasswordHash, user.HemisId, user.Role, user.FacultyId, user.IsSuperAdmin);
        }) ?? Inactive;

        return current.Length > 0
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(current), Encoding.UTF8.GetBytes(stamp));
    }
}
