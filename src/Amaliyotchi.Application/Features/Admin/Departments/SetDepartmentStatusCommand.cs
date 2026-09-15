using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>PATCH /api/admin/departments/{id}/status</c>: <c>{ isActive }</c> → 200 <see cref="DepartmentDto"/>.
/// <see cref="Id"/> route'dan keladi. Topilmasa → 404.</summary>
public sealed record SetDepartmentStatusCommand(Guid Id, bool IsActive) : IRequest<DepartmentDto>;

internal sealed class SetDepartmentStatusCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetDepartmentStatusCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(SetDepartmentStatusCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kafedra topilmadi.");

        if (department.IsActive != request.IsActive)
        {
            if (request.IsActive)
                department.Activate();
            else
                department.Deactivate();

            await audit.WriteAsync(
                request.IsActive ? AuditAction.DepartmentActivated : AuditAction.DepartmentDeactivated,
                nameof(Department), department.Id.ToString(),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        var facultyName = await db.Faculties.AsNoTracking()
            .Where(f => f.Id == department.FacultyId)
            .Select(f => f.Name)
            .FirstAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.FacultyId, facultyName, department.Name, department.Code, department.IsActive);
    }
}
