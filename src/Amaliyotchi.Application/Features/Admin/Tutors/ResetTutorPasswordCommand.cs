using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>POST /api/admin/tutors/{id}/password</c>: <c>{ password }</c> → 204. <see cref="Id"/> route'dan.
/// Yangi parol o'rnatiladi va tyutorning barcha faol refresh tokenlari bekor qilinadi — eski sessiyalar
/// yangilanmaydi, qayta kirish kerak. Topilmasa → 404.</summary>
public sealed record ResetTutorPasswordCommand(Guid Id, string Password) : IRequest;

internal sealed class ResetTutorPasswordCommandHandler(
    IApplicationDbContext db, IPasswordHasher passwordHasher, IAuditWriter audit, IClock clock)
    : IRequestHandler<ResetTutorPasswordCommand>
{
    public async Task Handle(ResetTutorPasswordCommand request, CancellationToken cancellationToken)
    {
        var tutor = await db.Users
            .Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.Role == UserRole.Tutor, cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        tutor.SetPasswordHash(passwordHasher.Hash(request.Password));
        tutor.RevokeRefreshTokens(clock.UtcNow, "Parol administrator tomonidan tiklandi");

        await audit.WriteAsync(
            AuditAction.TutorPasswordReset, nameof(User), tutor.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
