using System.Net;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorStudentsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_DavomatVaKundalikKorsatkichlari()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 4);
        days.Should().HaveCount(4, "davr 14 kun oldin boshlangan");
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[0]);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[1], late: true);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[2], suspiciousReason: "Radius chetida");
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 5);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[1], s.Tutor.Id, score: 4);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[2]);

        var response = await s.Client.GetAsync("/api/tutor/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadAsync<List<TutorStudent>>())!;
        var row = list.Should().ContainSingle(r => r.Id == s.Student.Id).Subject;
        row.Name.Should().Be(s.Student.FullName);
        row.Group.Should().Be(s.Group.GroupName);
        row.Company.Should().Be(s.Company.Name);
        row.HemisId.Should().NotBeNullOrEmpty();
        row.AttendedDays.Should().Be(3);
        row.TotalDays.Should().BeGreaterThanOrEqualTo(row.AttendedDays);
        row.AttendancePct.Should().BeApproximately(Math.Round(300d / row.TotalDays, 1), 0.11);
        row.DiaryCount.Should().Be(3);
        row.DiaryAvg.Should().Be(4.5);
        row.SuspiciousCount.Should().Be(1);
        row.State.Should().BeOneOf(StudentState.Suspicious, StudentState.RedFlag);
    }

    [Fact]
    public async Task Royxat_FaqatOzGuruhlari_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var stranger = await Factory.CreateStudentAsync();

        var list = (await (await s.Client.GetAsync("/api/tutor/students")).Content.ReadAsync<List<TutorStudent>>())!;
        list.Select(r => r.Id).Should().Contain(s.Student.Id).And.NotContain(stranger.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/tutor/students")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
