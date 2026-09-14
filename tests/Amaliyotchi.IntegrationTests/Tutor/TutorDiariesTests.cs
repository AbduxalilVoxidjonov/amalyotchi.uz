using System.Net;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorDiariesTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_TekshirilmaganlarBirinchi_FayllarHavolaBilan()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var approved = await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 4);
        var file = await Factory.CreateStoredFileAsync(s.Student.Id, Amaliyotchi.Domain.Files.StoredFileKind.DiaryAttachment, "rasm.png", "image/png");
        var pending = await Factory.WithDbAsync(async db =>
        {
            var entry = DiaryEntry.Create(s.Student.Id, s.Period.Id, days[1], TutorTestHelpers.DiaryText, null,
                Amaliyotchi.Domain.Common.PracticeTime.At(days[1], new TimeOnly(18, 0)));
            entry.AddAttachment(file.Id, "rasm.png", 1234);
            db.DiaryEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        });

        var response = await s.Client.GetAsync("/api/tutor/diaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadAsync<List<TutorDiaryEntry>>())!;
        list.Select(d => d.Id).Should().ContainInOrder(pending.Id, approved.Id);
        var first = list.Single(d => d.Id == pending.Id);
        first.Status.Should().Be(DiaryStatus.Submitted);
        first.StudentName.Should().Be(s.Student.FullName);
        first.Group.Should().Be(s.Group.GroupName);
        first.Files.Should().ContainSingle().Which.Url.Should().Be($"/api/files/{file.Id}");
        first.Score.Should().BeNull();
        list.Single(d => d.Id == approved.Id).Score.Should().Be(4);

        var onlyApproved = (await (await s.Client.GetAsync("/api/tutor/diaries?status=approved")).Content.ReadAsync<List<TutorDiaryEntry>>())!;
        onlyApproved.Select(d => d.Id).Should().Contain(approved.Id).And.NotContain(pending.Id);
    }

    [Fact]
    public async Task Review_Score_Rewrite_Validatsiya_Takror409()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var entry1 = await Factory.AddDiaryAsync(s.Student, s.Period, days[0]);
        var entry2 = await Factory.AddDiaryAsync(s.Student, s.Period, days[1]);

        var scored = await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry1.Id}/review", new { action = "score", score = 5, comment = "Ajoyib" });
        scored.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await scored.Content.ReadAsync<TutorDiaryEntry>())!;
        dto.Status.Should().Be(DiaryStatus.Approved);
        dto.Score.Should().Be(5);
        dto.Comment.Should().Be("Ajoyib");

        (await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry1.Id}/review", new { action = "approve" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry2.Id}/review", new { action = "score" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry2.Id}/review", new { action = "score", score = 7 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry2.Id}/review", new { action = "rewrite" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rewrite = await s.Client.PostJsonAsync($"/api/tutor/diaries/{entry2.Id}/review", new { action = "rewrite", comment = "Batafsilroq yozing" });
        rewrite.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rewrite.Content.ReadAsync<TutorDiaryEntry>())!.Status.Should().Be(DiaryStatus.Rewrite);

        await Factory.WithDbAsync(async db =>
            (await db.AuditLogs.CountAsync(l => l.Action == Amaliyotchi.Domain.Enums.AuditAction.DiaryReviewed
                && (l.EntityId == entry1.Id.ToString() || l.EntityId == entry2.Id.ToString()))).Should().Be(2));
    }

    [Fact]
    public async Task Review_BegonaGuruh404_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var entry = await Factory.AddDiaryAsync(s.Student, s.Period, Factory.Today());
        var body = new { action = "approve", score = 4 };

        var strangerTutor = await Factory.LoginAsTutorAsync();
        (await strangerTutor.PostJsonAsync($"/api/tutor/diaries/{entry.Id}/review", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var strangerList = (await (await strangerTutor.GetAsync("/api/tutor/diaries")).Content.ReadAsync<List<TutorDiaryEntry>>())!;
        strangerList.Should().NotContain(d => d.Id == entry.Id);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.PostJsonAsync($"/api/tutor/diaries/{entry.Id}/review", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
