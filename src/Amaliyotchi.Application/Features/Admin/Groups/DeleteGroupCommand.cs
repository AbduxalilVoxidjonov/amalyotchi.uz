using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>DELETE /api/admin/groups/{id}</c> → 204. Soft delete. Topilmasa (yoki allaqachon
/// o'chirilgan) → 404. O'chirilmagan talabasi yoki aynan shu GURUH darajasidagi faol tyutor ko'lami bo'lsa → 409 —
/// avval ularni ko'chirish kerak. Ota ko'lamdan (fakultet/kafedra/yo'nalish) kelib chiqqan biriktiruvlar guruh bilan
/// birga faolsizlantiriladi (tarix saqlanadi).</summary>
public sealed record DeleteGroupCommand(Guid Id) : IRequest;

internal sealed class DeleteGroupCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteGroupCommand>
{
    public async Task Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Guruh topilmadi.");

        var hasStudents = await db.StudentProfiles.AnyAsync(p => p.StudentGroupId == request.Id, cancellationToken);
        var hasGroupScope = await db.TutorScopes.AnyAsync(
            s => s.Level == TutorScopeLevel.Group && s.StudentGroupId == request.Id && s.IsActive, cancellationToken);

        if (hasStudents || hasGroupScope)
            throw new ConflictException(
                "Guruhda talabalar yoki biriktirilgan tyutor bor — avval ularni ko'chiring.");

        var inherited = await db.TutorAssignments
            .Where(a => a.StudentGroupId == request.Id && a.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var assignment in inherited)
            assignment.Deactivate();

        group.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.GroupDeleted, nameof(StudentGroup), group.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
