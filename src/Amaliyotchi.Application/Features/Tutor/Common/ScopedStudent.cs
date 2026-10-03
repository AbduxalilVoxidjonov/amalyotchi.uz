using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Common;

/// <summary>Ko'lamdagi talaba — tyutor ro'yxatlari va hisoblar uchun yengil proyeksiya
/// (User va Group navigatsiyalaridan; tracked emas). <c>Work*</c> — talabaning o'z ish vaqti ustunlari
/// (<see cref="StudentProfile.SetWorkHours"/>); kunga qarab qaysi biri amalda — <see cref="HoursOn"/>.</summary>
public sealed record ScopedStudent(
    Guid UserId,
    string FullName,
    string HemisId,
    Guid GroupId,
    string GroupName,
    int Course,
    StudentStatus Status,
    TimeOnly? WorkStart = null,
    TimeOnly? WorkEnd = null,
    DateOnly? WorkHoursEffectiveFrom = null,
    TimeOnly? PreviousWorkStart = null,
    TimeOnly? PreviousWorkEnd = null)
{
    /// <summary><paramref name="date"/> kuni amaldagi o'z ish vaqti; null — davr soatlari.</summary>
    public (TimeOnly Start, TimeOnly End)? HoursOn(DateOnly date)
        => StudentProfile.ResolveHours(WorkStart, WorkEnd, WorkHoursEffectiveFrom, PreviousWorkStart, PreviousWorkEnd, date);

    /// <summary>To'liq profildan (tracked entity) proyeksiya.</summary>
    public static ScopedStudent From(StudentProfile p)
        => new(p.UserId, p.User.FullName, p.HemisId, p.StudentGroupId, p.Group.Name, p.Group.Course, p.Status,
            p.WorkStart, p.WorkEnd, p.WorkHoursEffectiveFrom, p.PreviousWorkStart, p.PreviousWorkEnd);
}

internal static class ScopedStudentQueries
{
    public static IQueryable<ScopedStudent> SelectScoped(this IQueryable<StudentProfile> profiles)
        => profiles.Select(p => new ScopedStudent(
            p.UserId, p.User.FullName, p.HemisId, p.StudentGroupId, p.Group.Name, p.Group.Course, p.Status,
            p.WorkStart, p.WorkEnd, p.WorkHoursEffectiveFrom, p.PreviousWorkStart, p.PreviousWorkEnd));

    /// <summary>Ko'lamdagi talabalar manbasi — ro'yxat (<see cref="LoadScopedStudentsAsync"/>) va hisoblagich
    /// (<see cref="CountScopedStudentsAsync"/>) bir xil to'plamni ko'rsin.</summary>
    private static IQueryable<StudentProfile> ScopedProfiles(this IApplicationDbContext db, DataScope scope)
        => db.StudentProfiles.AsNoTracking().InScope(scope);

    /// <summary>Ko'lamdagi barcha talabalar, FISH bo'yicha tartiblangan.</summary>
    public static Task<List<ScopedStudent>> LoadScopedStudentsAsync(
        this IApplicationDbContext db, DataScope scope, CancellationToken cancellationToken)
        => db.ScopedProfiles(scope)
            .OrderBy(p => p.User.FullName)
            .ThenBy(p => p.UserId)
            .SelectScoped()
            .ToListAsync(cancellationToken);

    /// <summary><see cref="LoadScopedStudentsAsync"/> natijasi soni — xotiraga yuklamasdan (bitta COUNT).</summary>
    public static Task<int> CountScopedStudentsAsync(
        this IApplicationDbContext db, DataScope scope, CancellationToken cancellationToken)
        => db.ScopedProfiles(scope)
            .SelectScoped()
            .CountAsync(cancellationToken);
}
