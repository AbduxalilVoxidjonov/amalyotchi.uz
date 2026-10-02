using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Security;
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

    /// <summary>Lockout hisoblash migratsiyasiz — audit jurnalidagi <c>LoginFailed</c> yozuvlaridan
    /// (<c>audit_logs.user_id</c> indeksi). Qiymatlar — <see cref="AuthSecurity"/>.</summary>
    public const string WrongPasswordReason = "Parol noto'g'ri";
    public const string LockedOutReason = "Hisob vaqtincha bloklangan (ko'p noto'g'ri parol)";

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
        // Lockout parol tekshiruvidan OLDIN: bloklangan paytda to'g'ri parol ham qabul qilinmaydi (aks holda
        // bu shunchaki "sekinlatish" bo'lardi). Javob odatdagi "noto'g'ri" xabari bilan bir xil — hisob borligini
        // va bloklanganini oshkor qilmaydi. Bloklangan urinishlar hisobga qo'shilmaydi (blok cheksiz cho'zilmaydi).
        if (await IsLockedOutAsync(user, now, cancellationToken))
            throw await LoginFailedAsync(user, LockedOutReason, InvalidCredentials, cancellationToken);

        if (!passwordHasher.Verify(password, user.PasswordHash, out var needsRehash))
            throw await LoginFailedAsync(user, WrongPasswordReason, InvalidCredentials, cancellationToken);

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

    private async Task<bool> IsLockedOutAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Muvaffaqiyatli kirishdan oldingi xatolar hisobga olinmaydi.
        var since = now - AuthSecurity.LockoutWindow;
        if (user.LastLoginAt is { } lastLogin && lastLogin > since)
            since = lastLogin;

        var failures = await db.AuditLogs
            .AsNoTracking()
            .CountAsync(a => a.UserId == user.Id
                             && a.Action == AuditAction.LoginFailed
                             && a.Reason == WrongPasswordReason
                             && a.OccurredAt >= since,
                cancellationToken);

        return failures >= AuthSecurity.LockoutThreshold;
    }

    /// <summary>Sessiya beradi: <c>LastLoginAt</c>, eski tokenlarni tozalash, access + refresh token, audit
    /// <c>LoggedIn</c> (<paramref name="auditReason"/> bilan) va <c>SaveChanges</c>.</summary>
    public async Task<AuthResultDto> IssueSessionAsync(
        User user, DateTimeOffset now, string? auditReason, CancellationToken cancellationToken)
    {
        user.MarkLogin(now);
        user.PruneRefreshTokens(now);

        var accessToken = tokenService.CreateAccessToken(user);
        // Bazaga faqat xesh yoziladi; mijozga xom token qaytariladi.
        var rawRefreshToken = tokenService.CreateRefreshToken();
        user.IssueRefreshToken(
            RefreshTokenHash.Of(rawRefreshToken),
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
            rawRefreshToken,
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
