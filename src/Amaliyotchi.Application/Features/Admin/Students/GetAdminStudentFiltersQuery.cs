using Amaliyotchi.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

public sealed record StudentFilterFaculty(Guid Id, string Name);

/// <summary><paramref name="FacultyId"/> — yo'nalish kafedrasining fakulteti.</summary>
public sealed record StudentFilterDirection(Guid Id, string Name, Guid FacultyId);

/// <summary>Admin talabalar ro'yxati filtrlari variantlari.</summary>
public sealed record AdminStudentFiltersDto(
    IReadOnlyList<StudentFilterFaculty> Faculties,
    IReadOnlyList<StudentFilterDirection> Directions,
    IReadOnlyList<int> Courses);

/// <summary><c>GET /api/admin/students/filters</c> — variantlar admin "Fakultetlar/Yo'nalishlar" bo'limidagi
/// ma'lumotlardan dinamik olinadi: o'chirilmagan (soft delete global filtri) fakultet va yo'nalishlar — nofaollari ham
/// (eski talabalar filtrlana olsin), nom bo'yicha case-insensitive (ordinal) tartibda; kurslar — mavjud guruhlardagi noyob
/// <c>Course</c> qiymatlari, o'sish tartibida. 3 ta yengil SELECT.</summary>
public sealed record GetAdminStudentFiltersQuery : IRequest<AdminStudentFiltersDto>;

internal sealed class GetAdminStudentFiltersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAdminStudentFiltersQuery, AdminStudentFiltersDto>
{
    public async Task<AdminStudentFiltersDto> Handle(GetAdminStudentFiltersQuery request, CancellationToken cancellationToken)
    {
        var faculties = await db.Faculties
            .AsNoTracking()
            .Select(f => new StudentFilterFaculty(f.Id, f.Name))
            .ToListAsync(cancellationToken);

        var directions = await (
                from d in db.Directions.AsNoTracking()
                join dept in db.Departments on d.DepartmentId equals dept.Id
                select new StudentFilterDirection(d.Id, d.Name, dept.FacultyId))
            .ToListAsync(cancellationToken);

        var courses = await db.StudentGroups
            .AsNoTracking()
            .Select(g => g.Course)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        // Tartib xotirada: DB collation'iga (apostrof/tinish belgilarini e'tiborsiz qoldirishi mumkin) bog'liq bo'lmasin —
        // o'zbek nomlaridagi "o'", "g'" ham barqaror tartiblanadi. Ro'yxatlar kichik (o'nlab yozuv).
        return new AdminStudentFiltersDto(
            faculties.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, StringComparer.Ordinal).ThenBy(f => f.Id).ToList(),
            directions.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Name, StringComparer.Ordinal).ThenBy(d => d.Id).ToList(),
            courses);
    }
}
