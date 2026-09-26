using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.ChangeLogin;

/// <param name="Available">Login bo'sh va format to'g'ri — <c>change-login</c> ga yuborish mumkin.</param>
/// <param name="Normalized"><c>HemisId.Normalize</c> natijasi (bo'shliqlarsiz); format noto'g'ri bo'lsa — kiritilgan
/// qiymat <c>Trim</c> qilingan holda.</param>
/// <param name="Reason"><c>Available = false</c> bo'lsa sababi (o'zbekcha), aks holda <c>null</c>.</param>
public sealed record LoginAvailabilityDto(bool Available, string Normalized, string? Reason);

/// <summary><c>GET /api/auth/login-available?login=</c> (AdminOnly): yangi login band emasligini oldindan tekshirish.
/// Har doim 200: format xatosi, band yoki o'zining joriy logini — <c>available = false</c> + <c>reason</c>.</summary>
public sealed record CheckLoginAvailableQuery(string? Login) : IRequest<LoginAvailabilityDto>;

internal sealed class CheckLoginAvailableQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CheckLoginAvailableQuery, LoginAvailabilityDto>
{
    public async Task<LoginAvailabilityDto> Handle(CheckLoginAvailableQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        if (LoginAvailability.FormatError(request.Login, out var normalized) is { } formatError)
            return new LoginAvailabilityDto(false, normalized, formatError);

        var current = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.HemisId)
            .FirstOrDefaultAsync(cancellationToken);
        if (current == normalized)
            return new LoginAvailabilityDto(false, normalized, LoginAvailability.OwnLoginMessage);

        return await LoginAvailability.IsTakenAsync(db, normalized, userId, cancellationToken)
            ? new LoginAvailabilityDto(false, normalized, LoginAvailability.TakenMessage)
            : new LoginAvailabilityDto(true, normalized, null);
    }
}
