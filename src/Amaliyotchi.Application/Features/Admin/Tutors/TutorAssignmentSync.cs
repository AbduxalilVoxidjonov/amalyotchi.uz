using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Ko'lamlarni guruhlarga materializatsiya qilish: tyutorning faol <see cref="TutorScope"/>laridan kerakli
/// guruhlar to'plami hisoblanadi va <see cref="TutorAssignment"/>lar shunga keltiriladi — yo'qlari yaratiladi (faol
/// o'quv yili bilan), faolsizlantirilganlari qayta faollashadi, ortiqchalari faolsizlantiriladi (tarix saqlanadi).
/// Idempotent. <c>SaveChanges</c> chaqiruvchida.</summary>
internal static class TutorAssignmentSync
{
    public const string NoActiveAcademicYearMessage = "Faol o'quv yili yo'q.";

    /// <summary><paramref name="activeScopes"/> — xotiradagi (hali saqlanmagan bo'lishi mumkin) faol ko'lamlar;
    /// kerakli guruhlar = tyutor fakultetlarining (<paramref name="facultyIds"/>) faol guruhlari ichida ko'lamlardan biri
    /// (har biri o'z fakulteti bo'yicha) qamrab olganlari.</summary>
    public static async Task SyncAsync(
        IApplicationDbContext db,
        Guid tutorId,
        IReadOnlyCollection<Guid> facultyIds,
        IReadOnlyCollection<TutorScope> activeScopes,
        CancellationToken cancellationToken)
    {
        var wanted = new HashSet<Guid>();
        if (activeScopes.Count > 0)
        {
            var groups = await TutorScopeQueries.LoadActiveGroupsAsync(db, facultyIds, cancellationToken);
            foreach (var group in groups)
            {
                if (activeScopes.Any(s => s.CoversGroup(group.FacultyId, group.DepartmentId, group.DirectionId, group.Id)))
                    wanted.Add(group.Id);
            }
        }

        // Tyutorning barcha (faol va faolsizlantirilgan) biriktiruvlari — kuzatiladi, joyida o'zgartiriladi.
        var existing = await db.TutorAssignments
            .Where(a => a.TutorUserId == tutorId)
            .ToListAsync(cancellationToken);

        foreach (var assignment in existing)
        {
            var isWanted = wanted.Contains(assignment.StudentGroupId);
            if (isWanted && !assignment.IsActive)
                assignment.Activate();
            else if (!isWanted && assignment.IsActive)
                assignment.Deactivate();
        }

        var toCreate = wanted.Where(id => existing.All(a => a.StudentGroupId != id)).ToList();
        if (toCreate.Count == 0)
            return;

        var academicYearId = await ActiveAcademicYearIdAsync(db, cancellationToken);
        foreach (var groupId in toCreate)
            db.TutorAssignments.Add(TutorAssignment.Create(tutorId, groupId, academicYearId));
    }

    /// <summary>Yangi yaratilgan yoki qayta faollashtirilgan guruh uchun: uni qamrab oluvchi faol ko'lam (istalgan
    /// darajada) bo'lsa — o'sha tyutorga biriktiruv qo'shiladi (mavjud faolsizlantirilgani bo'lsa faollashadi).
    /// Kesishmaslik qoidasi tufayli bunday ko'lam ko'pi bilan bitta. <paramref name="academicYearId"/> — guruhning o'quv yili.</summary>
    public static async Task AttachGroupAsync(
        IApplicationDbContext db,
        Guid groupId,
        Guid directionId,
        Guid departmentId,
        Guid facultyId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var tutorId = await db.TutorScopes.AsNoTracking()
            .Where(s => s.IsActive
                        && ((s.Level == TutorScopeLevel.Faculty && s.FacultyId == facultyId)
                            || (s.Level == TutorScopeLevel.Department && s.DepartmentId == departmentId)
                            || (s.Level == TutorScopeLevel.Direction && s.DirectionId == directionId)
                            || (s.Level == TutorScopeLevel.Group && s.StudentGroupId == groupId)))
            .OrderBy(s => s.CreatedAt)
            .Select(s => (Guid?)s.TutorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tutorId is not { } id)
            return;

        var existing = await db.TutorAssignments
            .FirstOrDefaultAsync(a => a.TutorUserId == id && a.StudentGroupId == groupId, cancellationToken);
        if (existing is null)
            db.TutorAssignments.Add(TutorAssignment.Create(id, groupId, academicYearId));
        else if (!existing.IsActive)
            existing.Activate();
    }

    private static async Task<Guid> ActiveAcademicYearIdAsync(IApplicationDbContext db, CancellationToken cancellationToken)
        => await db.AcademicYears.AsNoTracking()
            .Where(y => y.IsActive)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException(NoActiveAcademicYearMessage);
}
