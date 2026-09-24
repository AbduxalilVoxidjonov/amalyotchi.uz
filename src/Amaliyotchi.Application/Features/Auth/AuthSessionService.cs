using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;

namespace Amaliyotchi.Application.Features.Auth;

/// <summary>Auth handler'lari (login, Telegram login, Telegram bog'lash) uchun umumiy qism: HEMIS ID + parol
/// tekshiruvi, muvaffaqiyatsiz urinish auditi va sessiya (access + refresh token) berish. Mantiq bir joyda —
/// login va Telegram bog'lash AYNAN bir xil qoidalar bilan ishlaydi.</summary>
internal sealed class AuthSessionService(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditWriter audit,
    ICurrentUser currentUser)
{
    public const string InvalidCredentials = "HEMIS ID yoki parol noto'g'ri.";
    public const string TelegramSignatureRejected = "Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching.";

    /// <summary>HEMIS ID + parolni tekshiradi va foydalanuvchini (kuzatiladigan, <see cref="UserQueries.WithSummary"/>
    /// + muddati o'tgan refresh tokenlar bilan) qaytaradi. Avval xodim (<c>User.HemisId</c>), topilmasa — talaba
    /// (<c>StudentProfile.HemisId</c>). Xato → audit <c>LoginFailed</c> + <see cref="ForbiddenException"/>.
    /// Parol faollikdan OLDIN tekshiriladi. Kerak bo'lsa xesh yangilanadi (saqlash — chaqiruvchida).</summary>
    public async Task<User> VerifyPasswordAsync(
        string rawHemisId, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // HEMIS ID noto'g'ri formatda bo'lsa ham javob bir xil bo'ladi —
        // "bu ID ro'yxatda bormi" degan ma'lumotni tashqariga chiqarmaslik uchun.
        if (!Hemis.TryNormalize(rawHemisId, out var hemisId))
            throw new ForbiddenException(InvalidCredentials);

        // Faqat muddati o'tgan tokenlar yuklanadi — ular sessiya berilganda tozalanadi.
        var user = await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => t.ExpiresAt <= now))
            .FirstOrDefaultAsync(u => u.HemisId == hemisId, cancellationToken);

        // Talaba: login identifikatori — profil HEMIS ID'si (User.HemisId talabada null). O'chirilgan profil
        // global filtr bilan chiqib ketadi.
        user ??= await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => t.ExpiresAt <= now))
            .FirstOrDefaultAsync(
                u => u.Role == UserRole.Student && u.StudentProfile != null && u.StudentProfile.HemisId == hemisId,
                cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            throw await LoginFailedAsync(
                user, "Foydalanuvchi topilmadi yoki parol bilan kirish mumkin emas",
                InvalidCredentials, cancellationToken);
        }

        // Parol faollikdan OLDIN tekshiriladi: aks holda "hisob faol emas" xabari
        // parolni bilmagan odamga ham raqam ro'yxatda borligini oshkor qiladi.
        if (!passwordHasher.Verify(password, user.PasswordHash, out var needsRehash))
            throw await LoginFailedAsync(user, "Parol noto'g'ri", InvalidCredentials, cancellationToken);

        if (!user.IsActive)
        {
            throw await LoginFailedAsync(
                user, "Hisob faol emas",
                user.IsStudent
                    ? "Hisobingiz faol emas. Tyutoringizga murojaat qiling."
                    : "Hisobingiz faol emas. Administratorga murojaat qiling.",
                cancellationToken);
        }

        if (needsRehash)
            user.SetPasswordHash(passwordHasher.Hash(password));

        return user;
    }

    /// <summary>Sessiya beradi: <c>LastLoginAt</c>, eski tokenlarni tozalash, access + refresh token, audit
    /// <c>LoggedIn</c> (<paramref name="auditReason"/> bilan) va <c>SaveChanges</c>.</summary>
    public async Task<AuthResultDto> IssueSessionAsync(
        User user, DateTimeOffset now, string? auditReason, CancellationToken cancellationToken)
    {
        user.MarkLogin(now);
        user.PruneRefreshTokens(now);

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = user.IssueRefreshToken(
            tokenService.CreateRefreshToken(),
            now.Add(tokenService.RefreshTokenLifetime),
            currentUser.IpAddress);

        await audit.WriteAsync(
            AuditAction.LoggedIn, nameof(User), user.Id.ToString(),
            userId: user.Id, userRole: user.Role,
            reason: auditReason,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken.Token,
            UserSummaryDto.From(user),
            user.MustChangePassword);
    }

    /// <summary>Muvaffaqiyatsiz urinishni audit jurnaliga yozib, DARHOL saqlaydi — aks holda tashlanadigan
    /// xato tufayli yozuv hech qachon bazaga tushmaydi.</summary>
    public async Task RecordFailureAsync(User? user, string reason, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(
            AuditAction.LoginFailed, nameof(User), user?.Id.ToString(),
            userId: user?.Id, userRole: user?.Role,
            reason: reason,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary><see cref="RecordFailureAsync"/> + mijozga ko'rsatiladigan <paramref name="message"/> bilan 403.</summary>
    public async Task<ForbiddenException> LoginFailedAsync(
        User? user, string reason, string message, CancellationToken cancellationToken)
    {
        await RecordFailureAsync(user, reason, cancellationToken);
        return new ForbiddenException(message);
    }
}
