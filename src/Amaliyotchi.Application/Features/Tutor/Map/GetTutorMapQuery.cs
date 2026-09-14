using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Map;

public enum MapPointKind
{
    Ok = 1,
    Late = 2,
    Bad = 3
}

/// <param name="Time">"HH:mm" (Toshkent) — urinish server tomonidan qabul qilingan vaqt.</param>
public sealed record MapPoint(
    Guid StudentId,
    string Name,
    string Company,
    double DistanceM,
    int RadiusM,
    bool Rejected,
    string Time,
    MapPointKind Kind,
    double Lat,
    double Lng);

public sealed record MapResponse(DateOnly Date, IReadOnlyList<MapPoint> Points);

/// <summary><c>GET /api/tutor/map?date=</c> (berilmasa — bugun). Har talaba uchun shu kundagi oxirgi check-in urinishi
/// (qabul qilingan yoki rad etilgan). <c>kind</c>: rad → bad, kech kelgan → late, aks holda ok.</summary>
public sealed record GetTutorMapQuery(DateOnly? Date) : IRequest<MapResponse>;

internal sealed class GetTutorMapQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorMapQuery, MapResponse>
{
    public async Task<MapResponse> Handle(GetTutorMapQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var date = request.Date ?? clock.LocalToday();

        var events = await db.AttendanceEvents.AsNoTracking().InScope(scope)
            .Where(e => e.Date == date && e.Kind == AttendanceEventKind.CheckIn)
            .OrderBy(e => e.StudentUserId)
            .ThenByDescending(e => e.ReceivedAt)
            .Select(e => new
            {
                e.StudentUserId,
                e.CompanyId,
                e.DistanceM,
                e.RadiusM,
                e.Accepted,
                e.ReceivedAt,
                e.Location.Latitude,
                e.Location.Longitude
            })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
            return new MapResponse(date, []);

        var latest = events.GroupBy(e => e.StudentUserId).Select(g => g.First()).ToList();
        var studentIds = latest.Select(e => e.StudentUserId).ToList();
        var companyIds = latest.Select(e => e.CompanyId).Distinct().ToList();

        var students = await db.StudentProfiles.AsNoTracking().InScope(scope)
            .Where(p => studentIds.Contains(p.UserId))
            .SelectScoped()
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        var companies = await db.Companies.AsNoTracking()
            .Where(c => companyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var lateStudents = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(a => a.Date == date && a.Status == AttendanceStatus.Late && studentIds.Contains(a.StudentUserId))
            .Select(a => a.StudentUserId)
            .ToHashSetAsync(cancellationToken);

        var points = latest
            .Where(e => students.ContainsKey(e.StudentUserId))
            .Select(e => new MapPoint(
                e.StudentUserId,
                students[e.StudentUserId].FullName,
                companies.GetValueOrDefault(e.CompanyId, string.Empty),
                Math.Round(e.DistanceM, 0),
                e.RadiusM,
                !e.Accepted,
                PracticeTime.Hm(e.ReceivedAt),
                !e.Accepted ? MapPointKind.Bad : lateStudents.Contains(e.StudentUserId) ? MapPointKind.Late : MapPointKind.Ok,
                e.Latitude,
                e.Longitude))
            .OrderBy(p => p.Name)
            .ToList();

        return new MapResponse(date, points);
    }
}
