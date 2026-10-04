using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.Faculties;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Supervision.Common;
using Amaliyotchi.Application.Features.Supervision.Diaries;
using Amaliyotchi.Application.Features.Supervision.Faces;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Nav;

/// <summary>Sidebar badge'lari — har biri tegishli sahifaning sukut (filtrsiz) holatidagi soni bilan AYNAN teng:
/// <list type="bullet">
/// <item><paramref name="Faculties"/>, <paramref name="Companies"/>, <paramref name="Students"/> — ro'yxatlarning <c>total</c>i;</item>
/// <item><paramref name="Today"/> — <c>GET /api/admin/today</c> → <c>stats.total</c> (bugungi jadval qatorlari);</item>
/// <item><paramref name="Applications"/> — <c>GET /api/admin/applications</c> → <c>counts.submitted</c> (ko'rib chiqilishi kerak);</item>
/// <item><paramref name="Diaries"/> — tekshirilmagan kundaliklar (<see cref="DiaryQueue.Unreviewed"/>: submitted/seen);</item>
/// <item><paramref name="PendingFaceEnrollments"/> — <c>GET /api/admin/face-enrollments</c> sukut (pending) ro'yxati uzunligi.</item>
/// </list></summary>
public sealed record AdminNavCounts(
    int Faculties,
    int Companies,
    int Students,
    int Today,
    int Applications,
    int Diaries,
    int PendingFaceEnrollments);

/// <summary>Header crumb konteksti: <paramref name="AcademicYear"/> — joriy (faol) o'quv yili nomi, yo'q bo'lsa <c>null</c>.</summary>
public sealed record AdminNavContext(string? AcademicYear);

public sealed record AdminNavDto(AdminNavCounts Counts, AdminNavContext Context);

/// <summary><c>GET /api/admin/nav</c> — har sahifada chaqiriladi, shuning uchun faqat COUNT va bitta skalyar so'rov.
/// Manbalar ro'yxat handler'larining o'zidan (<c>Source(db)</c>) olinadi — predikatlar nusxalanmaydi.</summary>
public sealed record GetAdminNavQuery : IRequest<AdminNavDto>;

internal sealed class GetAdminNavQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetAdminNavQuery, AdminNavDto>
{
    public async Task<AdminNavDto> Handle(GetAdminNavQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        // Bitta DbContext — so'rovlar ketma-ket (parallel emas).
        var faculties = await GetFacultiesQueryHandler.Source(db).CountAsync(cancellationToken);
        var companies = await GetCompaniesQueryHandler.Source(db).CountAsync(cancellationToken);
        var students = await GetAdminStudentsQueryHandler.Source(db).CountAsync(cancellationToken);

        // "Bugun" jadvali har talabaga bitta qator beradi (stats.total == talabalar).
        var today = await db.CountScopedStudentsAsync(scope, cancellationToken);

        var applications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, cancellationToken);

        // Ro'yxat (SelectReviewEntriesAsync) talaba profili bilan birlashtiriladi — hisob ham xuddi shunday.
        var diaries = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(DiaryQueue.Unreviewed)
            .Join(db.StudentProfiles, d => d.StudentUserId, p => p.UserId, (d, p) => d.Id)
            .CountAsync(cancellationToken);

        var pendingFaces = await db.CountPendingFaceEnrollmentsAsync(scope, cancellationToken);

        var academicYear = await PracticePeriodQueries.CurrentAcademicYear(db)
            .Select(y => y.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminNavDto(
            new AdminNavCounts(faculties, companies, students, today, applications, diaries, pendingFaces),
            new AdminNavContext(academicYear));
    }
}
