using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Ko'lamdagi talaba — tyutor ro'yxatlari va hisoblar uchun yengil proyeksiya
/// (User va Group navigatsiyalaridan; tracked emas).</summary>
public sealed record ScopedStudent(
    Guid UserId,
    string FullName,
    string HemisId,
    Guid GroupId,
    string GroupName,
    int Course,
    StudentStatus Status);

internal static class ScopedStudentQueries
{
    public static IQueryable<ScopedStudent> SelectScoped(this IQueryable<StudentProfile> profiles)
        => profiles.Select(p => new ScopedStudent(
            p.UserId, p.User.FullName, p.HemisId, p.StudentGroupId, p.Group.Name, p.Group.Course, p.Status));

    /// <summary>Ko'lamdagi barcha talabalar, FISH bo'yicha tartiblangan.</summary>
    public static Task<List<ScopedStudent>> LoadScopedStudentsAsync(
        this IApplicationDbContext db, DataScope scope, CancellationToken cancellationToken)
        => db.StudentProfiles
            .AsNoTracking()
            .InScope(scope)
            .OrderBy(p => p.User.FullName)
            .ThenBy(p => p.UserId)
            .SelectScoped()
            .ToListAsync(cancellationToken);
}
