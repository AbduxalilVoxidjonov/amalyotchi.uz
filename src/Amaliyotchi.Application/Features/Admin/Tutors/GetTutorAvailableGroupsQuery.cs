using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><c>GET /api/admin/tutors/{id}/available-groups</c> → tyutor fakultetidagi barcha FAOL guruhlar
/// (kafedra → yo'nalish → guruh nomi bo'yicha), har birida hozir faol biriktirilgan tyutor (bo'lsa).
/// Tyutor topilmasa → 404.</summary>
public sealed record GetTutorAvailableGroupsQuery(Guid Id) : IRequest<IReadOnlyList<AvailableGroupRow>>;

internal sealed class GetTutorAvailableGroupsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTutorAvailableGroupsQuery, IReadOnlyList<AvailableGroupRow>>
{
    public async Task<IReadOnlyList<AvailableGroupRow>> Handle(
        GetTutorAvailableGroupsQuery request, CancellationToken cancellationToken)
    {
        var facultyId = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.Id && u.Role == UserRole.Tutor)
            .Select(u => u.FacultyId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(TutorDetailQueries.NotFoundMessage);

        var groups = await (from g in db.StudentGroups.AsNoTracking()
                            join d in db.Directions on g.DirectionId equals d.Id
                            join dept in db.Departments on d.DepartmentId equals dept.Id
                            where dept.FacultyId == facultyId && g.IsActive
                            orderby dept.Name, d.Name, g.Name
                            select new
                            {
                                g.Id,
                                g.Name,
                                g.Course,
                                DirectionName = d.Name,
                                DepartmentName = dept.Name,
                                Students = db.StudentProfiles.Count(p => p.StudentGroupId == g.Id),
                                TutorId = db.TutorAssignments
                                    .Where(a => a.StudentGroupId == g.Id && a.IsActive)
                                    .OrderBy(a => a.CreatedAt)
                                    .Select(a => (Guid?)a.TutorUserId)
                                    .FirstOrDefault()
                            })
            .ToListAsync(cancellationToken);

        var tutorIds = groups.Where(g => g.TutorId is not null).Select(g => g.TutorId!.Value).Distinct().ToList();
        var tutorNames = tutorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking()
                .Where(u => tutorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return groups
            .Select(g => new AvailableGroupRow(
                g.Id, g.Name, g.Course, g.DirectionName, g.DepartmentName, g.Students,
                g.TutorId, g.TutorId is { } tutorId ? tutorNames.GetValueOrDefault(tutorId) : null))
            .ToList();
    }
}
