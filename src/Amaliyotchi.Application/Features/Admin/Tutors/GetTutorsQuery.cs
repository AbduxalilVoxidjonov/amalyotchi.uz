using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Tutors;

/// <summary>Kontrakt v2 <c>Tutor</c>: telefon E.164 xom, <c>facultyCode</c> + <c>groups[]</c> (formatlash frontend'da),
/// <paramref name="LastActiveAt"/> — oxirgi kirish yoki oxirgi audit yozuvi (qaysi keyin bo'lsa), ISO.</summary>
public sealed record TutorRow(
    Guid Id,
    string FullName,
    string? Phone,
    Guid? FacultyId,
    string? FacultyCode,
    string? FacultyName,
    IReadOnlyList<string> Groups,
    int Students,
    int Pending,
    DateTimeOffset? OldestPendingAt,
    double? AvgDecisionHours,
    DateTimeOffset? LastActiveAt,
    bool IsActive,
    TutorStatus Status);

/// <summary><c>GET /api/admin/tutors?q&amp;facultyId&amp;page&amp;pageSize</c> — <c>q</c>: ism, telefon, fakultet kodi/nomi;
/// <see cref="FacultyId"/> — ixtiyoriy, faqat shu fakultet tyutorlari.</summary>
public sealed record GetTutorsQuery : PagedQuery, IRequest<Paged<TutorRow>>
{
    public Guid? FacultyId { get; init; }
}

internal sealed class GetTutorsQueryHandler(IApplicationDbContext db, IClock clock)
    : IRequestHandler<GetTutorsQuery, Paged<TutorRow>>
{
    public async Task<Paged<TutorRow>> Handle(GetTutorsQuery request, CancellationToken cancellationToken)
    {
        var tutors = from u in db.Users.AsNoTracking()
                     join f in db.Faculties on u.FacultyId equals f.Id into faculties
                     from f in faculties.DefaultIfEmpty()
                     where u.Role == UserRole.Tutor
                     select new { User = u, Faculty = f };

        if (request.FacultyId is { } facultyId)
            tutors = tutors.Where(x => x.User.FacultyId == facultyId);

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            tutors = tutors.Where(x =>
                EF.Functions.Like(x.User.FullName.ToLower(), pattern, AdminSearch.Escape)
                || (x.User.PhoneNumber != null && EF.Functions.Like(x.User.PhoneNumber, pattern, AdminSearch.Escape))
                || (x.Faculty != null && (EF.Functions.Like(x.Faculty.Code.ToLower(), pattern, AdminSearch.Escape)
                                          || EF.Functions.Like(x.Faculty.Name.ToLower(), pattern, AdminSearch.Escape))));
        }

        var page = await tutors
            .OrderBy(x => x.User.FullName)
            .Select(x => new
            {
                x.User.Id,
                x.User.FullName,
                x.User.PhoneNumber,
                x.User.FacultyId,
                FacultyCode = x.Faculty != null ? x.Faculty.Code : null,
                FacultyName = x.Faculty != null ? x.Faculty.Name : null,
                x.User.LastLoginAt,
                x.User.IsActive
            })
            .ToPagedAsync(request, cancellationToken);

        if (page.Total == 0)
            return Paged<TutorRow>.Empty(request);

        var now = clock.UtcNow;
        var stats = await TutorStatsLoader.LoadAsync(db, page.Items.Select(t => t.Id).ToList(), now, cancellationToken);

        var rows = page.Items.Select(t =>
        {
            var s = stats.GetValueOrDefault(t.Id) ?? TutorStats.Empty;
            return new TutorRow(
                t.Id, t.FullName, t.PhoneNumber, t.FacultyId, t.FacultyCode, t.FacultyName,
                s.Groups, s.StudentCount, s.PendingCount, s.OldestPendingAt, s.AvgDecisionHours,
                Latest(t.LastLoginAt, s.LastAuditAt), t.IsActive, s.Status(now));
        }).ToList();

        return new Paged<TutorRow>(rows, page.Page, page.PageSize, page.Total);
    }

    internal static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : a > b ? a : b;
}
