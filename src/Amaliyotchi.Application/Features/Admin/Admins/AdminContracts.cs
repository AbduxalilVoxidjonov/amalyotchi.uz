using System.Linq.Expressions;
using System.Security.Cryptography;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary>Adminlar ro'yxati qatori (<c>/api/admin/admins</c>). <paramref name="Login"/> — <c>User.HemisId</c>
/// (HEMIS ID formatidagi raqamli login).</summary>
public sealed record AdminAccountDto(
    Guid Id,
    string FullName,
    string Login,
    string? PhoneNumber,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record AdminListDto(IReadOnlyList<AdminAccountDto> Items);

/// <summary><c>POST /api/admin/admins</c> javobi — qator maydonlari + <paramref name="TemporaryPassword"/>
/// (parol berilmagan bo'lsa server yaratgan vaqtinchalik parol, aks holda <c>null</c>).</summary>
public sealed record CreatedAdminDto(
    Guid Id,
    string FullName,
    string Login,
    string? PhoneNumber,
    bool IsActive,
    bool IsSuperAdmin,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    string? TemporaryPassword);

/// <summary><c>POST /api/admin/admins/{id}/password</c> javobi.</summary>
public sealed record AdminPasswordResetDto(string TemporaryPassword);

internal static class AdminAccounts
{
    public const string NotFoundMessage = "Admin topilmadi.";
    public const string NotSuperAdminMessage = "Bu amal faqat bosh admin uchun.";
    public const string PhoneTakenMessage = "Bu telefon raqami bilan foydalanuvchi mavjud.";

    public static readonly Expression<Func<User, AdminAccountDto>> Projection = u => new AdminAccountDto(
        u.Id, u.FullName, u.HemisId ?? string.Empty, u.PhoneNumber, u.IsActive, u.IsSuperAdmin, u.LastLoginAt, u.CreatedAt);

    public static AdminAccountDto ToDto(User u)
        => new(u.Id, u.FullName, u.HemisId ?? string.Empty, u.PhoneNumber, u.IsActive, u.IsSuperAdmin, u.LastLoginAt, u.CreatedAt);

    /// <summary>Policy (<c>SuperAdminOnly</c>, JWT claim) ustiga bazadan qo'shimcha tekshiruv: joriy foydalanuvchi faol
    /// bosh admin bo'lmasa → 403. Claim eskirgan bo'lsa ham (masalan belgi olib tashlangan) amal bajarilmaydi.</summary>
    public static async Task<Guid> EnsureSuperAdminAsync(
        IApplicationDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException(NotSuperAdminMessage);
        var isSuper = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin && u.IsActive && u.IsSuperAdmin, cancellationToken);
        if (!isSuper)
            throw new ForbiddenException(NotSuperAdminMessage);
        return userId;
    }

    /// <summary>Faqat admin roli (talaba yoki o'chirilgan hisob — 404).</summary>
    public static async Task<User> LoadAdminAsync(
        IApplicationDbContext db, Guid id, bool withActiveTokens, CancellationToken cancellationToken)
    {
        IQueryable<User> users = withActiveTokens
            ? db.Users.Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null))
            : db.Users;
        return await users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Admin, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
    }
}

/// <summary>Vaqtinchalik parol: 12 belgi, adashtiradigan belgilarsiz (0/O, 1/l/I), kriptografik tasodifiy.</summary>
internal static class TemporaryPasswords
{
    public const int Length = 12;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
