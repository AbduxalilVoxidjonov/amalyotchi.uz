using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>PUT /api/admin/directions/{id}</c>: <c>{ name, code }</c> → 200 <see cref="DirectionDto"/>.
/// <see cref="Id"/> route'dan (body'da yo'q). Topilmasa → 404; shu kafedradagi boshqa yo'nalishning kodi
/// bilan to'qnashsa → 409.</summary>
public sealed record UpdateDirectionCommand(Guid Id, string Name, string Code) : IRequest<DirectionDto>;

internal sealed class UpdateDirectionCommandHandler(IApplicationDbContext db, IAuditWriter audit)
    : IRequestHandler<UpdateDirectionCommand, DirectionDto>
{
    public async Task<DirectionDto> Handle(UpdateDirectionCommand request, CancellationToken cancellationToken)
    {
        var direction = await db.Directions.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Yo'nalish topilmadi.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (!string.Equals(direction.Code, code, StringComparison.Ordinal))
        {
            var duplicate = await db.Directions.AnyAsync(
                d => d.Id != request.Id && d.DepartmentId == direction.DepartmentId && d.Code == code, cancellationToken);
            if (duplicate)
                throw new ConflictException($"'{code}' kodli yo'nalish bu kafedrada allaqachon mavjud.");
        }

        direction.Update(request.Name, request.Code);

        await audit.WriteAsync(
            AuditAction.DirectionUpdated, nameof(Direction), direction.Id.ToString(),
            cancellationToken: cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

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
