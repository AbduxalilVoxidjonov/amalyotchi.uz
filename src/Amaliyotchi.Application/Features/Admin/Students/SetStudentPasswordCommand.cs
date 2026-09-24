using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary><c>POST /api/admin/students/{id}/password</c> va <c>POST /api/tutor/students/{id}/password</c>:
/// <c>{ password }</c> → 204. <see cref="Id"/> route'dan. Talabaga brauzer orqali (HEMIS ID + parol) kirish uchun
/// vaqtinchalik parol o'rnatiladi (<c>MustChangePassword=true</c>) va barcha faol refresh tokenlari bekor qilinadi.
/// Ko'lam: admin — hamma, tyutor — biriktirilgan guruhlari; tashqarisida (yoki yo'q) → 404.
/// Uslub <see cref="ResetTutorPasswordCommand"/> bilan bir xil.</summary>
public sealed record SetStudentPasswordCommand(Guid Id, string Password) : IRequest;

internal sealed class SetStudentPasswordCommandHandler(
    IApplicationDbContext db, IScopeResolver scopeResolver, IPasswordHasher passwordHasher, IAuditWriter audit, IClock clock)
    : IRequestHandler<SetStudentPasswordCommand>
{
    public async Task Handle(SetStudentPasswordCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → 404.
        var exists = await db.StudentProfiles.AsNoTracking().InScope(scope)
            .AnyAsync(p => p.UserId == request.Id, cancellationToken);
        if (!exists)
            throw new NotFoundException("Talaba", request.Id);

        var student = await db.Users
            .Include(u => u.RefreshTokens.Where(t => t.RevokedAt == null))
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.Role == UserRole.Student, cancellationToken)
            ?? throw new NotFoundException("Talaba", request.Id);

        student.SetTemporaryPassword(passwordHasher.Hash(request.Password));
        student.RevokeRefreshTokens(clock.UtcNow, "Parol xodim tomonidan o'rnatildi");

        await audit.WriteAsync(
            AuditAction.StudentPasswordSet, nameof(User), student.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
