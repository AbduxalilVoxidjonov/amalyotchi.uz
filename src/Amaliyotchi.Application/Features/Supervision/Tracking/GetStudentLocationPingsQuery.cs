using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Supervision.Tracking;

/// <summary><c>GET /api/admin/students/{studentId}/location-pings?date=YYYY-MM-DD</c> — talabaning bir kunlik ping'lari
/// va xulosa. <c>date</c> berilmasa — bugun (Toshkent). Ko'lam — boshqa admin endpoint'lari bilan bir xil
/// (<see cref="ScopeQueries.GetScopedStudentAsync"/>): ko'lamdan tashqari yoki yo'q talaba → 404.</summary>
public sealed record GetStudentLocationPingsQuery(Guid StudentId, DateOnly? Date) : IRequest<StudentLocationPingsDto>;

internal sealed class GetStudentLocationPingsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetStudentLocationPingsQuery, StudentLocationPingsDto>
{
    public async Task<StudentLocationPingsDto> Handle(GetStudentLocationPingsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var date = request.Date ?? clock.LocalToday();

        var profile = await db.GetScopedStudentAsync(scope, request.StudentId, cancellationToken);
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);

        var row = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.StudentId && a.Date == date)
            .Select(a => new { a.PeriodId, a.CheckInAt, a.CheckOutAt })
            .FirstOrDefaultAsync(cancellationToken);

        TrackingPlaceDto? place = null;
        if (row is not null)
        {
            place = await db.PracticeApplications.AsNoTracking().InScope(scope)
                .Where(a => a.StudentUserId == request.StudentId && a.PeriodId == row.PeriodId && a.Status == ApplicationStatus.Approved)
                .OrderByDescending(a => a.DecidedAt)
                .Select(a => new TrackingPlaceDto(a.Company.Name, a.Company.Location.Latitude, a.Company.Location.Longitude, a.Company.RadiusM))
                .FirstOrDefaultAsync(cancellationToken);
        }

        var pings = await db.LocationPings.AsNoTracking().InScope(scope)
            .Where(p => p.StudentUserId == request.StudentId && p.Date == date)
            .OrderBy(p => p.OccurredAt)
            .Select(p => new LocationPingDto(
                p.OccurredAt, p.Location.Latitude, p.Location.Longitude, p.AccuracyM, p.DistanceM, p.Status, p.IsMocked, p.Source))
            .ToListAsync(cancellationToken);
        pings = pings.Select(p => p with { DistanceM = Math.Round(p.DistanceM) }).ToList();

        var samples = pings.Select(p => new PingSample(p.OccurredAt, p.Status, p.DistanceM, p.IsMocked)).ToList();
        var summary = new LocationPingsSummaryDto(
            pings.Count,
            pings.Count(p => p.Status == LocationPingStatus.Inside),
            pings.Count(p => p.Status == LocationPingStatus.Outside),
            pings.Count(p => p.Status == LocationPingStatus.Inaccurate),
            LocationTracking.OutsideMinutes(samples, settings.TrackingIntervalSec),
            LocationTracking.LongestGapMinutes(samples),
            pings.Count > 0 ? pings[^1].OccurredAt : null);

        return new StudentLocationPingsDto(
            date, place, row?.CheckInAt, row?.CheckOutAt, profile.HasTrackingConsent, pings, summary);
    }
}
