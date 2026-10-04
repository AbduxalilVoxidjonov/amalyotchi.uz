using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary><c>PATCH /api/admin/admins/{id}/status</c> (bosh admin): <c>{ isActive }</c> → 200 <see cref="AdminAccountDto"/>.
/// <c>false</c> — hisob bloklanadi va barcha faol refresh tokenlari bekor qilinadi (access token ham stamp orqali darhol
/// rad etiladi). Bosh adminni yoki o'zini bloklash → 409. Holat o'zgarmasa — audit yozilmaydi. Topilmasa → 404.</summary>
public sealed record SetAdminStatusCommand(Guid Id, bool IsActive) : IRequest<AdminAccountDto>;

internal sealed class SetAdminStatusCommandHandler(IApplicationDbContext db, ICurrentUser currentUser, IAuditWriter audit)
    : IRequestHandler<SetAdminStatusCommand, AdminAccountDto>
{
    public async Task<AdminAccountDto> Handle(SetAdminStatusCommand request, CancellationToken cancellationToken)
    {
        var actorId = await AdminAccounts.EnsureSuperAdminAsync(db, currentUser, cancellationToken);
        var admin = await AdminAccounts.LoadAdminAsync(db, request.Id, withActiveTokens: true, cancellationToken);

        if (!request.IsActive)
        {
            if (admin.Id == actorId)
                throw new ConflictException(User.SelfBlockMessage);
            if (admin.IsSuperAdmin)
                throw new ConflictException(User.SuperAdminBlockMessage);
        }

        if (admin.IsActive == request.IsActive)
            return AdminAccounts.ToDto(admin);

        if (request.IsActive)
            admin.Activate();
        else
            admin.Deactivate();

        await audit.WriteAsync(
            AuditAction.AdminStatusChanged, nameof(User), admin.Id.ToString(),
            changes: JsonSerializer.Serialize(new { isActive = request.IsActive }),
            cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return AdminAccounts.ToDto(admin);
    }
}
