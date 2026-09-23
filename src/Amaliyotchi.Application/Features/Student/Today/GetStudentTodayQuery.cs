using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Today;

/// <summary><c>GET /api/student/today</c> — bugungi belgilanish holati, oyna, joy statistikasi, kundalik.</summary>
public sealed record GetStudentTodayQuery : IRequest<TodayDto>;

internal sealed class GetStudentTodayQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<GetStudentTodayQuery, TodayDto>
{
    public Task<TodayDto> Handle(GetStudentTodayQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        return TodayBuilder.BuildAsync(db, clock, userId, cancellationToken);
    }
}

/// <summary><see cref="TodayDto"/> ni quradi — GET today va check-in/check-out javobi bitta manbadan.</summary>
internal static class TodayBuilder
{
    public static async Task<TodayDto> BuildAsync(
        IApplicationDbContext db, IClock clock, Guid studentUserId, CancellationToken cancellationToken)
    {
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();
        // Ko'rinish davri: davom etayotgan → eng yaqin kelgusi → oxirgi tugagan (tanaffusda "davr hali boshlanmagan: …").
        // Belgilanish esa faqat davom etayotgan davrda mumkin (Availability).
        var practice = await db.LoadStudentPracticeAsync(studentUserId, today, PeriodPurpose.Current, cancellationToken);
        var rules = practice.Rules;

        var window = new TodayWindowDto(
            PracticeTime.Hm(rules.DailyStart),
            PracticeTime.Hm(rules.LateAfter),
            PracticeTime.Hm(rules.WindowEnd),
            PracticeTime.Hm(rules.CheckOutFrom),
            IsOpen: false);

        var diaryDto = new TodayDiaryDto(
            false, practice.Settings.MinReportLength, DiaryEntry.MaxAttachments, practice.Settings.DiaryPdfRequired);

        if (practice.Period is null)
        {
            var noPeriod = new TodayCheckInDto(
                AttendanceStatus.Pending, null, null, null, null, null, false, false, StudentPractice.NoPeriodMessage);
            return new TodayDto(today, window, noPeriod, null, diaryDto, null);
        }

        var periodId = practice.Period.Id;
        var rows = await db.AttendanceInPeriodAsync(studentUserId, periodId, cancellationToken);
        var leaves = await db.ApprovedLeavesAsync(studentUserId, periodId, cancellationToken);
        var todayRow = rows.FirstOrDefault(r => r.Date == today);

        var status = AttendanceCalendar.ToAttendanceStatus(
            AttendanceCalendar.DayStatus(practice, today, today, localNow, todayRow, leaves));

        // Oxirgi urinish (rad etilgani ham) — masofa/aniqlik shundan; yozuv bo'lsa yozuvdan.
        var lastEvent = await db.AttendanceEvents
            .AsNoTracking()
            .Where(e => e.StudentUserId == studentUserId && e.Date == today)
            .OrderByDescending(e => e.ReceivedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var distance = todayRow?.CheckOutDistanceM ?? todayRow?.CheckInDistanceM ?? lastEvent?.DistanceM;
        var accuracy = todayRow?.CheckInAccuracyM ?? lastEvent?.AccuracyM;

        var (isOpen, note) = Availability(practice, status, todayRow, today, localNow);

        var checkin = new TodayCheckInDto(
            status,
            todayRow?.CheckInAt,
            todayRow?.CheckOutAt,
            distance,
            practice.Company?.RadiusM,
            accuracy,
            todayRow?.IsSuspicious ?? false,
            todayRow?.AutoClosed ?? false,
            note);

        var diaries = await db.DiaryEntries
            .AsNoTracking()
            .Where(d => d.StudentUserId == studentUserId && d.PeriodId == periodId)
            .ToListAsync(cancellationToken);
        var diaryStats = AttendanceCalendar.ComputeDiaryStats(diaries);
        diaryDto = diaryDto with { SubmittedToday = diaries.Any(d => d.Date == today) };

        TodayPlaceDto? place = null;
        if (practice.IsApproved && practice.Company is { } company)
        {
            var stats = AttendanceCalendar.ComputeStats(practice, today, localNow, rows, leaves);
            place = new TodayPlaceDto(
                company.Name, company.Address, company.RadiusM,
                stats.AttendancePct, stats.DaysPresent, stats.DaysTotal,
                diaryStats.Count, diaryStats.AvgScore);
        }

        return new TodayDto(
            today, window with { IsOpen = isOpen }, checkin, place, diaryDto,
            StudentPeriodSet.ToOption(practice.Period, today, isDefault: true));
    }

    /// <summary>Hozirgi bosqich uchun amal mumkinmi va bo'lmasa — sababi. GPS/masofa hisobga olinmaydi
    /// (ular faqat urinishda ma'lum): policy'ga "ideal" masofa/aniqlik beriladi.</summary>
    private static (bool IsOpen, string? Note) Availability(
        StudentPractice practice, AttendanceStatus status, DailyAttendance? row, DateOnly today, TimeOnly localNow)
    {
        var rules = practice.Rules;

        if (row is { HasCheckedIn: true })
        {
            var checkOut = CheckInPolicy.EvaluateCheckOut(
                new CheckOutContext(localNow, true, row.HasCheckedOut, row.AutoClosed, 0, 0, practice.Company?.RadiusM ?? 0),
                rules);
            return checkOut.Accepted ? (true, null) : (false, checkOut.Reason.Message());
        }

        // Tanlangan davr bugun davom etmayapti (ikki davr oralig'i, davr hali boshlanmagan yoki tugagan) — sabab nom va sana bilan.
        if (!practice.IsOngoing)
            return (false, practice.NoOngoingNote());

        if (!practice.IsApproved || practice.Period is null)
            return (false, CheckInRejectReason.NotApproved.Message());

        var ctx = new CheckInContext(
            today, localNow,
            ApplicationApproved: true,
            PeriodStarted: today >= practice.Period.StartDate,
            PeriodEnded: today > practice.Period.EndDate,
            practice.Period.WorkDays,
            IsHoliday: practice.IsHoliday(today),
            HasApprovedLeave: status == AttendanceStatus.Excused,
            AlreadyCheckedIn: row is not null,
            AccuracyM: 0, DistanceM: 0, RadiusM: practice.Company!.RadiusM);
        var checkIn = CheckInPolicy.Evaluate(ctx, rules);
        return checkIn.Accepted ? (true, null) : (false, checkIn.Reason.Message());
    }
}
