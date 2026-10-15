using Amaliyotchi.Application.Common.Caching;
using Amaliyotchi.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Students;

/// <summary>Talaba formasidagi guruh varianti.</summary>
public sealed record StudentGroupOption(Guid Id, string Name, int Course, string DirectionName, string FacultyName);

/// <summary><c>GET /api/admin/students/group-options?facultyId&amp;directionId&amp;course</c> — talaba qo'shish formasi uchun
/// guruhlar: import va yakka yaratish qabul qiladigan to'plamning AYNAN o'zi (<see cref="StudentImportGroups.Active"/> —
/// faol o'quv yilidagi faol guruh, zanjiri faol). Filtrlar ixtiyoriy, AND. Tartib: fakultet, yo'nalish, kurs, nom.</summary>
public sealed record GetStudentGroupOptionsQuery(Guid? FacultyId, Guid? DirectionId, int? Course)
    : IRequest<IReadOnlyList<StudentGroupOption>>;

internal sealed class GetStudentGroupOptionsQueryHandler(IApplicationDbContext db, IAppCache cache)
    : IRequestHandler<GetStudentGroupOptionsQuery, IReadOnlyList<StudentGroupOption>>
{
    private static readonly string[] Tags = [CacheTags.Organization];

    /// <summary>Ma'lumotnoma — filtr kombinatsiyasi bo'yicha keshlanadi; tuzilma yozuvi keshni darhol bekor qiladi.</summary>
    public async Task<IReadOnlyList<StudentGroupOption>> Handle(
        GetStudentGroupOptionsQuery request, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync<IReadOnlyList<StudentGroupOption>>(
            CacheKeys.StudentGroupOptions(request.FacultyId, request.DirectionId, request.Course),
            async ct => await LoadAsync(request, ct),
            CacheKeys.OrganizationTtl, Tags, cancellationToken);

    private async Task<List<StudentGroupOption>> LoadAsync(
        GetStudentGroupOptionsQuery request, CancellationToken cancellationToken)
    {
        var groups = StudentImportGroups.Active(db);

        if (request.FacultyId is { } facultyId)
            groups = groups.Where(x => x.Faculty.Id == facultyId);
        if (request.DirectionId is { } directionId)
            groups = groups.Where(x => x.Direction.Id == directionId);
        if (request.Course is { } course)
            groups = groups.Where(x => x.Group.Course == course);

        return await groups
            .OrderBy(x => x.Faculty.Name).ThenBy(x => x.Direction.Name).ThenBy(x => x.Group.Course).ThenBy(x => x.Group.Name)
            .Select(x => new StudentGroupOption(x.Group.Id, x.Group.Name, x.Group.Course, x.Direction.Name, x.Faculty.Name))
            .ToListAsync(cancellationToken);
    }
}
