using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>DELETE /api/admin/directions/{id}</c> → 204. Soft delete: faqat yo'nalishning o'zi
/// arxivlanadi. Topilmasa (yoki allaqachon o'chirilgan) → 404. O'chirilmagan guruhi bo'lsa → 409 —
/// avval ularni o'chirish kerak.</summary>
public sealed record DeleteDirectionCommand(Guid Id) : IRequest;

internal sealed class DeleteDirectionCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteDirectionCommand>
{
    public async Task Handle(DeleteDirectionCommand request, CancellationToken cancellationToken)
    {
        var direction = await db.Directions.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yo'nalish topilmadi.");

        var hasGroups = await db.StudentGroups.AnyAsync(g => g.DirectionId == request.Id, cancellationToken);
        if (hasGroups)
            throw new ConflictException("Yo'nalishda guruhlar bor — avval ularni o'chiring.");

        direction.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.DirectionDeleted, nameof(Direction), direction.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
