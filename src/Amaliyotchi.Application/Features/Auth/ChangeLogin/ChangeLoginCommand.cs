using System.Text.Json;
using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Auth.ChangeLogin;

/// <summary><c>POST /api/auth/change-login</c> (AdminOnly): <c>{ newLogin, currentPassword, refreshToken? }</c> → 200
/// <see cref="UserSummaryDto"/>. Tekshiruv tartibi: format (400 <c>errors.NewLogin</c>) → joriy parol (400
/// <c>errors.CurrentPassword</c>) → joriy login bilan bir xil (400 <c>errors.NewLogin</c>) → band (409; xodimlar va talaba
/// profillari, o'chirilganlar ham). Access token'da login claim'i yo'q — joriy sessiya ishlashda davom etadi.
/// <see cref="RefreshToken"/> berilsa — u saqlanadi, qolgan refresh tokenlar bekor (parol almashtirishdagi siyosat);
/// berilmasa sessiyalarga tegilmaydi (login almashishi hisob sirini buzmaydi, joriy sessiya yashashi shart).</summary>
public sealed record ChangeLoginCommand(string NewLogin, string CurrentPassword, string? RefreshToken = null)
    : IRequest<UserSummaryDto>;

internal sealed class ChangeLoginCommandHandler(
    IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<ChangeLoginCommand, UserSummaryDto>
{
    public async Task<UserSummaryDto> Handle(ChangeLoginCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        if (LoginAvailability.FormatError(request.NewLogin, out var newLogin) is { } formatError)
            throw Invalid(nameof(ChangeLoginCommand.NewLogin), formatError);

        try
        {
            return await db.InTransactionAsync(async ct =>
            {
                // Bir xil loginni bir vaqtda ikki so'rov olib qo'ymasin: users jadvalida unikal indeks bor, ammo
                // student_profiles bilan kesishuvni indeks ushlamaydi — tranzaksiya oxirigacha login bo'yicha qulf.
                await db.AcquireTransactionLockAsync(LockKeyPrefix + newLogin, ct);

                return await ChangeAsync(request, userId, newLogin, ct);
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Poyga: tekshiruvdan keyin boshqa so'rov shu loginni egallab ulgurdi — unikal indeks ushladi.
            if (await LoginAvailability.IsTakenAsync(db, newLogin, userId, cancellationToken))
                throw new ConflictException(LoginAvailability.TakenMessage);
            throw;
        }
    }

    private async Task<UserSummaryDto> ChangeAsync(
        ChangeLoginCommand request, Guid userId, string newLogin, CancellationToken cancellationToken)
    {
        var keep = string.IsNullOrWhiteSpace(request.RefreshToken) ? null : request.RefreshToken;
        var revokeOthers = keep is not null;

        // Refresh tokenlar faqat joriy sessiya tokeni berilganda yuklanadi (u o'zi yuklanmaydi — bekor ham qilinmaydi).
        var user = await db.Users
            .WithSummary()
            .Include(u => u.RefreshTokens.Where(t => revokeOthers && t.RevokedAt == null && t.Token != keep))
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Foydalanuvchi topilmadi.");

        if (user.Role != UserRole.Admin)
            throw new ForbiddenException("Loginni faqat administrator almashtira oladi.");

        if (user.PasswordHash is null || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, out _))
            throw Invalid(nameof(ChangeLoginCommand.CurrentPassword), WrongCurrentPassword);

        if (user.HemisId == newLogin)
            throw Invalid(nameof(ChangeLoginCommand.NewLogin), User.SameHemisIdMessage);

        if (await LoginAvailability.IsTakenAsync(db, newLogin, userId, cancellationToken))
            throw new ConflictException(LoginAvailability.TakenMessage);

        var oldLogin = user.HemisId;
        user.ChangeHemisId(newLogin);
        if (revokeOthers)
            user.RevokeRefreshTokens(clock.UtcNow, "Login o'zgartirildi");

        await audit.WriteAsync(
            AuditAction.LoginChanged, nameof(User), user.Id.ToString(),
            changes: JsonSerializer.Serialize(new { oldLogin, newLogin }),
            userId: user.Id, userRole: user.Role,
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return UserSummaryDto.From(user);
    }

    private static ValidationException Invalid(string field, string message)
        => new(new Dictionary<string, string[]> { [field] = [message] }, message);

    private const string LockKeyPrefix = "login:";
    public const string WrongCurrentPassword = "Joriy parol noto'g'ri.";
}
