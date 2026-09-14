using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.CheckIn;

/// <summary>Check-in va check-out urinishlarining umumiy qismi: talaba → faol davr → tasdiqlangan ariza → korxona →
/// masofa (Haversine) → policy → <see cref="AttendanceEvent"/> (HAR urinish, rad etilgani ham) → davomat → tranzaksiya.
/// Rad etilganda hodisa avval saqlanadi, keyin xato tashlanadi (Login'dagi <c>LoginFailedAsync</c> uslubi).</summary>
internal sealed class AttendanceAttempt(IApplicationDbContext db, IClock clock, Guid studentUserId)
{
    /// <summary>Urinish sharoiti — konkret qaror (check-in yoki check-out) shu asosda beriladi.</summary>
    public sealed record Situation(
        StudentPractice Practice,
        PracticePeriod Period,
        Company Company,
        CheckInRules Rules,
        DateOnly Today,
        TimeOnly LocalNow,
        DateTimeOffset ReceivedAt,
        GeoPoint Location,
        double DistanceM,
        DailyAttendance? Attendance,
        bool HasApprovedLeave,
        IReadOnlyList<AttendanceEvent> RecentEvents);

    public async Task<TodayDto> RunAsync(
        IGeoRequest request,
        AttendanceEventKind kind,
        Func<Situation, CheckInVerdict> decide,
        Action<Situation, CheckInVerdict, AttendanceEvent> apply,
        CancellationToken cancellationToken)
    {
        var verdict = await db.InTransactionAsync(async ct =>
        {
            var situation = await LoadAsync(request, ct);

            // Idempotentlik: bir xil occurredAt bilan qabul qilingan urinish allaqachon bor (offline navbat,
            // tarmoq takrori) → xato emas, mavjud natija.
            if (situation.RecentEvents.Any(e => e.Kind == kind && e.Accepted && SameInstant(e.OccurredAt, request.OccurredAt)))
                return CheckInVerdict.Accept();

            var result = decide(situation);
            var attempt = AttendanceEvent.Record(
                studentUserId, situation.Company.Id, situation.Today, kind,
                request.OccurredAt, situation.ReceivedAt, situation.Location, request.Accuracy,
                situation.DistanceM, situation.Company.RadiusM, result);
            db.AttendanceEvents.Add(attempt);

            if (result.Accepted)
                apply(situation, result, attempt);

            await db.SaveChangesAsync(ct);
            return result;
        }, cancellationToken);

        if (!verdict.Accepted)
            throw verdict.Reason.ToException();

        return await TodayBuilder.BuildAsync(db, clock, studentUserId, cancellationToken);
    }

    /// <summary>Bazada mikrosekund aniqlik — so'rovdagi 100 ns qismi yo'qoladi; 1 ms ichida — bir xil moment.</summary>
    private static bool SameInstant(DateTimeOffset a, DateTimeOffset b)
        => Math.Abs((a - b).Ticks) < TimeSpan.TicksPerMillisecond;

    private async Task<Situation> LoadAsync(IGeoRequest request, CancellationToken cancellationToken)
    {
        var receivedAt = clock.UtcNow;
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var practice = await db.LoadStudentPracticeAsync(studentUserId, today, cancellationToken);

        // Korxona bo'lmasa hodisani yozib bo'lmaydi (CompanyId majburiy) — policy'dagi NotApproved xabari bilan rad.
        if (practice.Period is null)
            throw new DomainException("Faol amaliyot davri yo'q.");
        if (practice.Company is null)
            throw CheckInRejectReason.NotApproved.ToException();

        var location = new GeoPoint(request.Lat, request.Lng);
        var distance = practice.Company.Location.DistanceMetersTo(location);

        // Davomat qatori tranzaksiya ichida kuzatiladi — check-out uni o'zgartiradi.
        var attendance = await db.AttendanceOnAsync(studentUserId, today, track: true, cancellationToken);

        var periodId = practice.Period.Id;
        var hasLeave = await db.LeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StudentUserId == studentUserId && l.PeriodId == periodId
                           && l.Status == Domain.Leave.LeaveRequestStatus.Approved
                           && l.DateFrom <= today && l.DateTo >= today, cancellationToken);

        // Shubha tahlili uchun: bugungi + so'nggi soatlardagi urinishlar va oldingi kunning qabul qilingan check-in'i.
        var since = receivedAt.AddHours(-SuspiciousDetector.SpeedWindowHours);
        var recent = await db.AttendanceEvents
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentUserId && (e.Date == today || e.ReceivedAt >= since))
            .OrderByDescending(e => e.ReceivedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var previousCheckIn = await db.AttendanceEvents
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentUserId && e.Date < today && e.Accepted && e.Kind == AttendanceEventKind.CheckIn)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (previousCheckIn is not null && recent.All(e => e.Id != previousCheckIn.Id))
            recent.Add(previousCheckIn);

        return new Situation(
            practice, practice.Period, practice.Company, practice.Rules, today, localNow, receivedAt,
            location, distance, attendance, hasLeave, recent);
    }
}
