using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResultDto>;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    ITokenService tokenService,
    ICurrentUser currentUser,
    IClock clock)
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken)
            ?? throw new ForbiddenException(TokenRejected);

        if (!stored.IsActive(now))
            throw new ForbiddenException(TokenRejected);

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
        var newRefreshToken = user.IssueRefreshToken(
            tokenService.CreateRefreshToken(),
            now.Add(tokenService.RefreshTokenLifetime),
            currentUser.IpAddress);

        stored.Revoke(now, "Yangilandi", newRefreshToken.Token);

        var accessToken = tokenService.CreateAccessToken(user);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            newRefreshToken.Token,
            UserSummaryDto.From(user));
    }

    private const string TokenRejected = "Sessiya muddati tugagan. Qaytadan kiring.";
}
