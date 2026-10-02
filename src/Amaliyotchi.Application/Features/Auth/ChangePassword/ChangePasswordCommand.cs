using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.ChangePassword;

/// <summary><c>POST /api/auth/change-password</c>: <c>{ currentPassword, newPassword, refreshToken? }</c> → 204.
/// Joriy foydalanuvchi (har qanday rol, paroli bor) o'z parolini almashtiradi: <c>MustChangePassword=false</c>,
/// boshqa sessiyalarning refresh tokenlari bekor qilinadi. <see cref="RefreshToken"/> — joriy sessiyaniki
/// (ixtiyoriy): berilsa u saqlanadi, berilmasa BARCHA refresh tokenlar bekor (qayta kirish). Parol almashgach security
/// stamp o'zgaradi — eski access token darhol 401 oladi; klient saqlangan refresh token bilan yangilaydi.
/// Joriy parol noto'g'ri yoki hisobda parol yo'q → 400 <c>errors.CurrentPassword</c>.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, string? RefreshToken = null) : IRequest;

internal sealed class ChangePasswordCommandHandler(
    IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var keep = RefreshTokenHash.OfOptional(request.RefreshToken);

        // Joriy sessiya tokeni (berilgan bo'lsa) yuklanmaydi — shuning uchun bekor ham qilinmaydi.
        var user = await db.Users
            .Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null && t.Token != keep))
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Foydalanuvchi topilmadi.");

        if (user.PasswordHash is null || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, out _))
            throw new ValidationException(
                new Dictionary<string, string[]> { [nameof(ChangePasswordCommand.CurrentPassword)] = [WrongCurrentPassword] },
                WrongCurrentPassword);

        var now = clock.UtcNow;
        user.ChangeOwnPassword(passwordHasher.Hash(request.NewPassword));
        user.RevokeRefreshTokens(now, "Parol o'zgartirildi");

        await audit.WriteAsync(
            AuditAction.PasswordChanged, nameof(User), user.Id.ToString(),
            userId: user.Id, userRole: user.Role,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public const string WrongCurrentPassword = "Joriy parol noto'g'ri.";
}
