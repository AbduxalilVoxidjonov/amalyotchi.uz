using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.Faculties;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Admin.Tutors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Nav;

/// <summary>Sidebar badge'lari — har biri tegishli ro'yxat sahifasining sukut (filtrsiz) <c>total</c>i bilan AYNAN teng.</summary>
public sealed record AdminNavCounts(int Faculties, int Tutors, int Companies, int Students);

/// <summary>Header crumb konteksti: <paramref name="AcademicYear"/> — joriy (faol) o'quv yili nomi, yo'q bo'lsa <c>null</c>.</summary>
public sealed record AdminNavContext(string? AcademicYear);

public sealed record AdminNavDto(AdminNavCounts Counts, AdminNavContext Context);

/// <summary><c>GET /api/admin/nav</c> — har sahifada chaqiriladi, shuning uchun faqat COUNT va bitta skalyar so'rov.
/// Manbalar ro'yxat handler'larining o'zidan (<c>Source(db)</c>) olinadi — predikatlar nusxalanmaydi.</summary>
public sealed record GetAdminNavQuery : IRequest<AdminNavDto>;

internal sealed class GetAdminNavQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAdminNavQuery, AdminNavDto>
{
    public async Task<AdminNavDto> Handle(GetAdminNavQuery request, CancellationToken cancellationToken)
    {
        // Bitta DbContext — so'rovlar ketma-ket (parallel emas).
        var faculties = await GetFacultiesQueryHandler.Source(db).CountAsync(cancellationToken);
        var tutors = await GetTutorsQueryHandler.Source(db).CountAsync(cancellationToken);
        var companies = await GetCompaniesQueryHandler.Source(db).CountAsync(cancellationToken);
        var students = await GetAdminStudentsQueryHandler.Source(db).CountAsync(cancellationToken);

        var academicYear = await PracticePeriodQueries.CurrentAcademicYear(db)
            .Select(y => y.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminNavDto(
            new AdminNavCounts(faculties, tutors, companies, students),
            new AdminNavContext(academicYear));
    }
}
