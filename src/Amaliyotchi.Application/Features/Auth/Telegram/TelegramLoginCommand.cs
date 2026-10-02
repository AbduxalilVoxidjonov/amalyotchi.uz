using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Telegram;

/// <summary>Talaba uchun Telegram Mini App orqali kirish. <c>initData</c> — <c>window.Telegram.WebApp.initData</c>
/// xom satri; server HMAC imzosini tekshiradi va <c>user.id</c> bo'yicha hisobni topadi.</summary>
public sealed record TelegramLoginCommand(string InitData) : IRequest<AuthResultDto>;

internal sealed class TelegramLoginCommandHandler(
    IApplicationDbContext db,
    ITelegramInitDataValidator validator,
    AuthSessionService sessions,
    IClock clock)
    : IRequestHandler<TelegramLoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(TelegramLoginCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (!validator.TryValidate(request.InitData, now, out var tgUser, out var error) || tgUser is null)
        {
            // Sabab faqat audit/log uchun — mijozga imzo nima uchun rad etilgani aytilmaydi.
            throw await sessions.LoginFailedAsync(
                null, $"Telegram initData rad etildi: {error}",
                AuthSessionService.TelegramSignatureRejected, cancellationToken);
        }

        // Faqat muddati o'tgan tokenlar yuklanadi — ular shu yerda tozalanadi.
        var user = await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => t.ExpiresAt <= now))
            .FirstOrDefaultAsync(u => u.TelegramUserId == tgUser.Id && u.Role == UserRole.Student, cancellationToken);

        if (user is null)
        {
            throw await sessions.LoginFailedAsync(
                null, $"Telegram hisobi bog'lanmagan (telegramId={tgUser.Id})",
                AccountNotFound, cancellationToken);
        }

        if (!user.IsActive)
        {
            throw await sessions.LoginFailedAsync(
                user, "Hisob faol emas",
                "Hisobingiz faol emas. Tyutoringizga murojaat qiling.", cancellationToken);
        }

        // Mini App'dan kirdi va bot unga yoza oladi — "botni bloklagan" belgisi eskirgan (xabarlar yana yetkaziladi).
        if (tgUser.AllowsWriteToPm)
            user.ClearBotBlocked();

        return await sessions.IssueSessionAsync(user, now, auditReason: "Telegram", cancellationToken);
    }

    private const string AccountNotFound = "Hisob topilmadi — tyutoringizdan taklif havolasini oling.";
}
