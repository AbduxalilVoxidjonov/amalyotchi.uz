using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>POST /api/admin/departments/{departmentId}/directions</c>: <c>{ name, code }</c> → 201
/// <see cref="DirectionDto"/>. <see cref="DepartmentId"/> route'dan keladi. Kafedra topilmasa → 404. Kod
/// (shu kafedra ichida, faol yozuvlar orasida, case-insensitive) takrorlansa → 409.</summary>
public sealed record CreateDirectionCommand(Guid DepartmentId, string Name, string Code) : IRequest<DirectionDto>;

internal sealed class CreateDirectionCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<CreateDirectionCommand, DirectionDto>
{
    public async Task<DirectionDto> Handle(CreateDirectionCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            ?? throw new NotFoundException("Kafedra topilmadi.");

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await db.Directions.AnyAsync(
            d => d.DepartmentId == request.DepartmentId && d.Code == code, cancellationToken);
        if (exists)
            throw new ConflictException($"'{code}' kodli yo'nalish bu kafedrada allaqachon mavjud.");

        var direction = department.AddDirection(request.Name, request.Code);
        db.Directions.Add(direction);

        await audit.WriteAsync(
            AuditAction.DirectionCreated, nameof(Direction), direction.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var faculty = await db.Faculties.AsNoTracking()
            .Where(f => f.Id == department.FacultyId)
            .Select(f => new { f.Id, f.Name })
            .FirstAsync(cancellationToken);

        return new DirectionDto(
            direction.Id, department.Id, department.Name, faculty.Id, faculty.Name,
            direction.Name, direction.Code, direction.IsActive);
    }
}
