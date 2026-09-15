using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Groups;

/// <summary><c>GET /api/admin/directions/{directionId}/groups?q&amp;page&amp;pageSize</c>. <see cref="DirectionId"/>
/// route'dan keladi. Yo'nalish topilmasa → 404. Qator shakli global ro'yxat bilan bir xil (<see cref="GroupRow"/>).</summary>
public sealed record GetDirectionGroupsQuery(Guid DirectionId) : PagedQuery, IRequest<Paged<GroupRow>>;

internal sealed class GetDirectionGroupsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetDirectionGroupsQuery, Paged<GroupRow>>
{
    public async Task<Paged<GroupRow>> Handle(GetDirectionGroupsQuery request, CancellationToken cancellationToken)
    {
        var directionExists = await db.Directions.AnyAsync(d => d.Id == request.DirectionId, cancellationToken);
        if (!directionExists)
            throw new NotFoundException("Yo'nalish topilmadi.");

        return await GroupRowQueries.LoadPagedAsync(db, clock, request.DirectionId, request, cancellationToken);
    }
}
