using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Admins;

/// <summary><c>POST /api/admin/admins/{id}/password</c> (bosh admin) → 200 <c>{ temporaryPassword }</c>. Server vaqtinchalik
/// parol yaratadi (<c>mustChangePassword=true</c>) va adminning barcha faol refresh tokenlarini bekor qiladi — qayta
/// kirish kerak. Topilmasa → 404.</summary>
public sealed record ResetAdminPasswordCommand(Guid Id) : IRequest<AdminPasswordResetDto>;

internal sealed class ResetAdminPasswordCommandHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IPasswordHasher passwordHasher, IAuditWriter audit, IClock clock)
    : IRequestHandler<ResetAdminPasswordCommand, AdminPasswordResetDto>
{
    public async Task<AdminPasswordResetDto> Handle(ResetAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        await AdminAccounts.EnsureSuperAdminAsync(db, currentUser, cancellationToken);
        var admin = await AdminAccounts.LoadAdminAsync(db, request.Id, withActiveTokens: true, cancellationToken);

        var password = TemporaryPasswords.Generate();
        admin.SetTemporaryPassword(passwordHasher.Hash(password));
        admin.RevokeRefreshTokens(clock.UtcNow, "Parol bosh admin tomonidan tiklandi");

        await audit.WriteAsync(AuditAction.AdminPasswordReset, nameof(User), admin.Id.ToString(), cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new AdminPasswordResetDto(password);
    }
}
