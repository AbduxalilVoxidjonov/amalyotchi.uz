using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>PUT /api/admin/departments/{id}</c>: <c>{ name, code }</c> → 200 <see cref="DepartmentDto"/>.
/// <see cref="Id"/> route'dan (body'da yo'q). Topilmasa → 404; shu fakultetdagi boshqa kafedraning kodi
/// bilan to'qnashsa → 409.</summary>
public sealed record UpdateDepartmentCommand(Guid Id, string Name, string Code) : IRequest<DepartmentDto>;

internal sealed class UpdateDepartmentCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Kafedra topilmadi.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (!string.Equals(department.Code, code, StringComparison.Ordinal))
        {
            var duplicate = await db.Departments.AnyAsync(
                d => d.Id != request.Id && d.FacultyId == department.FacultyId && d.Code == code, cancellationToken);
            if (duplicate)
                throw new ConflictException($"'{code}' kodli kafedra bu fakultetda allaqachon mavjud.");
        }

        department.Update(request.Name, request.Code);

        await audit.WriteAsync(
            AuditAction.DepartmentUpdated, nameof(Department), department.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var facultyName = await db.Faculties.AsNoTracking()
            .Where(f => f.Id == department.FacultyId)
            .Select(f => f.Name)
            .FirstAsync(cancellationToken);

        return new DepartmentDto(department.Id, department.FacultyId, facultyName, department.Name, department.Code, department.IsActive);
    }
}
