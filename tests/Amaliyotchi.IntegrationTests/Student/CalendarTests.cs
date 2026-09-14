using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET /api/student/calendar?month=YYYY-MM</c>: oyning har kuni, holatlar, validatsiya.
/// Vaqtga bog'liq holatlar <see cref="MutableClock"/> bilan muzlatilgan soatda tekshiriladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CalendarTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task JoriyOy_200_HarKunUchunHolat()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var yesterday = day.AddDays(-1);
        await Factory.CheckInDirectlyAsync(scene, yesterday, late: true, at: new TimeOnly(9, 20));

        CalendarMonthDto calendar;
        string body;
        try
        {
            // 09:05 — bugungi oyna ochiq, hali belgilanmagan.
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.GetAsync($"/api/student/calendar?month={day:yyyy-MM}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body = await response.Content.ReadAsStringAsync();
            calendar = (await response.Content.ReadAsync<CalendarMonthDto>())!;
        }
        finally
        {
            fixture.Clock.Reset();
        }

        calendar.Month.Should().Be(day.ToString("yyyy-MM"));
        calendar.StudentName.Should().Be(scene.Student.FullName);
        calendar.GroupName.Should().Be(scene.Group.GroupName);
        calendar.Days.Should().HaveCount(DateTime.DaysInMonth(day.Year, day.Month));
        calendar.Days.Select(d => d.Date.Day).Should().BeInAscendingOrder();

        var byDate = calendar.Days.ToDictionary(d => d.Date, d => d.Status);
        byDate[day].Should().Be(CalendarDayStatus.Pending, "oyna hozir ochiq, hali belgilanmagan");
        if (yesterday.Month == day.Month)
            byDate[yesterday].Should().Be(CalendarDayStatus.Late);
        calendar.Days.Where(d => d.Date > day).Should().OnlyContain(d => d.Status == CalendarDayStatus.Future);
        calendar.Days.Where(d => d.Date < day && d.Date >= scene.Period.StartDate && d.Date != yesterday)
            .Should().OnlyContain(d => d.Status == CalendarDayStatus.Absent || d.Status == CalendarDayStatus.DayOff, "har kuni ish kuni (bayramdan tashqari), yozuv yo'q");
        calendar.Days.Where(d => d.Date < scene.Period.StartDate).All(d => d.Status == CalendarDayStatus.DayOff)
            .Should().BeTrue("davr boshlanmasidan oldingi kunlar — dam olish");

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("days")[0].GetProperty("status").GetString().Should().MatchRegex("^(future|pending|present|late|absent|excused|dayOff)$");
    }

    [Fact]
    public async Task MonthBerilmasa_JoriyOy()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var calendar = await (await scene.Client.GetAsync("/api/student/calendar")).Content.ReadAsync<CalendarMonthDto>();

            calendar!.Month.Should().Be(day.ToString("yyyy-MM"));
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("10.2026")]
    [InlineData("abc")]
    public async Task NotogriFormat_400(string month)
    {
        var scene = await Factory.CreateSceneAsync();

        var response = await scene.Client.GetAsync($"/api/student/calendar?month={month}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Month");
    }

    [Fact]
    public async Task FaolDavrYoq_200_DayOff()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var calendar = await (await client.GetAsync("/api/student/calendar")).Content.ReadAsync<CalendarMonthDto>();

        calendar!.Days.Should().OnlyContain(d => d.Status == CalendarDayStatus.DayOff || d.Status == CalendarDayStatus.Future);
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/calendar")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
