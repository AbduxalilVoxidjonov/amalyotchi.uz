using System.Net;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Check-in/check-out oqimi <see cref="MutableClock"/> bilan: davr standart qoidalar (09:00–17:00, 15 daqiqa
/// kechikish) bilan, soat esa kerakli momentga muzlatiladi — haqiqiy vaqtga bog'liq emas.
/// Token soat muzlatilishidan oldin olinadi (JwtBearer o'z soati bilan tekshiradi).</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInClockTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Soat0905_Present_0920_Late_1705_CheckOut200()
    {
        var day = await Factory.NextWorkDayAsync();
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var company = await Factory.CreateCompanyAsync();
        var period = await Factory.CreatePeriodAtAsync(group, tutor.Id, day);

        var early = await Factory.CreateStudentAsync(group: group);
        var late = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(early, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(late, period, company, tutor.Id);
        var earlyClient = await Factory.LoginAsStudentAsync(early);
        var lateClient = await Factory.LoginAsStudentAsync(late);

        try
        {
            // 09:05 — kechikish chegarasi (09:15) dan oldin → present.
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var presentResponse = await earlyClient.PostJsonAsync("/api/student/checkin", Geo());
            presentResponse.StatusCode.Should().Be(HttpStatusCode.OK, await presentResponse.Content.ReadAsStringAsync());
            var present = await presentResponse.Content.ReadAsync<TodayDto>();
            present!.Date.Should().Be(day);
            present.Checkin.Status.Should().Be(AttendanceStatus.Present);
            present.Checkin.CheckInAt.Should().Be(PracticeTime.At(day, new TimeOnly(9, 5)));

            // 09:20 — chegaradan keyin, oyna (10:30) yopilmagan → late.
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 20)));
            var lateResponse = await lateClient.PostJsonAsync("/api/student/checkin", Geo());
            lateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await lateResponse.Content.ReadAsStringAsync());
            (await lateResponse.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Late);

            // 17:05 — check-out oynasi (17:00–18:00) ochiq → 200, checkOutAt to'ldirilgan.
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
            var checkOutResponse = await earlyClient.PostJsonAsync("/api/student/checkout", Geo());
            checkOutResponse.StatusCode.Should().Be(HttpStatusCode.OK, await checkOutResponse.Content.ReadAsStringAsync());
            var afterCheckOut = await checkOutResponse.Content.ReadAsync<TodayDto>();
            afterCheckOut!.Checkin.Status.Should().Be(AttendanceStatus.Present);
            afterCheckOut.Checkin.CheckOutAt.Should().Be(PracticeTime.At(day, new TimeOnly(17, 5)));
            afterCheckOut.Window.IsOpen.Should().BeFalse("ketish belgilangan");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        fixture.Clock.IsFrozen.Should().BeFalse();
        var rows = await Factory.WithDbAsync(db =>
            db.DailyAttendances.Where(a => a.PeriodId == period.Id && a.Date == day).ToListAsync());
        rows.Should().HaveCount(2);
        rows.Single(a => a.StudentUserId == early.Id).CheckOutAt.Should().NotBeNull();
        rows.Single(a => a.StudentUserId == late.Id).Status.Should().Be(AttendanceStatus.Late);

        var events = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.Where(e => e.Date == day && (e.StudentUserId == early.Id || e.StudentUserId == late.Id)).ToListAsync());
        events.Should().HaveCount(3).And.OnlyContain(e => e.Accepted);
    }

    /// <summary>Muzlatilgan soat momentidagi urinish (validator <c>occurredAt</c> ni shu soat bilan solishtiradi).</summary>
    private object Geo() => Factory.Geo();
}
