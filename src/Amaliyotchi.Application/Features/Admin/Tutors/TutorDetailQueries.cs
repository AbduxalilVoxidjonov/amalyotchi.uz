using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary><see cref="TutorDetail"/> ni yig'ish — GET, yaratish, tahrirlash va ko'lam almashtirish bir xil
/// shaklni qaytaradi, mantiq shu yerda. Topilmasa yoki roli tyutor bo'lmasa → 404.</summary>
internal static class TutorDetailQueries
{
    public const string NotFoundMessage = "Tyutor topilmadi.";

    public static async Task<TutorDetail> LoadAsync(IApplicationDbContext db, Guid tutorId, CancellationToken cancellationToken)
    {
        var tutor = await db.Users.AsNoTracking()
            .Where(u => u.Id == tutorId && u.Role == UserRole.Tutor)
            .Select(u => new { u.Id, u.FullName, u.HemisId, u.PhoneNumber, u.IsActive, u.LastLoginAt, u.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        var faculties = await TutorFacultyQueries.LoadRefsAsync(db, tutorId, cancellationToken);

        var groups = await (from a in db.TutorAssignments.AsNoTracking()
                            join g in db.StudentGroups on a.StudentGroupId equals g.Id
                            join d in db.Directions on g.DirectionId equals d.Id
                            join y in db.AcademicYears on a.AcademicYearId equals y.Id
                            where a.TutorUserId == tutorId && a.IsActive
                            orderby d.Name, g.Name
                            select new TutorGroupDto(
                                a.Id, g.Id, g.Name, g.Course, d.Name,
                                db.StudentProfiles.Count(p => p.StudentGroupId == g.Id),
                                y.Name, g.IsActive))
            .ToListAsync(cancellationToken);

        var scopes = await db.TutorScopes.AsNoTracking()
            .Where(s => s.TutorUserId == tutorId && s.IsActive)
            .ToListAsync(cancellationToken);
        var facultyGroups = scopes.Count == 0
            ? []
            : await TutorScopeQueries.LoadActiveGroupsAsync(db, faculties.Select(f => f.Id).ToList(), cancellationToken);
        var scopeDtos = await TutorScopeQueries.ToDtosAsync(db, scopes, facultyGroups, cancellationToken);

        return new TutorDetail(
            tutor.Id, tutor.FullName, tutor.HemisId ?? string.Empty, tutor.PhoneNumber, faculties,
            tutor.IsActive, tutor.LastLoginAt, tutor.CreatedAt, scopeDtos, groups);
    }
}
