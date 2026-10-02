using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

internal sealed class LogoutCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Faqat o'zining tokenini bekor qila oladi — begona token bilan boshqa sessiyani o'chirib bo'lmaydi.
        var tokenHash = RefreshTokenHash.Of(request.RefreshToken ?? string.Empty);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(
                t => t.Token == tokenHash && t.UserId == currentUser.UserId,
                cancellationToken);

        // Token topilmasa ham xato qaytarilmaydi: chiqish har doim muvaffaqiyatli tugaydi.
        if (stored is null)
            return;

        stored.Revoke(clock.UtcNow, "Foydalanuvchi tizimdan chiqdi");
        await db.SaveChangesAsync(cancellationToken);
    }
}
