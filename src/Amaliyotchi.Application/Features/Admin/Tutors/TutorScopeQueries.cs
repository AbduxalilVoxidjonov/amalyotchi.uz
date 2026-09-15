using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Tyutor fakultetlaridagi FAOL guruh (yo'nalish → kafedra → fakultet zanjiri bilan) — ko'lam hisob-kitoblari
/// uchun umumiy qator.</summary>
internal sealed record FacultyGroupRow(
    Guid Id,
    string Name,
    int Course,
    Guid DirectionId,
    string DirectionName,
    Guid DepartmentId,
    string DepartmentName,
    Guid FacultyId,
    int Students);

/// <summary>Tanlangan tugunning nomi va ota yo'li (<c>"Fakultet › Kafedra"</c>).</summary>
internal sealed record TutorScopeNames(string Name, string Path);

/// <summary>Ko'lamlar bo'yicha umumiy o'qishlar: fakultetning faol guruhlari, tugun nomlari, DTO yig'ish.</summary>
internal static class TutorScopeQueries
{
    public const string PathSeparator = " › ";

    /// <summary>Daraja nomi — xabarlar uchun ("Fakultet", "Kafedra", "Yo'nalish", "Guruh").</summary>
    public static string LevelTitle(TutorScopeLevel level) => level switch
    {
        TutorScopeLevel.Faculty => "Fakultet",
        TutorScopeLevel.Department => "Kafedra",
        TutorScopeLevel.Direction => "Yo'nalish",
        _ => "Guruh"
    };

    /// <summary>Daraja nomi kichik harf bilan ("fakultet", "kafedra", "yo'nalish", "guruh").</summary>
    public static string LevelName(TutorScopeLevel level) => level switch
    {
        TutorScopeLevel.Faculty => "fakultet",
        TutorScopeLevel.Department => "kafedra",
        TutorScopeLevel.Direction => "yo'nalish",
        _ => "guruh"
    };

    /// <summary>Berilgan fakultetlardagi barcha faol (<c>StudentGroup.IsActive</c>) guruhlar — materializatsiya va hisoblar
    /// aynan shu to'plamga tayanadi. Kafedra/yo'nalishning faol emasligi guruhni chiqarib tashlamaydi.</summary>
    public static Task<List<FacultyGroupRow>> LoadActiveGroupsAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> facultyIds, CancellationToken cancellationToken)
        => (from g in db.StudentGroups.AsNoTracking()
            join d in db.Directions on g.DirectionId equals d.Id
            join dept in db.Departments on d.DepartmentId equals dept.Id
            where facultyIds.Contains(dept.FacultyId) && g.IsActive
            select new FacultyGroupRow(
                g.Id, g.Name, g.Course, d.Id, d.Name, dept.Id, dept.Name, dept.FacultyId,
                db.StudentProfiles.Count(p => p.StudentGroupId == g.Id)))
            .ToListAsync(cancellationToken);

    /// <summary>Har ko'lam uchun tugun nomi va ota yo'li (ko'pi bilan 4 ta so'rov). O'chirilgan tugun (soft-delete
    /// filtri) uchun nom bo'sh qoladi — bunday ko'lam faol bo'lmasligi kerak.</summary>
    public static async Task<IReadOnlyDictionary<Guid, TutorScopeNames>> LoadNamesAsync(
        IApplicationDbContext db, IReadOnlyCollection<TutorScope> scopes, CancellationToken cancellationToken)
    {
        var facultyIds = scopes.Select(s => s.FacultyId).Distinct().ToList();
        var departmentIds = scopes.Where(s => s.DepartmentId is not null).Select(s => s.DepartmentId!.Value).Distinct().ToList();
        var directionIds = scopes.Where(s => s.DirectionId is not null).Select(s => s.DirectionId!.Value).Distinct().ToList();
        var groupIds = scopes.Where(s => s.StudentGroupId is not null).Select(s => s.StudentGroupId!.Value).Distinct().ToList();

        var faculties = facultyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Faculties.AsNoTracking().Where(f => facultyIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);
        var departments = departmentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var directions = directionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Directions.AsNoTracking().Where(d => directionIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var groups = groupIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.StudentGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        var result = new Dictionary<Guid, TutorScopeNames>(scopes.Count);
        foreach (var scope in scopes)
        {
            var chain = new List<string> { faculties.GetValueOrDefault(scope.FacultyId, string.Empty) };
            if (scope.DepartmentId is { } departmentId)
                chain.Add(departments.GetValueOrDefault(departmentId, string.Empty));
            if (scope.DirectionId is { } directionId)
                chain.Add(directions.GetValueOrDefault(directionId, string.Empty));
            if (scope.StudentGroupId is { } groupId)
                chain.Add(groups.GetValueOrDefault(groupId, string.Empty));

            result[scope.Id] = new TutorScopeNames(chain[^1], string.Join(PathSeparator, chain.Take(chain.Count - 1)));
        }

        return result;
    }

    /// <summary>Faol ko'lamlar → <see cref="TutorScopeDto"/> (daraja → nom bo'yicha tartib). <paramref name="facultyGroups"/> —
    /// tyutor fakultetlarining faol guruhlari (<see cref="LoadActiveGroupsAsync"/>).</summary>
    public static async Task<IReadOnlyList<TutorScopeDto>> ToDtosAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<TutorScope> scopes,
        IReadOnlyCollection<FacultyGroupRow> facultyGroups,
        CancellationToken cancellationToken)
    {
        if (scopes.Count == 0)
            return [];

        var names = await LoadNamesAsync(db, scopes, cancellationToken);

        return scopes
            .Select(s =>
            {
                var covered = facultyGroups.Where(g => s.CoversGroup(g.FacultyId, g.DepartmentId, g.DirectionId, g.Id)).ToList();
                var name = names[s.Id];
                return new TutorScopeDto(
                    s.Id, s.Level, s.FacultyId, s.DepartmentId, s.DirectionId, s.StudentGroupId,
                    name.Name, name.Path, covered.Count, covered.Sum(g => g.Students));
            })
            .OrderBy(s => s.Level).ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }
}
