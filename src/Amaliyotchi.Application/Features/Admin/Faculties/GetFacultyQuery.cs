using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Faculties;

/// <summary><c>GET /api/admin/faculties/{id}</c> → <see cref="FacultyDto"/> (breadcrumb uchun). Topilmasa → 404.</summary>
public sealed record GetFacultyQuery(Guid Id) : IRequest<FacultyDto>;

internal sealed class GetFacultyQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFacultyQuery, FacultyDto>
{
    public async Task<FacultyDto> Handle(GetFacultyQuery request, CancellationToken cancellationToken)
        => await db.Faculties.AsNoTracking()
            .Where(f => f.Id == request.Id)
            .Select(f => new FacultyDto(f.Id, f.Name, f.Code, f.IsActive))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Fakultet topilmadi.");
}
