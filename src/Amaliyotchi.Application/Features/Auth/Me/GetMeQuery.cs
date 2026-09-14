using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.Me;

public sealed record GetMeQuery : IRequest<UserSummaryDto>;

internal sealed class GetMeQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMeQuery, UserSummaryDto>
{
    public async Task<UserSummaryDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .SelectSummary()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Foydalanuvchi", userId);
    }
}
