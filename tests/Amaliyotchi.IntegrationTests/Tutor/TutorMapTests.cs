using System.Net;
using Amaliyotchi.Application.Features.Tutor.Map;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorMapTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Xarita_OxirgiUrinishNuqtalari_KindVaRejected()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var lateStudent = await Factory.CreateStudentAsync(group: s.Group);
        var badStudent = await Factory.CreateStudentAsync(group: s.Group);

        await Factory.AddAttendanceAsync(s.Student, s.Period, today, checkOut: false);
        await Factory.AddCheckInEventAsync(s.Student, s.Company, today, distanceM: 20, accepted: true, at: new TimeOnly(9, 2));

        await Factory.AddAttendanceAsync(lateStudent, s.Period, today, late: true, checkOut: false);
        await Factory.AddCheckInEventAsync(lateStudent, s.Company, today, distanceM: 40, accepted: true, at: new TimeOnly(9, 40));

        await Factory.AddCheckInEventAsync(badStudent, s.Company, today, distanceM: 900, accepted: false, at: new TimeOnly(9, 5), lat: 41.30, lng: 69.25);
        await Factory.AddCheckInEventAsync(badStudent, s.Company, today, distanceM: 3400, accepted: false, at: new TimeOnly(9, 10), lat: 41.28, lng: 69.22);

        var response = await s.Client.GetAsync("/api/tutor/map");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadAsync<MapResponse>())!;
        body.Date.Should().Be(today);
        body.Points.Should().HaveCount(3, "har talaba uchun bitta (oxirgi) nuqta");

        var ok = body.Points.Single(p => p.StudentId == s.Student.Id);
        ok.Kind.Should().Be(MapPointKind.Ok);
        ok.Rejected.Should().BeFalse();
        ok.Time.Should().Be("09:02");
        ok.DistanceM.Should().Be(20);
        ok.RadiusM.Should().Be(s.Company.RadiusM);
        ok.Company.Should().Be(s.Company.Name);
        ok.Lat.Should().BeApproximately(41.3111, 0.0001);

        body.Points.Single(p => p.StudentId == lateStudent.Id).Kind.Should().Be(MapPointKind.Late);

        var bad = body.Points.Single(p => p.StudentId == badStudent.Id);
        bad.Kind.Should().Be(MapPointKind.Bad);
        bad.Rejected.Should().BeTrue();
        bad.DistanceM.Should().Be(3400, "oxirgi urinish");
        bad.Time.Should().Be("09:10");
        bad.Lng.Should().BeApproximately(69.22, 0.0001);
    }

    [Fact]
    public async Task Xarita_SanaParametri_BegonaGuruhKorinmaydi_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 1);
        var stranger = await Factory.CreateStudentAsync();
        var strangerCompany = await Factory.CreateCompanyAsync();
        await Factory.AddCheckInEventAsync(s.Student, s.Company, days[0], distanceM: 30, accepted: true);
        await Factory.AddCheckInEventAsync(stranger, strangerCompany, days[0], distanceM: 30, accepted: true);

        var body = (await (await s.Client.GetAsync($"/api/tutor/map?date={days[0]:yyyy-MM-dd}")).Content.ReadAsync<MapResponse>())!;
        body.Date.Should().Be(days[0]);
        body.Points.Select(p => p.StudentId).Should().Contain(s.Student.Id).And.NotContain(stranger.Id);

        var empty = (await (await s.Client.GetAsync("/api/tutor/map?date=2020-01-01")).Content.ReadAsync<MapResponse>())!;
        empty.Points.Should().BeEmpty();

        (await s.Client.GetAsync("/api/tutor/map?date=kecha")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/tutor/map")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
