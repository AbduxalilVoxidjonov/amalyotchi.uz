using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Tyutor fakultetlari (<c>TutorFaculty</c>) bo'yicha umumiy o'qishlar: ro'yxat/karta uchun <see cref="FacultyRef"/>lar,
/// buyruqlar uchun id to'plami. O'chirilgan fakultet (soft-delete filtri) ro'yxatga tushmaydi.</summary>
internal static class TutorFacultyQueries
{
    /// <summary>Har tyutor uchun fakultetlari — nom bo'yicha tartib. Fakultetsiz tyutor lug'atda bo'lmaydi.</summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FacultyRef>>> LoadRefsAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> tutorIds, CancellationToken cancellationToken)
    {
        if (tutorIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<FacultyRef>>();

        var rows = await (from tf in db.TutorFaculties.AsNoTracking()
                          join f in db.Faculties on tf.FacultyId equals f.Id
                          where tutorIds.Contains(tf.TutorUserId)
                          orderby f.Name, f.Code
                          select new { tf.TutorUserId, Ref = new FacultyRef(f.Id, f.Code, f.Name) })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.TutorUserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FacultyRef>)g.Select(r => r.Ref).ToList());
    }

    /// <summary>Bitta tyutorning fakultetlari — nom bo'yicha tartib.</summary>
    public static async Task<IReadOnlyList<FacultyRef>> LoadRefsAsync(
        IApplicationDbContext db, Guid tutorId, CancellationToken cancellationToken)
        => (await LoadRefsAsync(db, [tutorId], cancellationToken)).GetValueOrDefault(tutorId) ?? [];

    /// <summary>Tyutor biriktirilgan fakultet id'lari (o'chirilgan fakultetlar ham — bog'lanish qatori bor ekan).</summary>
    public static Task<List<Guid>> LoadIdsAsync(IApplicationDbContext db, Guid tutorId, CancellationToken cancellationToken)
        => db.TutorFaculties.AsNoTracking()
            .Where(tf => tf.TutorUserId == tutorId)
            .Select(tf => tf.FacultyId)
            .ToListAsync(cancellationToken);
}
