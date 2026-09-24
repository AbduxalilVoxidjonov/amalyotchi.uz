using System.Net;
using Amaliyotchi.Application.Features.Tutor.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorTodayTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Bugun_KolamdagiTalabalar_StatusVaStatistika()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var lateStudent = await Factory.CreateStudentAsync(group: s.Group, fullName: "Kech Kelgan");
        await Factory.CreateApprovedApplicationAsync(lateStudent, s.Period, s.Company, s.Tutor.Id);
        await Factory.AddAttendanceAsync(s.Student, s.Period, today, checkOut: false);
        await Factory.AddAttendanceAsync(lateStudent, s.Period, today, late: true, checkOut: false, suspiciousReason: "Radius chetida");
        await Factory.AddDiaryAsync(s.Student, s.Period, today);

        var response = await s.Client.GetAsync("/api/tutor/today");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadAsync<TodayResponse>())!;
        body.Date.Should().Be(today);
        body.Stats.Total.Should().Be(2);
        body.Stats.Present.Should().Be(1);
        body.Stats.Late.Should().Be(1);
        body.Stats.Diaries.Should().Be(1);
        body.Rows.Total.Should().Be(2);
        body.Rows.Page.Should().Be(1);

        var row = body.Rows.Items.Single(r => r.StudentId == s.Student.Id);
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
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var other = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.AddAttendanceAsync(s.Student, s.Period, today, checkOut: false);
        await Factory.AddAttendanceAsync(other, s.Period, today, late: true, checkOut: false);

        var late = (await (await s.Client.GetAsync("/api/tutor/today?status=late")).Content.ReadAsync<TodayResponse>())!;
        late.Rows.Items.Should().ContainSingle().Which.StudentId.Should().Be(other.Id);
        late.Stats.Total.Should().Be(2, "statistika filtrga bog'liq emas");

        var paged = (await (await s.Client.GetAsync("/api/tutor/today?page=2&pageSize=1")).Content.ReadAsync<TodayResponse>())!;
        paged.Rows.Items.Should().HaveCount(1);
        paged.Rows.Page.Should().Be(2);
        paged.Rows.PageSize.Should().Be(1);
        paged.Rows.Total.Should().Be(2);

        (await s.Client.GetAsync("/api/tutor/today?status=nimadir")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bugun_RadiusTashqarisiUrinish_AlertVaOutOfRadius()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        await Factory.AddCheckInEventAsync(s.Student, s.Company, Factory.Today(), distanceM: 3400, accepted: false);

        var body = (await (await s.Client.GetAsync("/api/tutor/today")).Content.ReadAsync<TodayResponse>())!;

        var row = body.Rows.Items.Single();
        row.OutOfRadius.Should().BeTrue();
        row.DistanceM.Should().Be(3400);
        body.Alerts.Should().Contain(a => a.Kind == TodayAlertKind.OutOfRadius && a.Count == 1 && a.MaxDistanceM == 3400);
        body.Alerts.Should().NotContain(a => (int)a.Kind == 3); // ruxsat so'rovlari alerti olib tashlangan

        var suspicious = (await (await s.Client.GetAsync("/api/tutor/today?status=suspicious")).Content.ReadAsync<TodayResponse>())!;
        suspicious.Rows.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Bugun_BegonaGuruh_Korinmaydi_TalabaRoli_403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var otherGroup = await Factory.CreateGroupAsync();
        var stranger = await Factory.CreateStudentAsync(group: otherGroup);

        var body = (await (await s.Client.GetAsync("/api/tutor/today")).Content.ReadAsync<TodayResponse>())!;
        body.Rows.Items.Select(r => r.StudentId).Should().Contain(s.Student.Id).And.NotContain(stranger.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/tutor/today")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/tutor/today")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
