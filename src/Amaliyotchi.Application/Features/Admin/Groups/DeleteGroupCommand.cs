using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>DELETE /api/admin/groups/{id}</c> → 204. Soft delete. Topilmasa (yoki allaqachon
/// o'chirilgan) → 404. O'chirilmagan talabasi bo'lsa → 409 — avval ularni ko'chirish kerak.</summary>
public sealed record DeleteGroupCommand(Guid Id) : IRequest;

internal sealed class DeleteGroupCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteGroupCommand>
{
    public async Task Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await db.StudentGroups.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Guruh topilmadi.");

        var hasStudents = await db.StudentProfiles.AnyAsync(p => p.StudentGroupId == request.Id, cancellationToken);
        if (hasStudents)
            throw new ConflictException("Guruhda talabalar bor — avval ularni ko'chiring.");

        group.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.GroupDeleted, nameof(StudentGroup), group.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
