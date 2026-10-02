using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Talaba yaratish (import va forma) uchun guruh: undan fakultet va kurs olinadi
/// (kurs talabada saqlanmaydi — u guruhdan kelib chiqadi).</summary>
internal sealed record ImportGroup(
    Guid GroupId, string Name, int Course, Guid FacultyId, Guid DirectionId, string Direction, string Department, string Faculty);

/// <summary>Faol guruh zanjiri qatori (EF kompozitsiyasi uchun init-xossali tur — filtr proyeksiyadan OLDIN qo'llanadi).</summary>
internal sealed class ActiveGroupSource
{
    public required StudentGroup Group { get; init; }
    public required Direction Direction { get; init; }
    public required Department Department { get; init; }
    public required Faculty Faculty { get; init; }
}

internal static class StudentImportGroups
{
    /// <summary>Faol o'quv yilidagi faol guruhlar — zanjirdagi fakultet/kafedra/yo'nalish ham faol bo'lishi kerak.
    /// Shablondagi ro'yxat, import paytidagi qidiruv, formadagi guruh variantlari va yakka talaba yaratish —
    /// hammasi ayni shu to'plamga tayanadi.</summary>
    public static IQueryable<ActiveGroupSource> Active(IApplicationDbContext db)
        => from g in db.StudentGroups.AsNoTracking()
           join d in db.Directions on g.DirectionId equals d.Id
           join dep in db.Departments on d.DepartmentId equals dep.Id
           join f in db.Faculties on dep.FacultyId equals f.Id
           join y in db.AcademicYears on g.AcademicYearId equals y.Id
           where g.IsActive && d.IsActive && dep.IsActive && f.IsActive && y.IsActive
           select new ActiveGroupSource { Group = g, Direction = d, Department = dep, Faculty = f };

    public static async Task<IReadOnlyList<ImportGroup>> LoadAsync(
        IApplicationDbContext db, CancellationToken cancellationToken)
        => await Project(Active(db).OrderBy(x => x.Faculty.Name).ThenBy(x => x.Group.Name))
            .ToListAsync(cancellationToken);

    /// <summary>Id bo'yicha faol guruh; topilmasa (yo'q, o'chirilgan yoki zanjirda nofaol) — <c>null</c>.</summary>
    public static Task<ImportGroup?> FindAsync(
        IApplicationDbContext db, Guid groupId, CancellationToken cancellationToken)
        => Project(Active(db).Where(x => x.Group.Id == groupId)).FirstOrDefaultAsync(cancellationToken);

    private static IQueryable<ImportGroup> Project(IQueryable<ActiveGroupSource> source)
        => source.Select(x => new ImportGroup(
            x.Group.Id, x.Group.Name, x.Group.Course, x.Faculty.Id,
            x.Direction.Id, x.Direction.Name, x.Department.Name, x.Faculty.Name));

    /// <summary>Guruh nomini solishtirish uchun kalit: bo'shliqlarsiz, katta-kichik harf farqsiz
    /// ("412-22", " 412-22 ", "412-22 " — bittasi).</summary>
    public static string Key(string name) => string.Concat(name.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    public static IReadOnlyList<StudentImportGroupRef> ToRefs(IReadOnlyList<ImportGroup> groups)
        => [.. groups.Select(g => new StudentImportGroupRef(g.Name, g.Course, g.Direction, g.Department, g.Faculty))];
}

/// <summary><c>GET /api/admin/students/import/template</c> → to'ldirish uchun <c>.xlsx</c> shablon
/// (ichida faol guruhlar ro'yxati bilan).</summary>
public sealed record GetStudentImportTemplateQuery : IRequest<byte[]>;

internal sealed class GetStudentImportTemplateQueryHandler(IApplicationDbContext db, IStudentImportExcel excel)
    : IRequestHandler<GetStudentImportTemplateQuery, byte[]>
{
    public async Task<byte[]> Handle(GetStudentImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var groups = await StudentImportGroups.LoadAsync(db, cancellationToken);
        return excel.BuildTemplate(StudentImportGroups.ToRefs(groups));
    }
}
