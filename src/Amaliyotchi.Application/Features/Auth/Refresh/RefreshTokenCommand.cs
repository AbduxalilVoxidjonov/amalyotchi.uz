using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Amaliyotchi.Domain.Identity;

namespace Amaliyotchi.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResultDto>;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var tokenHash = RefreshTokenHash.Of(request.RefreshToken ?? string.Empty);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken)
            ?? throw new ForbiddenException(TokenRejected);

        if (!stored.IsActive(now))
        {
            if (IsReuse(stored, now))
                await RevokeAllSessionsAsync(stored.UserId, now, cancellationToken);

            throw new ForbiddenException(TokenRejected);
        }

        // Butun token tarixi emas, faqat muddati o'tganlari yuklanadi — ular shu yerda tozalanadi.
        var user = await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => t.ExpiresAt <= now))
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            ?? throw new ForbiddenException(TokenRejected);

        if (!user.IsActive)
            throw new ForbiddenException("Hisobingiz faol emas.");

        user.PruneRefreshTokens(now);

        // Rotatsiya: eski token darhol bekor qilinadi, yangisi beriladi.
        var rawRefreshToken = tokenService.CreateRefreshToken();
        var newRefreshToken = user.IssueRefreshToken(
            RefreshTokenHash.Of(rawRefreshToken),
            now.Add(tokenService.RefreshTokenLifetime),
            currentUser.IpAddress);

        stored.Revoke(now, "Yangilandi", newRefreshToken.Token);

        var accessToken = tokenService.CreateAccessToken(user);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            UserSummaryDto.From(user),
            user.MustChangePassword);
    }

    private const string TokenRejected = "Sessiya muddati tugagan. Qaytadan kiring.";

    /// <summary>Rotatsiyada almashtirilgan (<c>ReplacedByToken</c> bor) token grace oynasidan keyin qayta keldi.
    /// Logout/parol almashtirish bilan bekor qilingan tokenlar bunga kirmaydi.</summary>
    private static bool IsReuse(RefreshToken stored, DateTimeOffset now)
        => stored is { RevokedAt: { } revokedAt, ReplacedByToken: not null } && now - revokedAt > AuthSecurity.RefreshReuseGracePeriod;

    /// <summary>Token zanjiri sizib chiqqan: foydalanuvchining BARCHA faol refresh tokenlari bekor qilinadi —
    /// hujumchi ham, haqiqiy egasi ham qayta kirishi kerak bo'ladi.</summary>
    private async Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.Revoke(now, AuthSecurity.RefreshReuseRevokeReason);

        logger.LogWarning(
            "Refresh token qayta ishlatildi (foydalanuvchi {UserId}): {Count} ta faol sessiya bekor qilindi",
            userId, active.Count);

        await db.SaveChangesAsync(cancellationToken);
    }
}
