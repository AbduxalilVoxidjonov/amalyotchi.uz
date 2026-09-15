using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>PATCH /api/admin/directions/{id}/status</c>: <c>{ isActive }</c> → 200 <see cref="DirectionDto"/>.
/// <see cref="Id"/> route'dan keladi. Topilmasa → 404.</summary>
public sealed record SetDirectionStatusCommand(Guid Id, bool IsActive) : IRequest<DirectionDto>;

internal sealed class SetDirectionStatusCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<SetDirectionStatusCommand, DirectionDto>
{
    public async Task<DirectionDto> Handle(SetDirectionStatusCommand request, CancellationToken cancellationToken)
    {
        var direction = await db.Directions.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yo'nalish topilmadi.");

        if (direction.IsActive != request.IsActive)
        {
            if (request.IsActive)
                direction.Activate();
            else
                direction.Deactivate();

            await audit.WriteAsync(
                request.IsActive ? AuditAction.DirectionActivated : AuditAction.DirectionDeactivated,
                nameof(Direction), direction.Id.ToString(),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        var parent = await (from d in db.Departments.AsNoTracking()
                            join f in db.Faculties.AsNoTracking() on d.FacultyId equals f.Id
                            where d.Id == direction.DepartmentId
                            select new { Department = d, Faculty = f })
            .FirstAsync(cancellationToken);

        return new DirectionDto(
            direction.Id, parent.Department.Id, parent.Department.Name, parent.Faculty.Id, parent.Faculty.Name,
            direction.Name, direction.Code, direction.IsActive);
    }
}
