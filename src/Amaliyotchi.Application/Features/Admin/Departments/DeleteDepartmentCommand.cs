using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>DELETE /api/admin/departments/{id}</c> → 204. Soft delete: faqat kafedraning o'zi
/// arxivlanadi. Topilmasa (yoki allaqachon o'chirilgan) → 404. O'chirilmagan yo'nalishi bo'lsa → 409 —
/// avval ularni o'chirish kerak.</summary>
public sealed record DeleteDepartmentCommand(Guid Id) : IRequest;

internal sealed class DeleteDepartmentCommandHandler(IApplicationDbContext db, IAuditWriter audit, IClock clock)
    : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kafedra topilmadi.");

        var hasDirections = await db.Directions.AnyAsync(d => d.DepartmentId == request.Id, cancellationToken);
        if (hasDirections)
            throw new ConflictException("Kafedrada yo'nalishlar bor — avval ularni o'chiring.");

        department.Delete(clock.UtcNow);

        await audit.WriteAsync(
            AuditAction.DepartmentDeleted, nameof(Department), department.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
