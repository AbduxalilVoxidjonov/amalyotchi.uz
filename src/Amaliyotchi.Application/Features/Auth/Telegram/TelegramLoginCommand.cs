using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Telegram;

/// <summary>Talaba uchun Telegram Mini App orqali kirish. <c>initData</c> — <c>window.Telegram.WebApp.initData</c>
/// xom satri; server HMAC imzosini tekshiradi va <c>user.id</c> bo'yicha hisobni topadi.</summary>
public sealed record TelegramLoginCommand(string InitData) : IRequest<AuthResultDto>;

internal sealed class TelegramLoginCommandHandler(
    IApplicationDbContext db,
    ITelegramInitDataValidator validator,
    ITokenService tokenService,
    IAuditWriter audit,
    ICurrentUser currentUser,
    IClock clock)
    : IRequestHandler<TelegramLoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(TelegramLoginCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (!validator.TryValidate(request.InitData, now, out var tgUser, out var error) || tgUser is null)
        {
            // Sabab faqat audit/log uchun — mijozga imzo nima uchun rad etilgani aytilmaydi.
            throw await LoginFailedAsync(null, $"Telegram initData rad etildi: {error}", SignatureRejected, cancellationToken);
        }

        // Faqat muddati o'tgan tokenlar yuklanadi — ular shu yerda tozalanadi.
        var user = await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => t.ExpiresAt <= now))
            .FirstOrDefaultAsync(u => u.TelegramUserId == tgUser.Id && u.Role == UserRole.Student, cancellationToken);

        if (user is null)
        {
            throw await LoginFailedAsync(
                null, $"Telegram hisobi bog'lanmagan (telegramId={tgUser.Id})",
                AccountNotFound, cancellationToken);
        }

        if (!user.IsActive)
        {
            throw await LoginFailedAsync(
                user, "Hisob faol emas",
                "Hisobingiz faol emas. Tyutoringizga murojaat qiling.", cancellationToken);
        }

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
            reason: "Telegram",
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto(
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken.Token,
            UserSummaryDto.From(user),
            user.MustChangePassword);
    }

    /// <summary>Muvaffaqiyatsiz urinish audit jurnaliga DARHOL yoziladi — tashlanadigan xato
    /// tufayli yozuv yo'qolib ketmasligi uchun.</summary>
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

    private const string SignatureRejected = "Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching.";
    private const string AccountNotFound = "Hisob topilmadi — tyutoringizdan taklif havolasini oling.";
}
