using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Directions;

/// <summary><c>GET /api/admin/departments/{departmentId}/directions?q&amp;page&amp;pageSize</c> — <c>q</c>: nom yoki kod.
/// <see cref="DepartmentId"/> route'dan keladi. Kafedra topilmasa → 404.</summary>
public sealed record GetDirectionsQuery(Guid DepartmentId) : PagedQuery, IRequest<Paged<DirectionRow>>;

internal sealed class GetDirectionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDirectionsQuery, Paged<DirectionRow>>
{
    public async Task<Paged<DirectionRow>> Handle(GetDirectionsQuery request, CancellationToken cancellationToken)
    {
        var departmentExists = await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, cancellationToken);
        if (!departmentExists)
            throw new NotFoundException("Kafedra topilmadi.");

        var directions = db.Directions.AsNoTracking().Where(d => d.DepartmentId == request.DepartmentId);
        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            directions = directions.Where(d =>
                EF.Functions.Like(d.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(d.Code.ToLower(), pattern, AdminSearch.Escape));
        }

        var page = await directions
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Code,
                d.IsActive,
                Groups = d.Groups.Count
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<DirectionRow>.Empty(request);

        var ids = page.Items.Select(d => d.Id).ToList();

        var studentsByDirection = await db.StudentProfiles.AsNoTracking()
            .Join(db.StudentGroups.AsNoTracking(), p => p.StudentGroupId, g => g.Id, (p, g) => g)
            .Where(g => ids.Contains(g.DirectionId))
            .GroupBy(g => g.DirectionId)
            .Select(grp => new { DirectionId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.DirectionId, x => x.Count, cancellationToken);

        var rows = page.Items
            .Select(d => new DirectionRow(d.Id, d.Name, d.Code, d.IsActive, d.Groups, studentsByDirection.GetValueOrDefault(d.Id)))
            .ToList();

        return new Paged<DirectionRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
