using Amaliyotchi.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Import uchun guruh: nomi bo'yicha qidiriladi, undan fakultet va kurs olinadi
/// (kurs talabada saqlanmaydi — u guruhdan kelib chiqadi).</summary>
internal sealed record ImportGroup(
    Guid GroupId, string Name, int Course, Guid FacultyId, string Direction, string Department, string Faculty);

internal static class StudentImportGroups
{
    /// <summary>Faol o'quv yilidagi faol guruhlar — zanjirdagi fakultet/kafedra/yo'nalish ham faol bo'lishi kerak.
    /// Shablondagi ro'yxat ham, import paytidagi qidiruv ham ayni shu to'plamga tayanadi.</summary>
    public static async Task<IReadOnlyList<ImportGroup>> LoadAsync(
        IApplicationDbContext db, CancellationToken cancellationToken)
        => await (from g in db.StudentGroups.AsNoTracking()
                  join d in db.Directions on g.DirectionId equals d.Id
                  join dep in db.Departments on d.DepartmentId equals dep.Id
                  join f in db.Faculties on dep.FacultyId equals f.Id
                  join y in db.AcademicYears on g.AcademicYearId equals y.Id
                  where g.IsActive && d.IsActive && dep.IsActive && f.IsActive && y.IsActive
                  orderby f.Name, g.Name
                  select new ImportGroup(g.Id, g.Name, g.Course, f.Id, d.Name, dep.Name, f.Name))
            .ToListAsync(cancellationToken);

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
