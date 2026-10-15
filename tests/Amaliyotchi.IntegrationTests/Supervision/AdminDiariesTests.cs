using System.Net;
using Amaliyotchi.Application.Features.Supervision.Diaries;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Supervision;

[Collection(ApiCollection.Name)]
public sealed class AdminDiariesTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_TekshirilmaganlarBirinchi_FayllarHavolaBilan()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var approved = await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Admin.Id, score: 4);
        var file = await Factory.CreateStoredFileAsync(s.Student.Id, Amaliyotchi.Domain.Files.StoredFileKind.DiaryAttachment, "rasm.png", "image/png");
        var pending = await Factory.WithDbAsync(async db =>
        {
            var entry = DiaryEntry.Create(s.Student.Id, s.Period.Id, days[1], AdminTestHelpers.DiaryText, null,
                Amaliyotchi.Domain.Common.PracticeTime.At(days[1], new TimeOnly(18, 0)));
            entry.AddAttachment(file.Id, "rasm.png", 1234);
            db.DiaryEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        });

        var response = await s.Client.GetAsync("/api/admin/diaries?limit=1000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadAsync<List<DiaryReviewEntry>>())!;
        list.Select(d => d.Id).Should().ContainInOrder(pending.Id, approved.Id);
        var first = list.Single(d => d.Id == pending.Id);
        first.Status.Should().Be(DiaryStatus.Submitted);
        first.StudentName.Should().Be(s.Student.FullName);
        first.Group.Should().Be(s.Group.GroupName);
        first.Files.Should().ContainSingle().Which.Url.Should().Be($"/api/files/{file.Id}");
        first.Score.Should().BeNull();
        list.Single(d => d.Id == approved.Id).Score.Should().Be(4);

        var onlyApproved = (await (await s.Client.GetAsync("/api/admin/diaries?status=approved&limit=1000")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        onlyApproved.Select(d => d.Id).Should().Contain(approved.Id).And.NotContain(pending.Id);
    }

    [Fact]
    public async Task Review_Score_Rewrite_Validatsiya_Takror409()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var entry1 = await Factory.AddDiaryAsync(s.Student, s.Period, days[0]);
        var entry2 = await Factory.AddDiaryAsync(s.Student, s.Period, days[1]);

        var scored = await s.Client.PostJsonAsync($"/api/admin/diaries/{entry1.Id}/review", new { action = "score", score = 5, comment = "Ajoyib" });
        scored.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await scored.Content.ReadAsync<DiaryReviewEntry>())!;
        dto.Status.Should().Be(DiaryStatus.Approved);
        dto.Score.Should().Be(5);
        dto.Comment.Should().Be("Ajoyib");

        (await s.Client.PostJsonAsync($"/api/admin/diaries/{entry1.Id}/review", new { action = "approve" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await s.Client.PostJsonAsync($"/api/admin/diaries/{entry2.Id}/review", new { action = "score" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await s.Client.PostJsonAsync($"/api/admin/diaries/{entry2.Id}/review", new { action = "score", score = 7 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await s.Client.PostJsonAsync($"/api/admin/diaries/{entry2.Id}/review", new { action = "rewrite" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rewrite = await s.Client.PostJsonAsync($"/api/admin/diaries/{entry2.Id}/review", new { action = "rewrite", comment = "Batafsilroq yozing" });
        rewrite.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rewrite.Content.ReadAsync<DiaryReviewEntry>())!.Status.Should().Be(DiaryStatus.Rewrite);

        await Factory.WithDbAsync(async db =>
            (await db.AuditLogs.CountAsync(l => l.Action == Amaliyotchi.Domain.Enums.AuditAction.DiaryReviewed
                && (l.EntityId == entry1.Id.ToString() || l.EntityId == entry2.Id.ToString()))).Should().Be(2));
    }

    [Fact]
    public async Task Review_YoqKundalik404_Talaba403()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var entry = await Factory.AddDiaryAsync(s.Student, s.Period, Factory.Today());
        var body = new { action = "approve", score = 4 };

        (await s.Client.PostJsonAsync($"/api/admin/diaries/{Guid.CreateVersion7()}/review", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await s.Client.GetAsync("/api/admin/diaries?limit=1000")).Content.ReadAsync<List<DiaryReviewEntry>>())!
            .Should().Contain(d => d.Id == entry.Id, "admin barcha kundaliklarni ko'radi");

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.PostJsonAsync($"/api/admin/diaries/{entry.Id}/review", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>v3.35: ro'yxat limitli (sukut 100, 1..1000) — tartib saqlanadi: tekshirilmaganlar birinchi, keyin eng yangisi;
    /// limitdan kichik javob — boshqa yozuv yo'q.</summary>
    [Fact]
    public async Task List_Limit_TartibniSaqlaydi_VaChegaralanadi()
    {
        var s = await Factory.CreateAdminScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Admin.Id, score: 5);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[1]);

        var all = (await (await s.Client.GetAsync("/api/admin/diaries?limit=1000")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        all.Count.Should().BeGreaterThanOrEqualTo(2);
        var unreviewed = all.TakeWhile(d => d.Status is DiaryStatus.Submitted or DiaryStatus.Seen).ToList();
        all.Skip(unreviewed.Count).Should().OnlyContain(d => d.Status != DiaryStatus.Submitted && d.Status != DiaryStatus.Seen,
            "tekshirilmaganlar birinchi");
        unreviewed.Select(d => d.SubmittedAt).Should().BeInDescendingOrder();
        all.Skip(unreviewed.Count).Select(d => d.SubmittedAt).Should().BeInDescendingOrder();

        var two = (await (await s.Client.GetAsync("/api/admin/diaries?limit=2")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        two.Select(d => d.Id).Should().Equal(all.Take(2).Select(d => d.Id));

        // Chegaradan tashqari qiymatlar 1..1000 ga keltiriladi (400 emas).
        var clampedLow = (await (await s.Client.GetAsync("/api/admin/diaries?limit=0")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        clampedLow.Should().ContainSingle().Which.Id.Should().Be(all[0].Id);
        var byDefault = (await (await s.Client.GetAsync("/api/admin/diaries")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        byDefault.Count.Should().Be(Math.Min(all.Count, 100));
    }
}
