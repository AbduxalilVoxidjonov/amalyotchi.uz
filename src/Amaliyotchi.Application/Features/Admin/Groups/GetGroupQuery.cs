using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>GET /api/admin/groups/{id}</c> → <see cref="GroupDto"/>. Topilmasa → 404.</summary>
public sealed record GetGroupQuery(Guid Id) : IRequest<GroupDto>;

internal sealed class GetGroupQueryHandler(IApplicationDbContext db) : IRequestHandler<GetGroupQuery, GroupDto>
{
    public async Task<GroupDto> Handle(GetGroupQuery request, CancellationToken cancellationToken)
        => await (from g in db.StudentGroups.AsNoTracking()
                  join y in db.AcademicYears.AsNoTracking() on g.AcademicYearId equals y.Id
                  where g.Id == request.Id
                  select new GroupDto(g.Id, g.DirectionId, g.Name, g.Course, g.IsActive, y.Name))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Guruh topilmadi.");
}
