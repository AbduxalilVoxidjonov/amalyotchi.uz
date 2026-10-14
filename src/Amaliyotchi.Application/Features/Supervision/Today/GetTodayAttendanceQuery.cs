using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Tracking;
using Amaliyotchi.Application.Features.Supervision.Common;
using Amaliyotchi.Application.Features.Supervision.Tracking;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Supervision.Today;

/// <summary><c>GET /api/admin/today?status&amp;q&amp;page&amp;pageSize</c> — ko'lamdagi talabalar × bugun.
/// Qatori bo'lgan kun bazadan, qolgani hisoblanadi (pending / absent / dayOff / excused).</summary>
public sealed record GetTodayAttendanceQuery : PagedQuery, IRequest<TodayResponse>
{
    public TodayFilter? Status { get; init; }

    /// <summary><c>?presence=outside|signalLost</c> — live joylashuv holati bo'yicha filtr (ixtiyoriy).</summary>
    public TodayPresenceFilter? Presence { get; init; }
}

internal sealed class GetTodayAttendanceQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTodayAttendanceQuery, TodayResponse>
{
    public async Task<TodayResponse> Handle(GetTodayAttendanceQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var students = await db.LoadScopedStudentsAsync(scope, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var periodIds = periods.DefaultPeriodIds(students.Select(s => s.GroupId));

        var attendance = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(a => a.Date == today)
            .SelectSnapshot()
            .ToDictionaryAsync(a => a.StudentUserId, cancellationToken);

        var rejectedAttempts = await db.AttendanceEvents.AsNoTracking().InScope(scope)
            .Where(e => e.Date == today && !e.Accepted && e.RejectReason == CheckInRejectReason.OutOfRadius)
            .GroupBy(e => e.StudentUserId)
            .Select(g => new { StudentUserId = g.Key, MaxDistanceM = g.Max(e => e.DistanceM) })
            .ToDictionaryAsync(x => x.StudentUserId, x => x.MaxDistanceM, cancellationToken);

        var placements = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .Where(a => a.Status == ApplicationStatus.Approved && periodIds.Contains(a.PeriodId))
            .Select(a => new { a.StudentUserId, a.PeriodId, Company = a.Company.Name, a.Company.RadiusM })
            .ToListAsync(cancellationToken);
        var placementByStudent = placements
            .GroupBy(p => (p.StudentUserId, p.PeriodId))
            .ToDictionary(g => g.Key, g => g.First());

        // Ko'rsatiladigan korxona — faqat aktiv (bugun davom etayotgan ochiq davrdagi tasdiqlangan ariza); radius
        // tekshiruvi esa davomat davrining arizasidan (davom etayotgan davr bo'lsa — ikkalasi bir xil).
        var activeCompanies = await db.LoadActiveCompaniesAsync(
            students.Select(s => s.UserId).ToList(), today, cancellationToken);

        var diariesToday = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(d => d.Date == today)
            .Select(d => d.StudentUserId)
            .ToHashSetAsync(cancellationToken);

        var onLeave = await db.LeaveRequests.AsNoTracking().InScope(scope)
            .Where(l => l.Status == LeaveRequestStatus.Approved && l.DateFrom <= today && l.DateTo >= today)
            .Select(l => l.StudentUserId)
            .ToHashSetAsync(cancellationToken);

        var newApplications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, cancellationToken);

        // Live joylashuv (presence): bugungi barcha ping'lar BITTA so'rov bilan (tor proyeksiya), talaba bo'yicha
        // xotirada guruhlanadi — N+1 yo'q. Kuzatuv sozlamada o'chiq bo'lsa so'rov ham yo'q (presence = null).
        var settings = await db.LoadStudentSettingsAsync(cancellationToken);
        var now = clock.UtcNow;
        ILookup<Guid, PingSample> pingsByStudent = Array.Empty<PingSample>().ToLookup(_ => Guid.Empty);
        HashSet<Guid> consented = [];
        if (settings.TrackingEnabled)
        {
            var pings = await db.LocationPings.AsNoTracking().InScope(scope)
                .Where(p => p.Date == today)
                .OrderBy(p => p.StudentUserId).ThenBy(p => p.OccurredAt)
                .Select(p => new { p.StudentUserId, p.OccurredAt, p.Status, p.DistanceM, p.IsMocked })
                .ToListAsync(cancellationToken);
            pingsByStudent = pings.ToLookup(
                p => p.StudentUserId, p => new PingSample(p.OccurredAt, p.Status, p.DistanceM, p.IsMocked));

            consented = await db.StudentProfiles.AsNoTracking().InScope(scope)
                .Where(p => p.TrackingConsentAt != null)
                .Select(p => p.UserId)
                .ToHashSetAsync(cancellationToken);
        }

        var rows = new List<AttendanceRow>(students.Count);
        foreach (var student in students)
        {
            var period = periods.ForGroup(student.GroupId);
            var row = attendance.GetValueOrDefault(student.UserId);
            var placement = placementByStudent.GetValueOrDefault((student.UserId, period?.Period.Id ?? Guid.Empty));
            var rejectedDistance = rejectedAttempts.TryGetValue(student.UserId, out var d) ? d : (double?)null;

            var status = row?.Status
                ?? AttendanceStatusResolver.Resolve(
                    period, today, today, localNow, onLeave.Contains(student.UserId), student.HoursOn(today));

            var isWorkDay = period is not null && period.IsWorkDay(today);
            DiaryState? diary = null;
            if (diariesToday.Contains(student.UserId))
                diary = DiaryState.Written;
            else if (isWorkDay && status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Pending)
                diary = DiaryState.Pending;

            PresenceDto? presence = null;
            if (settings.TrackingEnabled && row?.CheckInAt is { } checkInAt)
            {
                var rowPeriod = periods.ForPeriod(row.PeriodId) ?? period;
                var rules = rowPeriod?.RulesFor(student.HoursOn(today)) ?? CheckInRules.Default;
                presence = PresenceDto.From(LocationTracking.Evaluate(new PresenceInput(
                    checkInAt, row.CheckOutAt, TrackingState.AutoCloseMoment(today, rules), row.CheckInDistanceM,
                    consented.Contains(student.UserId), pingsByStudent[student.UserId].ToList(), now,
                    settings.TrackingIntervalSec)));
            }

            var outOfRadius = row is null
                ? rejectedDistance is not null
                : placement is not null && row.CheckInDistanceM > placement.RadiusM;

            rows.Add(new AttendanceRow(
                student.UserId,
                student.FullName,
                student.GroupName,
                activeCompanies.GetValueOrDefault(student.UserId)?.Name,
                row?.CheckInAt is { } checkIn ? PracticeTime.Hm(checkIn) : null,
                row?.CheckOutAt is { } checkOut ? PracticeTime.Hm(checkOut) : null,
                diary,
                row?.CheckInDistanceM ?? rejectedDistance,
                outOfRadius,
                status,
                row?.IsSuspicious ?? false,
                row?.IsManual ?? false,
                row?.AutoClosed ?? false,
                presence));
        }

        var stats = new TodayStats(
            rows.Count(r => r.Status == AttendanceStatus.Present),
            rows.Count(r => r.Status == AttendanceStatus.Late),
            rows.Count(r => r.Status == AttendanceStatus.Absent),
            rows.Count(r => r.Status == AttendanceStatus.Excused),
            rows.Count(r => r.Status == AttendanceStatus.Pending),
            rows.Count(r => r.Diary == DiaryState.Written),
            rows.Count);

        var alerts = new List<TodayAlert>();
        if (rejectedAttempts.Count > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.OutOfRadius, rejectedAttempts.Count, "/admin/map", Math.Round(rejectedAttempts.Values.Max(), 0)));
        if (stats.Absent > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.NotCheckedIn, stats.Absent, "/admin/today?status=absent"));
        if (newApplications > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.NewApplications, newApplications, "/admin/applications"));

        IEnumerable<AttendanceRow> filtered = rows;
        if (request.Status is { } filter)
        {
            filtered = filter switch
            {
                TodayFilter.Suspicious => filtered.Where(r => r.Suspicious || r.OutOfRadius),
                TodayFilter.Present => filtered.Where(r => r.Status == AttendanceStatus.Present),
                TodayFilter.Late => filtered.Where(r => r.Status == AttendanceStatus.Late),
                TodayFilter.Absent => filtered.Where(r => r.Status == AttendanceStatus.Absent),
                TodayFilter.Excused => filtered.Where(r => r.Status == AttendanceStatus.Excused),
                TodayFilter.Pending => filtered.Where(r => r.Status == AttendanceStatus.Pending),
                _ => filtered
            };
        }

        if (request.Presence is { } presenceFilter)
        {
            var wanted = presenceFilter == TodayPresenceFilter.Outside ? PresenceState.Outside : PresenceState.SignalLost;
            filtered = filtered.Where(r => r.Presence?.State == wanted);
        }

        if (request.Q is { } q)
            filtered = filtered.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

        return new TodayResponse(today, stats, alerts, filtered.ToList().ToPaged(request));
    }
}
