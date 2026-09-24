using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Hemis = Amaliyotchi.Domain.ValueObjects.HemisId;

namespace Amaliyotchi.Application.Features.Auth.Login;

/// <summary>HEMIS ID + parol bilan kirish. Avval xodim (admin/tyutor, <c>User.HemisId</c>) qidiriladi, topilmasa —
/// talaba (<c>StudentProfile.HemisId</c>, o'chirilmagan profil). Talaba faqat xodim unga parol o'rnatgan bo'lsa
/// kira oladi (brauzer rejimi); parolsiz hisob umumiy "HEMIS ID yoki parol noto'g'ri" xatosini oladi.</summary>
public sealed record LoginCommand(string HemisId, string Password) : IRequest<AuthResultDto>;

internal sealed class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditWriter audit,
    ICurrentUser currentUser,
    IClock clock)
    : IRequestHandler<LoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // HEMIS ID noto'g'ri formatda bo'lsa ham javob bir xil bo'ladi —
        // "bu ID ro'yxatda bormi" degan ma'lumotni tashqariga chiqarmaslik uchun.
        if (!Hemis.TryNormalize(request.HemisId, out var hemisId))
            throw new ForbiddenException(InvalidCredentials);

        var now = clock.UtcNow;

        // Faqat muddati o'tgan tokenlar yuklanadi — ular shu yerda tozalanadi.
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
        if (!passwordHasher.Verify(request.Password, user.PasswordHash, out var needsRehash))
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
            user.SetPasswordHash(passwordHasher.Hash(request.Password));

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
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken.Token,
            UserSummaryDto.From(user),
            user.MustChangePassword);
    }

    /// <summary>Muvaffaqiyatsiz urinishni audit jurnaliga yozib, DARHOL saqlaydi —
    /// aks holda tashlanadigan xato tufayli yozuv hech qachon bazaga tushmaydi.</summary>
    private async Task<ForbiddenException> LoginFailedAsync(
        User? user, string reason, string message, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(
            AuditAction.LoginFailed, nameof(User), user?.Id.ToString(),
            userId: user?.Id, userRole: user?.Role,
            reason: reason,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return new ForbiddenException(message);
    }

    private const string InvalidCredentials = "HEMIS ID yoki parol noto'g'ri.";
}
