using System.Globalization;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Calendar;

public sealed record CalendarRow(Guid StudentId, string Name, IReadOnlyList<CalendarDayStatus> Days);

/// <param name="Month">"YYYY-MM"</param>
/// <param name="Days">Oy kunlari 1..N (<c>rows[].days</c> uzunligi shunga teng).</param>
public sealed record CalendarResponse(string Month, IReadOnlyList<int> Days, IReadOnlyList<CalendarRow> Rows);

/// <summary><c>GET /api/tutor/calendar?month=YYYY-MM</c> (berilmasa — joriy oy). Oy kunlari × ko'lamdagi talabalar.</summary>
public sealed record GetTutorCalendarQuery(string? Month) : IRequest<CalendarResponse>
{
    public const string MonthFormat = "yyyy-MM";

    public static bool TryParseMonth(string? value, out DateOnly firstDay)
    {
        if (DateOnly.TryParseExact(value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            firstDay = new DateOnly(parsed.Year, parsed.Month, 1);
            return true;
        }

        firstDay = default;
        return false;
    }
}

internal sealed class GetTutorCalendarQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorCalendarQuery, CalendarResponse>
{
    public async Task<CalendarResponse> Handle(GetTutorCalendarQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var first = GetTutorCalendarQuery.TryParseMonth(request.Month, out var parsed)
            ? parsed
            : new DateOnly(today.Year, today.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
        var last = first.AddDays(daysInMonth - 1);

        var students = await db.LoadScopedStudentsAsync(scope, cancellationToken);
        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);

        var attendance = (await db.DailyAttendances.AsNoTracking().InScope(scope)
                .Where(a => a.Date >= first && a.Date <= last)
                .SelectSnapshot()
                .ToListAsync(cancellationToken))
            .ToDictionary(a => (a.StudentUserId, a.Date));

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.Status == LeaveRequestStatus.Approved && l.DateFrom <= last && l.DateTo >= first)
                .Select(l => new { l.StudentUserId, l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .ToLookup(l => l.StudentUserId, l => (l.DateFrom, l.DateTo));

        var days = Enumerable.Range(1, daysInMonth).ToList();
        var rows = new List<CalendarRow>(students.Count);
        foreach (var student in students)
        {
            var studentLeaves = leaves[student.UserId].ToList();
            var statuses = new CalendarDayStatus[daysInMonth];
            for (var i = 0; i < daysInMonth; i++)
            {
                var date = first.AddDays(i);
                var row = attendance.GetValueOrDefault((student.UserId, date));
                var onLeave = studentLeaves.Any(l => date >= l.DateFrom && date <= l.DateTo);
                // Oy ikki davrni qamrashi mumkin (kuzgi tugab, bahorgi boshlanadi) — har kun o'z davri bilan.
                var period = periods.ForGroupOn(student.GroupId, date);
                statuses[i] = AttendanceStatusResolver.ResolveCalendar(period, date, today, localNow, row, onLeave);
            }

            rows.Add(new CalendarRow(student.UserId, student.FullName, statuses));
        }

        return new CalendarResponse(first.ToString(GetTutorCalendarQuery.MonthFormat, CultureInfo.InvariantCulture), days, rows);
    }
}
