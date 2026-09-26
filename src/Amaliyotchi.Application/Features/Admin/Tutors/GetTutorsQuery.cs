using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Kontrakt v2 <c>Tutor</c>: telefon E.164 xom, <c>faculties[]</c> (biriktirilgan fakultetlar, nom bo'yicha) +
/// <c>groups[]</c> (formatlash frontend'da), <paramref name="LastActiveAt"/> — oxirgi kirish yoki oxirgi audit yozuvi
/// (qaysi keyin bo'lsa), ISO.</summary>
public sealed record TutorRow(
    Guid Id,
    string FullName,
    string? Phone,
    IReadOnlyList<FacultyRef> Faculties,
    IReadOnlyList<string> Groups,
    int Students,
    int Pending,
    DateTimeOffset? OldestPendingAt,
    double? AvgDecisionHours,
    DateTimeOffset? LastActiveAt,
    bool IsActive,
    TutorStatus Status);

/// <summary><c>GET /api/admin/tutors?q&amp;facultyId&amp;page&amp;pageSize</c> — <c>q</c>: ism, telefon, fakultetlaridan birining
/// kodi/nomi; <see cref="FacultyId"/> — ixtiyoriy, fakultetlaridan biri shu bo'lgan tyutorlar.</summary>
public sealed record GetTutorsQuery : PagedQuery, IRequest<Paged<TutorRow>>
{
    public Guid? FacultyId { get; init; }
}

internal sealed class GetTutorsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetTutorsQuery, Paged<TutorRow>>
{
    public async Task<Paged<TutorRow>> Handle(GetTutorsQuery request, CancellationToken cancellationToken)
    {
        var tutors = Source(db);

        if (request.FacultyId is { } facultyId)
            tutors = tutors.Where(u => u.Faculties.Any(tf => tf.FacultyId == facultyId));

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            tutors = tutors.Where(u =>
                EF.Functions.Like(u.FullName.ToLower(), pattern, AdminSearch.Escape)
                || (u.PhoneNumber != null && EF.Functions.Like(u.PhoneNumber, pattern, AdminSearch.Escape))
                || u.Faculties.Any(tf => db.Faculties.Any(f => f.Id == tf.FacultyId
                                                                && (EF.Functions.Like(f.Code.ToLower(), pattern, AdminSearch.Escape)
                                                                    || EF.Functions.Like(f.Name.ToLower(), pattern, AdminSearch.Escape)))));
        }

        var page = await tutors
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.LastLoginAt, u.IsActive })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<TutorRow>.Empty(request);

        var now = clock.UtcNow;
        var ids = page.Items.Select(t => t.Id).ToList();
        var stats = await TutorStatsLoader.LoadAsync(db, ids, now, cancellationToken);
        var faculties = await TutorFacultyQueries.LoadRefsAsync(db, ids, cancellationToken);

        var rows = page.Items.Select(t =>
        {
            var s = stats.GetValueOrDefault(t.Id) ?? TutorStats.Empty;
            return new TutorRow(
                t.Id, t.FullName, t.PhoneNumber, faculties.GetValueOrDefault(t.Id) ?? [],
                s.Groups, s.StudentCount, s.PendingCount, s.OldestPendingAt, s.AvgDecisionHours,
                Latest(t.LastLoginAt, s.LastAuditAt), t.IsActive, s.Status(now));
        }).ToList();

        return new Paged<TutorRow>(rows, page.Page, page.PageSize, page.Total);
    }

    /// <summary>Ro'yxatning sukut (filtrsiz) to'plami — barcha tyutorlar (faol/nofaol). Sidebar hisoblagichi
    /// (<c>GET /api/admin/nav</c>) ham shu manbadan sanaydi, ro'yxat <c>total</c>i bilan mos bo'lishi uchun.</summary>
    internal static IQueryable<User> Source(IApplicationDbContext db)
        => db.Users.AsNoTracking().Where(u => u.Role == UserRole.Tutor);

    internal static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : a > b ? a : b;
}
