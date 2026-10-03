using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary><c>GET /api/tutor/students/{id}/attendance?periodId=&amp;from=&amp;to=</c> — kun-bakun davomat tarixi
/// tanlangan davr bo'yicha (<c>periodId</c> berilmasa — sukut bo'yicha davr, profil bilan bir xil qoida).
/// <c>from</c> berilmasa — davr boshidan, <c>to</c> berilmasa — <c>min(bugun, davr oxiri)</c> gacha; ikkalasi ham
/// davr chegaralariga qisiladi (oy navigatsiyasi davrdan chiqmaydi). Davr bo'lmasa — bo'sh massiv (xato emas).
/// Ko'lamdan tashqari talaba yoki talabaga tegishli bo'lmagan <c>periodId</c> → 404.</summary>
public sealed record GetStudentAttendanceQuery(Guid StudentId, DateOnly? From, DateOnly? To, Guid? PeriodId = null)
    : IRequest<IReadOnlyList<StudentAttendanceDay>>;

internal sealed class GetStudentAttendanceQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetStudentAttendanceQuery, IReadOnlyList<StudentAttendanceDay>>
{
    public async Task<IReadOnlyList<StudentAttendanceDay>> Handle(
        GetStudentAttendanceQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → NotFoundException (404).
        var profile = await db.GetScopedStudentAsync(scope, request.StudentId, cancellationToken);

        var periodSet = await db.LoadStudentPeriodsAsync(profile.UserId, profile.StudentGroupId, cancellationToken);
        var selected = periodSet.Resolve(request.PeriodId, today);
        if (selected is null)
            return [];

        var period = await PeriodLookup.ContextAsync(db, selected, cancellationToken);
        var periodId = selected.Id;

        var from = request.From ?? selected.StartDate;
        var to = request.To ?? (today < selected.EndDate ? today : selected.EndDate);
        if (from < selected.StartDate)
            from = selected.StartDate;
        if (to > selected.EndDate)
            to = selected.EndDate;
        if (from > to)
            return [];

        // Ehtiyot chegarasi: validatsiya faqat berilgan parametrlarni tekshiradi, sikl esa cheksiz bo'lmasin.
        var earliest = to.AddDays(-GetStudentAttendanceQueryValidator.MaxRangeDays);
        if (from < earliest)
            from = earliest;

        var rows = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => a.StudentUserId == request.StudentId && a.PeriodId == periodId && a.Date >= from && a.Date <= to)
                .Select(a => new AttendanceDayRow(
                    a.Date, a.Status, a.CheckInAt, a.CheckInDistanceM, a.CheckInAccuracyM,
                    a.CheckOutAt, a.CheckOutDistanceM, a.AutoClosed, a.IsSuspicious, a.SuspiciousReason,
                    a.IsManual, a.ManualReason, a.LeaveRequestId, a.CheckInPhotoFileId, a.CheckOutPhotoFileId))
                .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Date);

        var events = (await db.AttendanceEvents.AsNoTracking().InScope(scope)
                .Where(e => e.StudentUserId == request.StudentId && e.Date >= from && e.Date <= to)
                .OrderBy(e => e.ReceivedAt).ThenBy(e => e.Id)
                .Select(e => new AttendanceEventRow(
                    e.Id, e.Date, e.Kind, e.ReceivedAt, e.Accepted, e.RejectReason,
                    e.Location.Latitude, e.Location.Longitude, e.AccuracyM, e.DistanceM, e.RadiusM, e.PhotoFileId))
                .ToListAsync(cancellationToken))
            .ToLookup(e => e.Date);

        var diaries = (await db.DiaryEntries.AsNoTracking().InScope(scope)
                .Where(d => d.StudentUserId == request.StudentId && d.PeriodId == periodId && d.Date >= from && d.Date <= to)
                .Select(d => new { d.Date, d.Id, d.Status, d.Score })
                .ToListAsync(cancellationToken))
            .GroupBy(d => d.Date)
            .ToDictionary(g => g.Key, g => new StudentAttendanceDiary(g.First().Id, g.First().Status, g.First().Score));

        var leaves = await db.LeaveRequests.AsNoTracking().InScope(scope)
            .Where(l => l.StudentUserId == request.StudentId && l.Status == LeaveRequestStatus.Approved
                        && l.PeriodId == periodId && l.DateFrom <= to && l.DateTo >= from)
            .Select(l => new { l.Id, l.DateFrom, l.DateTo })
            .ToListAsync(cancellationToken);

        var radiusM = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.StudentId && a.PeriodId == periodId
                        && (a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.Completed))
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => (int?)a.Company.RadiusM)
            .FirstOrDefaultAsync(cancellationToken);

        var days = new List<StudentAttendanceDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var row = rows.GetValueOrDefault(date);
            var dayEvents = events[date].ToList();
            var leave = leaves.FirstOrDefault(l => date >= l.DateFrom && date <= l.DateTo);

            var status = row?.Status
                ?? AttendanceStatusResolver.Resolve(period, date, today, localNow, leave is not null, profile.HoursOn(today));

            var checkIns = dayEvents.Where(e => e.Kind == AttendanceEventKind.CheckIn).ToList();
            var acceptedCheckIn = checkIns.LastOrDefault(e => e.Accepted);
            var acceptedCheckOut = dayEvents.LastOrDefault(e => e.Kind == AttendanceEventKind.CheckOut && e.Accepted);

            days.Add(new StudentAttendanceDay(
                date,
                status,
                period.IsWorkDay(date),
                Punch(row?.CheckInAt, row?.CheckInDistanceM, row?.CheckInAccuracyM,
                    acceptedCheckIn, row?.CheckInPhotoFileId ?? acceptedCheckIn?.PhotoFileId, radiusM),
                Punch(row?.CheckOutAt, row?.CheckOutDistanceM, null,
                    acceptedCheckOut, row?.CheckOutPhotoFileId ?? acceptedCheckOut?.PhotoFileId, radiusM),
                row?.AutoClosed ?? false,
                row?.IsSuspicious ?? false,
                row?.SuspiciousReason,
                row?.IsManual ?? false,
                row?.ManualReason,
                row?.LeaveRequestId ?? leave?.Id,
                diaries.GetValueOrDefault(date),
                checkIns.Count,
                checkIns.Count(e => !e.Accepted),
                dayEvents.Select(ToEvent).ToList()));
        }

        return days;
    }

    /// <summary>Urinish yozuvini API shakliga o'tkazadi. Vaqt — <c>ReceivedAt</c> (punch bilan bir xil manba).</summary>
    private static StudentAttendanceEvent ToEvent(AttendanceEventRow e)
    {
        var rejected = !e.Accepted && e.RejectReason != CheckInRejectReason.None;
        return new StudentAttendanceEvent(
            e.Id,
            e.Kind,
            PracticeTime.Hm(e.ReceivedAt),
            PracticeTime.ToLocal(e.ReceivedAt),
            e.Accepted,
            rejected ? e.RejectReason : null,
            rejected ? e.RejectReason.Message() : null,
            e.DistanceM,
            e.AccuracyM,
            e.RadiusM,
            e.Latitude,
            e.Longitude,
            e.PhotoFileId is { } id ? FileUrls.For(id) : null);
    }

    /// <summary>Kunlik qatordagi vaqt/masofa + hodisadagi koordinata va aniqlikni birlashtiradi.
    /// Vaqt yo'q bo'lsa (check-in qilinmagan kun) — null.</summary>
    private static AttendancePunch? Punch(
        DateTimeOffset? at, double? distanceM, double? accuracyM, AttendanceEventRow? source, Guid? photoFileId, int? radiusM)
    {
        if (at is not { } moment)
            return null;

        return new AttendancePunch(
            PracticeTime.Hm(moment),
            PracticeTime.ToLocal(moment),
            distanceM,
            accuracyM ?? source?.AccuracyM,
            source?.Latitude,
            source?.Longitude,
            photoFileId is { } id ? FileUrls.For(id) : null,
            radiusM is { } radius && distanceM > radius);
    }
}

/// <summary>Kunlik davomat qatorining to'liq proyeksiyasi (profil jadvali uchun — <c>AttendanceSnapshot</c> dan kengroq).</summary>
internal sealed record AttendanceDayRow(
    DateOnly Date,
    AttendanceStatus Status,
    DateTimeOffset? CheckInAt,
    double? CheckInDistanceM,
    double? CheckInAccuracyM,
    DateTimeOffset? CheckOutAt,
    double? CheckOutDistanceM,
    bool AutoClosed,
    bool IsSuspicious,
    string? SuspiciousReason,
    bool IsManual,
    string? ManualReason,
    Guid? LeaveRequestId,
    Guid? CheckInPhotoFileId,
    Guid? CheckOutPhotoFileId);

/// <summary>Urinish yozuvi — koordinata va aniqlik faqat shu yerda bor.</summary>
internal sealed record AttendanceEventRow(
    Guid Id,
    DateOnly Date,
    AttendanceEventKind Kind,
    DateTimeOffset ReceivedAt,
    bool Accepted,
    CheckInRejectReason RejectReason,
    double Latitude,
    double Longitude,
    double AccuracyM,
    double DistanceM,
    int RadiusM,
    Guid? PhotoFileId);
