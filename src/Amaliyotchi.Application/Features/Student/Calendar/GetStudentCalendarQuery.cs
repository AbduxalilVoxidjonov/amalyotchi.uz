using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
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

        var practice = await db.LoadStudentPracticeAsync(userId, today, cancellationToken);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
        var days = new List<CalendarDayDto>(daysInMonth);

        if (practice.Period is null)
        {
            for (var d = 1; d <= daysInMonth; d++)
            {
                var date = new DateOnly(first.Year, first.Month, d);
                days.Add(new CalendarDayDto(date, date > today ? Domain.Attendance.CalendarDayStatus.Future : Domain.Attendance.CalendarDayStatus.DayOff));
            }
        }
        else
        {
            var periodId = practice.Period.Id;
            var last = first.AddMonths(1).AddDays(-1);
            var rows = (await db.AttendanceInPeriodAsync(userId, periodId, cancellationToken))
                .Where(r => r.Date >= first && r.Date <= last)
                .ToDictionary(r => r.Date);
            var leaves = await db.ApprovedLeavesAsync(userId, periodId, cancellationToken);

            for (var d = 1; d <= daysInMonth; d++)
            {
                var date = new DateOnly(first.Year, first.Month, d);
                var status = AttendanceCalendar.DayStatus(practice, date, today, localNow, rows.GetValueOrDefault(date), leaves);
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
