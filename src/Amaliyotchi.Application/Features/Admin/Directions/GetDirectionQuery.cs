using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>GET /api/admin/directions/{id}</c> → <see cref="DirectionDto"/>. Topilmasa → 404.</summary>
public sealed record GetDirectionQuery(Guid Id) : IRequest<DirectionDto>;

internal sealed class GetDirectionQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDirectionQuery, DirectionDto>
{
    public async Task<DirectionDto> Handle(GetDirectionQuery request, CancellationToken cancellationToken)
        => await (from dir in db.Directions.AsNoTracking()
                  join d in db.Departments.AsNoTracking() on dir.DepartmentId equals d.Id
                  join f in db.Faculties.AsNoTracking() on d.FacultyId equals f.Id
                  where dir.Id == request.Id
                  select new DirectionDto(dir.Id, d.Id, d.Name, f.Id, f.Name, dir.Name, dir.Code, dir.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Yo'nalish topilmadi.");
}
