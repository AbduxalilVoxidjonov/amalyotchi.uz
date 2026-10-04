using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary><c>GET /api/admin/admins</c> (bosh admin) → <c>{ items }</c>: barcha (o'chirilmagan) adminlar — bosh adminlar
/// birinchi, keyin FISH bo'yicha. Sahifalanmaydi (adminlar soni kichik).</summary>
public sealed record GetAdminsQuery : IRequest<AdminListDto>;

internal sealed class GetAdminsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetAdminsQuery, AdminListDto>
{
    public async Task<AdminListDto> Handle(GetAdminsQuery request, CancellationToken cancellationToken)
    {
        await AdminAccounts.EnsureSuperAdminAsync(db, currentUser, cancellationToken);

        var items = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin)
            .OrderByDescending(u => u.IsSuperAdmin).ThenBy(u => u.FullName).ThenBy(u => u.Id)
            .Select(AdminAccounts.Projection)
            .ToListAsync(cancellationToken);

        return new AdminListDto(items);
    }
}
