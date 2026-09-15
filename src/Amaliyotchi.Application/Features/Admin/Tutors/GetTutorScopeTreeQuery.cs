using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>GET /api/admin/tutors/{id}/scope-tree</c> → tyutor fakultetining daraxti: faqat FAOL kafedra/yo'nalish/guruhlar,
/// nom bo'yicha tartib (guruh: kurs, nom). Har tugunda AYNAN shu tugunda faol ko'lami bor tyutor (so'ralayotganning o'zi ham),
/// yo'q bo'lsa null. Tyutor topilmasa → 404.</summary>
public sealed record GetTutorScopeTreeQuery(Guid Id) : IRequest<TutorScopeTree>;

internal sealed class GetTutorScopeTreeQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTutorScopeTreeQuery, TutorScopeTree>
{
    public async Task<TutorScopeTree> Handle(GetTutorScopeTreeQuery request, CancellationToken cancellationToken)
    {
        var faculty = await (from u in db.Users.AsNoTracking()
                             join f in db.Faculties on u.FacultyId equals f.Id
                             where u.Id == request.Id && u.Role == UserRole.Tutor
                             select new { f.Id, f.Name, f.Code })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.FacultyId == faculty.Id && d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        var departmentIds = departments.Select(d => d.Id).ToList();
        var directions = await db.Directions.AsNoTracking()
            .Where(d => departmentIds.Contains(d.DepartmentId) && d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.DepartmentId })
            .ToListAsync(cancellationToken);

        var directionIds = directions.Select(d => d.Id).ToList();
        var groups = await db.StudentGroups.AsNoTracking()
            .Where(g => directionIds.Contains(g.DirectionId) && g.IsActive)
            .OrderBy(g => g.Course).ThenBy(g => g.Name)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Course,
                g.DirectionId,
                Students = db.StudentProfiles.Count(p => p.StudentGroupId == g.Id)
            })
            .ToListAsync(cancellationToken);

        // Fakultetdagi barcha faol ko'lamlar (tyutor kim bo'lishidan qat'i nazar) → tugun bo'yicha egasi.
        var owners = await (from s in db.TutorScopes.AsNoTracking()
                            join u in db.Users on s.TutorUserId equals u.Id
                            where s.IsActive && s.FacultyId == faculty.Id
                            orderby s.CreatedAt
                            select new { s.Level, s.FacultyId, s.DepartmentId, s.DirectionId, s.StudentGroupId, u.Id, u.FullName })
            .ToListAsync(cancellationToken);

        var byNode = new Dictionary<(TutorScopeLevel, Guid), (Guid Id, string Name)>();
        foreach (var o in owners)
        {
            var nodeId = o.Level switch
            {
                TutorScopeLevel.Faculty => o.FacultyId,
                TutorScopeLevel.Department => o.DepartmentId!.Value,
                TutorScopeLevel.Direction => o.DirectionId!.Value,
                _ => o.StudentGroupId!.Value
            };
            byNode.TryAdd((o.Level, nodeId), (o.Id, o.FullName));
        }

        (Guid? Id, string? Name) Owner(TutorScopeLevel level, Guid nodeId)
            => byNode.TryGetValue((level, nodeId), out var owner) ? (owner.Id, owner.Name) : (null, null);

        var tree = departments
            .Select(dept =>
            {
                var (deptTutorId, deptTutorName) = Owner(TutorScopeLevel.Department, dept.Id);
                var dirs = directions
                    .Where(d => d.DepartmentId == dept.Id)
                    .Select(d =>
                    {
                        var (dirTutorId, dirTutorName) = Owner(TutorScopeLevel.Direction, d.Id);
                        var grs = groups
                            .Where(g => g.DirectionId == d.Id)
                            .Select(g =>
                            {
                                var (gTutorId, gTutorName) = Owner(TutorScopeLevel.Group, g.Id);
                                return new TutorScopeTreeGroup(g.Id, g.Name, g.Course, g.Students, gTutorId, gTutorName);
                            })
                            .ToList();
                        return new TutorScopeTreeDirection(d.Id, d.Name, dirTutorId, dirTutorName, grs);
                    })
                    .ToList();
                return new TutorScopeTreeDepartment(dept.Id, dept.Name, deptTutorId, deptTutorName, dirs);
            })
            .ToList();

        var (facultyTutorId, facultyTutorName) = Owner(TutorScopeLevel.Faculty, faculty.Id);
        return new TutorScopeTree(faculty.Id, faculty.Name, faculty.Code, facultyTutorId, facultyTutorName, tree);
    }
}
