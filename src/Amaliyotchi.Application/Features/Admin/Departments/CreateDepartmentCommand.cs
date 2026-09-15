using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>POST /api/admin/faculties/{facultyId}/departments</c>: <c>{ name, code }</c> → 201 <see cref="DepartmentDto"/>.
/// <see cref="FacultyId"/> route'dan keladi. Fakultet topilmasa → 404. Kod (shu fakultet ichida, faol yozuvlar
/// orasida, case-insensitive) takrorlansa → 409.</summary>
public sealed record CreateDepartmentCommand(Guid FacultyId, string Name, string Code) : IRequest<DepartmentDto>;

internal sealed class CreateDepartmentCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var faculty = await db.Faculties.FirstOrDefaultAsync(f => f.Id == request.FacultyId, cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await db.Departments.AnyAsync(
            d => d.FacultyId == request.FacultyId && d.Code == code, cancellationToken);
        if (exists)
            throw new ConflictException($"'{code}' kodli kafedra bu fakultetda allaqachon mavjud.");

        var department = faculty.AddDepartment(request.Name, request.Code);
        db.Departments.Add(department);

        await audit.WriteAsync(
            AuditAction.DepartmentCreated, nameof(Department), department.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new DepartmentDto(department.Id, faculty.Id, faculty.Name, department.Name, department.Code, department.IsActive);
    }
}
