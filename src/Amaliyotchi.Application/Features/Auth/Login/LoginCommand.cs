using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Auth.Login;

/// <summary>HEMIS ID + parol bilan kirish. Avval xodim (admin/tyutor, <c>User.HemisId</c>) qidiriladi, topilmasa —
/// talaba (<c>StudentProfile.HemisId</c>, o'chirilmagan profil). Talaba faqat xodim unga parol o'rnatgan bo'lsa
/// kira oladi (brauzer rejimi); parolsiz hisob umumiy "HEMIS ID yoki parol noto'g'ri" xatosini oladi.</summary>
public sealed record LoginCommand(string HemisId, string Password) : IRequest<AuthResultDto>;

internal sealed class LoginCommandHandler(AuthSessionService sessions, IClock clock)
    : IRequestHandler<LoginCommand, AuthResultDto>
{
    public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await sessions.VerifyPasswordAsync(request.HemisId, request.Password, now, cancellationToken);
        return await sessions.IssueSessionAsync(user, now, auditReason: null, cancellationToken);
    }
}
