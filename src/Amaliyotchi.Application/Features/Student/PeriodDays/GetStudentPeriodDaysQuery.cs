using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using MediatR;

namespace Amaliyotchi.Application.Features.Student.PeriodDays;

/// <summary>Kundalik yozuvining qisqa ko'rinishi (bosh ekran paneli uchun).</summary>
public sealed record StudentPeriodDayDiaryDto(Guid Id, DiaryStatus Status, int? Score);

/// <summary>Davrning bitta kalendar kuni.</summary>
/// <param name="Weekday">ISO hafta kuni: 1 = Dushanba … 7 = Yakshanba.</param>
/// <param name="IsWorkDay">Davr ish kunlariga kiradi va bayram emas.</param>
/// <param name="Holiday">Bayram nomi (kun bayram bo'lsa), aks holda null.</param>
/// <param name="Status">Talaba kalendari (<c>GET /api/student/calendar</c>) bilan AYNAN bir xil holat.</param>
/// <param name="CheckInAt">"HH:mm" (Toshkent) yoki null.</param>
/// <param name="CheckOutAt">"HH:mm" (Toshkent) yoki null.</param>
public sealed record StudentPeriodDayDto(
    DateOnly Date,
    int Weekday,
    bool IsWorkDay,
    string? Holiday,
    CalendarDayStatus Status,
    string? CheckInAt,
    string? CheckOutAt,
    bool AutoClosed,
    bool Suspicious,
    bool Manual,
    StudentPeriodDayDiaryDto? Diary);

/// <param name="Status">Ko'rinadigan holat (<see cref="PracticePeriod.EffectiveStatus"/>).</param>
/// <param name="RequiredDays">Davrdagi ish kunlari (bayramsiz) — <see cref="PracticePeriod.RequiredDays"/>.</param>
/// <param name="ElapsedWorkDays">Hisobga olingan ish kunlari — tyutor statistikasi (<see cref="StudentStats.TotalDays"/>)
/// maxraji: o'tgan ish kunlari (bugun — oyna yopilgan yoki belgilangan bo'lsa), sababli kunlarsiz.</param>
public sealed record StudentPeriodDaysPeriodDto(
    Guid Id,
    string Name,
    PracticePeriodStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    int RequiredDays,
    int ElapsedWorkDays);

/// <summary><c>GET /api/student/period-days?periodId=</c> javobi. Davr yo'q → <c>period = null</c>, bo'sh ro'yxatlar.</summary>
public sealed record StudentPeriodDaysDto(
    DateOnly Today,
    StudentPeriodDaysPeriodDto? Period,
    IReadOnlyList<StudentPeriodOption> Periods,
    IReadOnlyList<StudentPeriodDayDto> Days);

/// <summary><c>GET /api/student/period-days?periodId=</c> — davrning HAR BIR kalendar kuni (bosh ekran paneli).
/// <c>periodId</c> berilmasa — <c>GET /api/student/today</c> ko'rsatadigan davr (<see cref="PeriodPurpose.Current"/>),
/// bunday davr bo'lmasa — <see cref="PeriodPurpose.Default"/> (eski guruh davri ham). Begona <c>periodId</c> → 404.</summary>
public sealed record GetStudentPeriodDaysQuery(Guid? PeriodId) : IRequest<StudentPeriodDaysDto>;

/// <summary>Holat — talaba kalendari mantiqi (<see cref="AttendanceCalendar.DayStatus"/>); check-in/out vaqti, avto-yopish,
/// shubhali, qo'lda, kundalik — tyutor/admin kun-bakun davomati (<see cref="GetStudentAttendanceQuery"/>, talaba
/// ko'lami <c>ScopeKind.Self</c>) dan; <c>elapsedWorkDays</c> — <see cref="StudentStatsCalculator"/>.</summary>
internal sealed class GetStudentPeriodDaysQueryHandler(
    IApplicationDbContext db, ICurrentUser currentUser, ISender sender, IClock clock)
    : IRequestHandler<GetStudentPeriodDaysQuery, StudentPeriodDaysDto>
{
    public async Task<StudentPeriodDaysDto> Handle(GetStudentPeriodDaysQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        // Begona periodId → NotFoundException (404) — StudentPeriodSet.Resolve ichida.
        var practice = await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Current, cancellationToken, request.PeriodId);
        if (practice.Period is null && practice.Periods.Default(today) is { } fallback)
            practice = practice with { Period = fallback };

        if (practice.Period is not { } period)
            return new StudentPeriodDaysDto(today, null, [], []);

        var rows = await db.AttendanceInPeriodAsync(userId, period.Id, cancellationToken);
        var leaves = await db.ApprovedLeavesAsync(userId, period.Id, cancellationToken);
        var rowsByDate = rows.ToDictionary(r => r.Date);

        // Batafsil maydonlar tyutor/admin profili bilan bir manbadan (talaba ko'lami — faqat o'zi).
        var details = (await sender.Send(new GetStudentAttendanceQuery(userId, null, null, period.Id), cancellationToken))
            .ToDictionary(d => d.Date);

        var context = await PeriodLookup.ContextAsync(db, period, cancellationToken);
        var snapshots = rows
            .Select(a => new AttendanceSnapshot(
                a.StudentUserId, a.PeriodId, a.Date, a.Status, a.CheckInAt, a.CheckInDistanceM, a.CheckOutAt,
                a.AutoClosed, a.IsSuspicious, a.IsManual))
            .ToList();
        var stats = StudentStatsCalculator.ComputeAttendance(
            context, snapshots, leaves.Select(l => (l.DateFrom, l.DateTo)).ToList(), today, localNow,
            practice.Student.HoursOn(today));

        var days = new List<StudentPeriodDayDto>(period.EndDate.DayNumber - period.StartDate.DayNumber + 1);
        for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
        {
            var detail = details.GetValueOrDefault(date);
            var status = AttendanceCalendar.DayStatus(practice, date, today, localNow, rowsByDate.GetValueOrDefault(date), leaves);
            days.Add(new StudentPeriodDayDto(
                date,
                date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek,
                practice.IsWorkDay(date),
                practice.Holidays.FirstOrDefault(h => h.AppliesTo(date))?.Name,
                status,
                detail?.CheckIn?.At,
                detail?.CheckOut?.At,
                detail?.AutoClosed ?? false,
                detail?.Suspicious ?? false,
                detail?.Manual ?? false,
                detail?.Diary is { } diary ? new StudentPeriodDayDiaryDto(diary.Id, diary.Status, diary.Score) : null));
        }

        var defaultId = practice.Periods.Default(today, PeriodPurpose.Current)?.Id ?? practice.Periods.Default(today)?.Id;

        return new StudentPeriodDaysDto(
            today,
            new StudentPeriodDaysPeriodDto(
                period.Id, period.Name, period.EffectiveStatus(today), period.StartDate, period.EndDate,
                period.RequiredDays, stats.TotalDays),
            practice.Periods.Options(today, defaultId),
            days);
    }
}
