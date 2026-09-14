using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Today;

/// <summary><c>GET /api/tutor/today?status&amp;q&amp;page&amp;pageSize</c> — ko'lamdagi talabalar × bugun.
/// Qatori bo'lgan kun bazadan, qolgani hisoblanadi (pending / absent / dayOff / excused).</summary>
public sealed record GetTutorTodayQuery : PagedQuery, IRequest<TodayResponse>
{
    public TodayFilter? Status { get; init; }
}

internal sealed class GetTutorTodayQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorTodayQuery, TodayResponse>
{
    public async Task<TodayResponse> Handle(GetTutorTodayQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var students = await db.LoadScopedStudentsAsync(scope, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var periodIds = periods.PeriodIds;

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
            .Select(a => new { a.StudentUserId, Company = a.Company.Name, a.Company.RadiusM })
            .ToListAsync(cancellationToken);
        var placementByStudent = placements
            .GroupBy(p => p.StudentUserId)
            .ToDictionary(g => g.Key, g => g.First());

        var diariesToday = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(d => d.Date == today)
            .Select(d => d.StudentUserId)
            .ToHashSetAsync(cancellationToken);

        var onLeave = await db.LeaveRequests.AsNoTracking().InScope(scope)
            .Where(l => l.Status == LeaveRequestStatus.Approved && l.DateFrom <= today && l.DateTo >= today)
            .Select(l => l.StudentUserId)
            .ToHashSetAsync(cancellationToken);

        var pendingLeaves = await db.LeaveRequests.AsNoTracking().InScope(scope)
            .CountAsync(l => l.Status == LeaveRequestStatus.Pending, cancellationToken);

        var newApplications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, cancellationToken);

        var rows = new List<AttendanceRow>(students.Count);
        foreach (var student in students)
        {
            var period = periods.ForGroup(student.GroupId);
            var row = attendance.GetValueOrDefault(student.UserId);
            var placement = placementByStudent.GetValueOrDefault(student.UserId);
            var rejectedDistance = rejectedAttempts.TryGetValue(student.UserId, out var d) ? d : (double?)null;

            var status = row?.Status
                ?? AttendanceStatusResolver.Resolve(period, today, today, localNow, onLeave.Contains(student.UserId));

            var isWorkDay = period is not null && period.IsWorkDay(today);
            DiaryState? diary = null;
            if (diariesToday.Contains(student.UserId))
                diary = DiaryState.Written;
            else if (isWorkDay && status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Pending)
                diary = DiaryState.Pending;

            var outOfRadius = row is null
                ? rejectedDistance is not null
                : placement is not null && row.CheckInDistanceM > placement.RadiusM;

            rows.Add(new AttendanceRow(
                student.UserId,
                student.FullName,
                student.GroupName,
                placement?.Company,
                row?.CheckInAt is { } checkIn ? PracticeTime.Hm(checkIn) : null,
                row?.CheckOutAt is { } checkOut ? PracticeTime.Hm(checkOut) : null,
                diary,
                row?.CheckInDistanceM ?? rejectedDistance,
                outOfRadius,
                status,
                row?.IsSuspicious ?? false,
                row?.IsManual ?? false,
                row?.AutoClosed ?? false));
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
            alerts.Add(new TodayAlert(TodayAlertKind.OutOfRadius, rejectedAttempts.Count, "/tutor/map", Math.Round(rejectedAttempts.Values.Max(), 0)));
        if (stats.Absent > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.NotCheckedIn, stats.Absent, "/tutor?status=absent"));
        if (pendingLeaves > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.NewLeaveRequests, pendingLeaves, "/tutor/leave-requests"));
        if (newApplications > 0)
            alerts.Add(new TodayAlert(TodayAlertKind.NewApplications, newApplications, "/tutor/applications"));

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

        if (request.Q is { } q)
            filtered = filtered.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

        return new TodayResponse(today, stats, alerts, filtered.ToList().ToPaged(request));
    }
}
