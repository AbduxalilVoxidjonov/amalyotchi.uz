using System.Net;
using Amaliyotchi.Application.Features.Tutor.Calendar;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorCalendarTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Kalendar_OyKunlari_HolatlarTogri()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var holidays = await Factory.LoadHolidaysAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[0]);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[1], late: true);
        var month = $"{today.Year:0000}-{today.Month:00}";

        var response = await s.Client.GetAsync($"/api/tutor/calendar?month={month}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadAsync<CalendarResponse>())!;
        body.Month.Should().Be(month);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        body.Days.Should().Equal(Enumerable.Range(1, daysInMonth));
        var row = body.Rows.Should().ContainSingle(r => r.StudentId == s.Student.Id).Subject;
        row.Days.Should().HaveCount(daysInMonth);

        CalendarDayStatus At(DateOnly d) => row.Days[d.Day - 1];

        if (days[0].Month == today.Month)
            At(days[0]).Should().Be(CalendarDayStatus.Present);
        if (days[1].Month == today.Month)
            At(days[1]).Should().Be(CalendarDayStatus.Late);

        for (var d = new DateOnly(today.Year, today.Month, 1); d <= new DateOnly(today.Year, today.Month, daysInMonth); d = d.AddDays(1))
        {
            if (d == days[0] || d == days[1])
                continue;
            var inPeriod = d >= s.Period.StartDate && d <= s.Period.EndDate;
            if (!inPeriod || !s.Period.IsWorkDay(d, holidays))
                At(d).Should().Be(CalendarDayStatus.DayOff, $"{d} ish kuni emas");
            else if (d < today)
                At(d).Should().Be(CalendarDayStatus.Absent, $"{d} o'tgan ish kuni, qator yo'q");
            else if (d > today)
                At(d).Should().Be(CalendarDayStatus.Future, $"{d} kelajak");
            else
                At(d).Should().BeOneOf(CalendarDayStatus.Pending, CalendarDayStatus.Absent);
        }
    }

    [Fact]
    public async Task Kalendar_OyChegaralari_FevralVaDekabr_DavrdanTashqariDayOff()
    {
        var s = await Factory.CreateTutorScenarioAsync();

        var feb = (await (await s.Client.GetAsync("/api/tutor/calendar?month=2024-02")).Content.ReadAsync<CalendarResponse>())!;
        feb.Days.Should().HaveCount(29);
        feb.Rows.Single(r => r.StudentId == s.Student.Id).Days.Should().HaveCount(29).And.OnlyContain(d => d == CalendarDayStatus.DayOff);

        var dec = (await (await s.Client.GetAsync("/api/tutor/calendar?month=2023-12")).Content.ReadAsync<CalendarResponse>())!;
        dec.Days.Should().HaveCount(31);
        dec.Month.Should().Be("2023-12");

        var current = (await (await s.Client.GetAsync("/api/tutor/calendar")).Content.ReadAsync<CalendarResponse>())!;
        var today = Factory.Today();
        current.Month.Should().Be($"{today.Year:0000}-{today.Month:00}");
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026/10")]
    [InlineData("oktabr")]
    public async Task Kalendar_NotogriOy_400(string month)
    {
        var s = await Factory.CreateTutorScenarioAsync();

        var response = await s.Client.GetAsync($"/api/tutor/calendar?month={month}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Month");
    }

    [Fact]
    public async Task Kalendar_BegonaGuruhKorinmaydi_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var stranger = await Factory.CreateStudentAsync();

        var body = (await (await s.Client.GetAsync("/api/tutor/calendar")).Content.ReadAsync<CalendarResponse>())!;
        body.Rows.Select(r => r.StudentId).Should().Contain(s.Student.Id).And.NotContain(stranger.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/tutor/calendar")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
