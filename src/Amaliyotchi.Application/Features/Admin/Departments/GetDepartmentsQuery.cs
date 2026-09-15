using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Departments;

/// <summary><c>GET /api/admin/faculties/{facultyId}/departments?q&amp;page&amp;pageSize</c> — <c>q</c>: nom yoki kod.
/// <see cref="FacultyId"/> route'dan keladi. Fakultet topilmasa → 404.</summary>
public sealed record GetDepartmentsQuery(Guid FacultyId) : PagedQuery, IRequest<Paged<DepartmentRow>>;

internal sealed class GetDepartmentsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDepartmentsQuery, Paged<DepartmentRow>>
{
    public async Task<Paged<DepartmentRow>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var facultyExists = await db.Faculties.AnyAsync(f => f.Id == request.FacultyId, cancellationToken);
        if (!facultyExists)
            throw new NotFoundException("Fakultet topilmadi.");

        var departments = db.Departments.AsNoTracking().Where(d => d.FacultyId == request.FacultyId);
        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            departments = departments.Where(d =>
                EF.Functions.Like(d.Name.ToLower(), pattern, AdminSearch.Escape)
                || EF.Functions.Like(d.Code.ToLower(), pattern, AdminSearch.Escape));
        }

        var page = await departments
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Code,
                d.IsActive,
                Directions = d.Directions.Count,
                Groups = d.Directions.SelectMany(x => x.Groups).Count()
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<DepartmentRow>.Empty(request);

        var ids = page.Items.Select(d => d.Id).ToList();

        var studentsByDepartment = await (from p in db.StudentProfiles.AsNoTracking()
                                          join g in db.StudentGroups on p.StudentGroupId equals g.Id
                                          join dir in db.Directions on g.DirectionId equals dir.Id
                                          where ids.Contains(dir.DepartmentId)
                                          group p by dir.DepartmentId into grp
                                          select new { DepartmentId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count, cancellationToken);

        var rows = page.Items
            .Select(d => new DepartmentRow(
                d.Id, d.Name, d.Code, d.IsActive, d.Directions, d.Groups, studentsByDepartment.GetValueOrDefault(d.Id)))
            .ToList();

        return new Paged<DepartmentRow>(rows, page.Page, page.PageSize, page.Total);
    }
}
