using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.Application.Features.Tutor.Grading;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorGradingTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Grading_Put_KeyinGetMos_HisobGradeCalculatorBilan()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        foreach (var day in days)
            await Factory.AddAttendanceAsync(s.Student, s.Period, day);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 5);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[1], s.Tutor.Id, score: 4);

        var before = (await (await s.Client.GetAsync("/api/tutor/grading")).Content.ReadAsync<List<GradingRow>>())!;
        var initial = before.Should().ContainSingle(r => r.StudentId == s.Student.Id).Subject;
        initial.TutorPoints.Should().BeNull();
        initial.ReferencePoints.Should().BeNull();
        initial.Reports.Avg.Should().Be(4.5);
        initial.Reports.Points.Should().Be(27);
        initial.Recommended.TutorPoints.Should().Be(18);

        var put = await s.Client.PutAsJsonAsync($"/api/tutor/grading/{s.Student.Id}", new { tutorPoints = 15, referencePoints = 8 }, JsonDefaults.Options);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await put.Content.ReadAsync<GradingRow>())!;
        updated.TutorPoints.Should().Be(15);
        updated.ReferencePoints.Should().Be(8);
        var expected = GradeCalculator.Compute(updated.Attendance.Pct, 2, 4.5, 15, 8);
        updated.Total.Should().Be(expected.Total);
        updated.Grade.Should().Be(expected.Grade);
        updated.Attendance.Points.Should().Be(expected.AttendancePoints);

        var after = (await (await s.Client.GetAsync("/api/tutor/grading")).Content.ReadAsync<List<GradingRow>>())!;
        after.Single(r => r.StudentId == s.Student.Id).Should().BeEquivalentTo(updated);

        // Qayta PUT — upsert (ikkinchi qator emas), null bilan tozalash mumkin.
        var cleared = await s.Client.PutAsJsonAsync($"/api/tutor/grading/{s.Student.Id}", new { tutorPoints = (int?)null, referencePoints = 10 }, JsonDefaults.Options);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        var clearedRow = (await cleared.Content.ReadAsync<GradingRow>())!;
        clearedRow.TutorPoints.Should().BeNull();
        clearedRow.ReferencePoints.Should().Be(10);
    }

    [Fact]
    public async Task Grading_Validatsiya400_BegonaTalaba404_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var stranger = await Factory.CreateStudentAsync();

        var tooMany = await s.Client.PutAsJsonAsync($"/api/tutor/grading/{s.Student.Id}", new { tutorPoints = 25, referencePoints = 5 }, JsonDefaults.Options);
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).Should().Contain("TutorPoints");

        var negative = await s.Client.PutAsJsonAsync($"/api/tutor/grading/{s.Student.Id}", new { tutorPoints = 10, referencePoints = -1 }, JsonDefaults.Options);
        negative.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await s.Client.PutAsJsonAsync($"/api/tutor/grading/{stranger.Id}", new { tutorPoints = 10, referencePoints = 5 }, JsonDefaults.Options))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = (await (await s.Client.GetAsync("/api/tutor/grading")).Content.ReadAsync<List<GradingRow>>())!;
        list.Select(r => r.StudentId).Should().Contain(s.Student.Id).And.NotContain(stranger.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/tutor/grading")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.PutAsJsonAsync($"/api/tutor/grading/{s.Student.Id}", new { tutorPoints = 10, referencePoints = 5 }, JsonDefaults.Options))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
