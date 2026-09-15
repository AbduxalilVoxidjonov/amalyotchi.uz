using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>GET /api/admin/departments/{id}</c> → <see cref="DepartmentDto"/>. Topilmasa → 404.</summary>
public sealed record GetDepartmentQuery(Guid Id) : IRequest<DepartmentDto>;

internal sealed class GetDepartmentQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepartmentQuery, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(GetDepartmentQuery request, CancellationToken cancellationToken)
        => await (from d in db.Departments.AsNoTracking()
                  join f in db.Faculties.AsNoTracking() on d.FacultyId equals f.Id
                  where d.Id == request.Id
                  select new DepartmentDto(d.Id, d.FacultyId, f.Name, d.Name, d.Code, d.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Kafedra topilmadi.");
}
