using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;
using FluentValidation;
using MediatR;

namespace Amaliyotchi.Application.Features.Student.Calendar;

/// <summary><c>GET /api/student/calendar?month=YYYY-MM</c>. <c>month</c> berilmasa — joriy oy (Toshkent).
/// Oyning har kuni uchun holat: davr tashqarisi/dam olish → dayOff, kelajak → future, bugun → pending/absent.</summary>
public sealed record GetStudentCalendarQuery(string? Month) : IRequest<CalendarMonthDto>;

public sealed class GetStudentCalendarQueryValidator : AbstractValidator<GetStudentCalendarQuery>
{
    public GetStudentCalendarQueryValidator()
    {
        RuleFor(x => x.Month)
            .Must(m => m is null || TryParseMonth(m, out _))
            .WithMessage("Oy YYYY-MM ko'rinishida bo'lishi kerak (masalan, 2026-10).");
    }

    public static bool TryParseMonth(string value, out DateOnly firstDay)
        => DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out firstDay);
}

internal sealed class GetStudentCalendarQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<GetStudentCalendarQuery, CalendarMonthDto>
{
    public async Task<CalendarMonthDto> Handle(GetStudentCalendarQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var first = request.Month is { } month && GetStudentCalendarQueryValidator.TryParseMonth(month, out var parsed)
            ? parsed
            : new DateOnly(today.Year, today.Month, 1);

        var practice = await db.LoadStudentPracticeAsync(userId, today, PeriodPurpose.Current, cancellationToken);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
        var last = first.AddMonths(1).AddDays(-1);
        var days = new List<CalendarDayDto>(daysInMonth);

        // Oy ikki davrni qamrashi mumkin (kuzgi tugab, bahorgi boshlanadi) — har kun o'zini o'z ichiga olgan davr bilan.
        var monthPeriods = practice.Periods.All.Where(p => p.StartDate <= last && p.EndDate >= first).ToList();

        if (practice.Period is null && monthPeriods.Count == 0)
        {
            for (var d = 1; d <= daysInMonth; d++)
            {
                var date = new DateOnly(first.Year, first.Month, d);
                days.Add(new CalendarDayDto(date, date > today ? Domain.Attendance.CalendarDayStatus.Future : Domain.Attendance.CalendarDayStatus.DayOff));
            }
        }
        else
        {
            var rows = new Dictionary<DateOnly, Domain.Attendance.DailyAttendance>();
            var leaves = new List<Domain.Leave.LeaveRequest>();
            foreach (var period in monthPeriods)
            {
                foreach (var row in await db.AttendanceInPeriodAsync(userId, period.Id, cancellationToken))
                {
                    if (row.Date >= first && row.Date <= last)
                        rows[row.Date] = row;
                }

                leaves.AddRange(await db.ApprovedLeavesAsync(userId, period.Id, cancellationToken));
            }

            var byPeriod = monthPeriods.ToDictionary(p => p.Id, p => practice with { Period = p });
            for (var d = 1; d <= daysInMonth; d++)
            {
                var date = new DateOnly(first.Year, first.Month, d);
                var dayPractice = monthPeriods.FirstOrDefault(p => p.Contains(date)) is { } covering
                    ? byPeriod[covering.Id]
                    : practice;
                var status = AttendanceCalendar.DayStatus(dayPractice, date, today, localNow, rows.GetValueOrDefault(date), leaves);
                days.Add(new CalendarDayDto(date, status));
            }
        }

        return new CalendarMonthDto(
            first.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            practice.Student.User.FullName,
            practice.Student.Group.Name,
            days);
    }
}
