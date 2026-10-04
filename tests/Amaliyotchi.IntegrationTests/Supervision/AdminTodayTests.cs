using System.Net;
using Amaliyotchi.Application.Features.Supervision.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Supervision;

[Collection(ApiCollection.Name)]
public sealed class AdminTodayTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Bugun_Talabalar_StatusVaStatistika()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var today = Factory.Today();
        var marker = "Bugun" + Guid.NewGuid().ToString("N")[..8];
        var onTime = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{marker} Vaqtida");
        var lateStudent = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{marker} Kech");
        await Factory.CreateApprovedApplicationAsync(onTime, s.Period, s.Company, s.Admin.Id);
        await Factory.CreateApprovedApplicationAsync(lateStudent, s.Period, s.Company, s.Admin.Id);
        await Factory.AddAttendanceAsync(onTime, s.Period, today, checkOut: false);
        await Factory.AddAttendanceAsync(lateStudent, s.Period, today, late: true, checkOut: false, suspiciousReason: "Radius chetida");
        await Factory.AddDiaryAsync(onTime, s.Period, today);

        var response = await s.Client.GetAsync($"/api/admin/today?q={marker}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadAsync<TodayResponse>())!;
        body.Date.Should().Be(today);
        // Admin — barcha talabalar (umumiy baza): statistika filtrga bog'liq emas va kamida shu sahnani o'z ichiga oladi.
        body.Stats.Total.Should().BeGreaterThanOrEqualTo(3);
        body.Stats.Present.Should().BeGreaterThanOrEqualTo(1);
        body.Stats.Late.Should().BeGreaterThanOrEqualTo(1);
        body.Stats.Diaries.Should().BeGreaterThanOrEqualTo(1);
        body.Rows.Total.Should().Be(2);
        body.Rows.Page.Should().Be(1);

        var row = body.Rows.Items.Single(r => r.StudentId == onTime.Id);
        row.Status.Should().Be(AttendanceStatus.Present);
        row.CheckIn.Should().Be("08:55");
        row.CheckOut.Should().BeNull();
        row.Company.Should().Be(s.Company.Name);
        row.Group.Should().Be(s.Group.GroupName);
        row.Diary.Should().Be(DiaryState.Written);
        row.DistanceM.Should().Be(25);
        row.Suspicious.Should().BeFalse();

        var late = body.Rows.Items.Single(r => r.StudentId == lateStudent.Id);
        late.Status.Should().Be(AttendanceStatus.Late);
        late.Suspicious.Should().BeTrue();
        late.CheckIn.Should().Be("09:40");
    }

    [Fact]
    public async Task Bugun_StatusFiltri_VaSahifalash()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var today = Factory.Today();
        var marker = "Filtr" + Guid.NewGuid().ToString("N")[..8];
        var first = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{marker} A");
        var other = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{marker} B");
        await Factory.CreateApprovedApplicationAsync(first, s.Period, s.Company, s.Admin.Id);
        await Factory.CreateApprovedApplicationAsync(other, s.Period, s.Company, s.Admin.Id);
        await Factory.AddAttendanceAsync(first, s.Period, today, checkOut: false);
        await Factory.AddAttendanceAsync(other, s.Period, today, late: true, checkOut: false);

        var all = (await (await s.Client.GetAsync("/api/admin/today")).Content.ReadAsync<TodayResponse>())!;
        var late = (await (await s.Client.GetAsync($"/api/admin/today?status=late&q={marker}")).Content.ReadAsync<TodayResponse>())!;
        late.Rows.Items.Should().ContainSingle().Which.StudentId.Should().Be(other.Id);
        late.Stats.Total.Should().Be(all.Stats.Total, "statistika filtrga bog'liq emas");

        var paged = (await (await s.Client.GetAsync($"/api/admin/today?q={marker}&page=2&pageSize=1")).Content.ReadAsync<TodayResponse>())!;
        paged.Rows.Items.Should().HaveCount(1);
        paged.Rows.Page.Should().Be(2);
        paged.Rows.PageSize.Should().Be(1);
        paged.Rows.Total.Should().Be(2);

        (await s.Client.GetAsync("/api/admin/today?status=nimadir")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bugun_RadiusTashqarisiUrinish_AlertVaOutOfRadius()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        await Factory.AddCheckInEventAsync(s.Student, s.Company, Factory.Today(), distanceM: 3400, accepted: false);

        var marker = "Radius" + Guid.NewGuid().ToString("N")[..8];
        await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == s.Student.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.FullName, marker)));
        var body = (await (await s.Client.GetAsync($"/api/admin/today?status=suspicious&q={marker}")).Content.ReadAsync<TodayResponse>())!;

        var row = body.Rows.Items.Should().ContainSingle(r => r.StudentId == s.Student.Id).Subject;
        row.OutOfRadius.Should().BeTrue();
        row.DistanceM.Should().Be(3400);
        body.Alerts.Should().Contain(a => a.Kind == TodayAlertKind.OutOfRadius && a.Count >= 1 && a.MaxDistanceM >= 3400
                                          && a.Href == "/admin/map");
        body.Alerts.Should().NotContain(a => (int)a.Kind == 3); // ruxsat so'rovlari alerti olib tashlangan


    }

    [Fact]
    public async Task Bugun_Admin_BarchaGuruhlar_TalabaRoli_403()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var marker = "Hamma" + Guid.NewGuid().ToString("N")[..8];
        var mine = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{marker} A");
        var stranger = await Factory.CreateStudentAsync(group: await Factory.CreateGroupAsync(), fullName: $"{marker} B");

        var body = (await (await s.Client.GetAsync($"/api/admin/today?q={marker}")).Content.ReadAsync<TodayResponse>())!;
        body.Rows.Items.Select(r => r.StudentId).Should().BeEquivalentTo([mine.Id, stranger.Id], "admin ko'lami cheklovsiz");

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/admin/today")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/admin/today")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
