using System.Net;
using System.Text;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET/POST /api/student/diary</c>: multipart yuborish, fayl havolasi, takror 409, validatsiya, ko'lam.</summary>
[Collection(ApiCollection.Name)]
public sealed class DiaryTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static readonly byte[] Pdf = Encoding.UTF8.GetBytes("%PDF-1.4 kundalik");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    public async Task Yuborish_Multipart_201_FaylHavolasiIshlaydi()
    {
        var scene = await Factory.CreateSceneAsync();
        using var form = StudentTestData.DiaryForm(
            StudentTestData.LongText(), "Docker compose bilan tanishdim",
            ("hisobot.pdf", "application/pdf", Pdf), ("rasm.png", "image/png", Png));

        var response = await scene.Client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var entry = await response.Content.ReadAsync<DiaryEntryDto>();
        entry!.Date.Should().Be(Factory.LocalToday());
        entry.Status.Should().Be(DiaryStatus.Submitted);
        entry.Text.Should().HaveLength(200);
        entry.Learned.Should().Be("Docker compose bilan tanishdim");
        entry.Score.Should().BeNull();
        entry.Comment.Should().BeNull();
        entry.Files.Should().HaveCount(2);
        entry.Files.Select(f => f.Name).Should().BeEquivalentTo(["hisobot.pdf", "rasm.png"]);
        entry.Files.Should().OnlyContain(f => f.Url == $"/api/files/{f.Id}");

        var file = await scene.Client.GetAsync(entry.Files[0].Url);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(Pdf);

        // Ro'yxat: o'zi yozgani, today.diary.submittedToday = true.
        var list = await (await scene.Client.GetAsync("/api/student/diary")).Content.ReadAsync<List<DiaryEntryDto>>();
        list.Should().ContainSingle(e => e.Id == entry.Id);
        var today = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
        today!.Diary.SubmittedToday.Should().BeTrue();
        today.Place!.Reports.Should().Be(1);

        using var json = JsonDocument.Parse(await (await scene.Client.GetAsync("/api/student/diary")).Content.ReadAsStringAsync());
        json.RootElement[0].GetProperty("status").GetString().Should().Be("submitted");
    }

    [Fact]
    public async Task BugunAllaqachonBor_409()
    {
        var scene = await Factory.CreateSceneAsync();
        using var first = StudentTestData.DiaryForm(StudentTestData.LongText());
        (await scene.Client.PostAsync("/api/student/diary", first)).StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = StudentTestData.DiaryForm(StudentTestData.LongText());
        var response = await scene.Client.PostAsync("/api/student/diary", second);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("allaqachon");
    }

    [Fact]
    public async Task QaytaYozishSoralgan_QaytaYuborish_201()
    {
        var scene = await Factory.CreateSceneAsync();
        using var first = StudentTestData.DiaryForm(StudentTestData.LongText());
        var created = await (await scene.Client.PostAsync("/api/student/diary", first)).Content.ReadAsync<DiaryEntryDto>();
        await Factory.WithDbAsync(async db =>
        {
            var entry = await db.DiaryEntries.SingleAsync(d => d.Id == created!.Id);
            entry.RequestRewrite(scene.Tutor.Id, "Batafsilroq yozing", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        using var again = StudentTestData.DiaryForm(StudentTestData.LongText(300));
        var response = await scene.Client.PostAsync("/api/student/diary", again);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var entry = await response.Content.ReadAsync<DiaryEntryDto>();
        entry!.Id.Should().Be(created!.Id, "bugungi yozuv yangilanadi, yangisi yaratilmaydi");
        entry.Status.Should().Be(DiaryStatus.Submitted);
        entry.Text.Should().HaveLength(300);
    }

    [Fact]
    public async Task MatnQisqa_400_ErrorsText()
    {
        var scene = await Factory.CreateSceneAsync();
        using var form = StudentTestData.DiaryForm("Qisqa matn");

        var response = await scene.Client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("Text", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FayllarKopYokiNotogriTur_400_ErrorsFiles()
    {
        var scene = await Factory.CreateSceneAsync();
        var six = Enumerable.Range(1, 6).Select(i => ($"f{i}.png", "image/png", Png)).ToArray();
        using var tooMany = StudentTestData.DiaryForm(StudentTestData.LongText(), null, six);

        var response = await scene.Client.PostAsync("/api/student/diary", tooMany);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("errors").TryGetProperty("Files", out _).Should().BeTrue();

        using var badType = StudentTestData.DiaryForm(StudentTestData.LongText(), null, ("virus.exe", "application/octet-stream", Png));
        var typeResponse = await scene.Client.PostAsync("/api/student/diary", badType);
        typeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await typeResponse.Content.ReadAsStringAsync()).Should().Contain("Files");

        (await Factory.WithDbAsync(db => db.DiaryEntries.AnyAsync(d => d.StudentUserId == scene.Student.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task PdfMajburiy_PdfsizVaRasmBilan_400_ErrorsFiles()
    {
        var scene = await Factory.CreateSceneAsync();
        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryPdfRequired, "true");

        var today = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
        today!.Diary.PdfRequired.Should().BeTrue();

        using var noFiles = StudentTestData.DiaryForm(StudentTestData.LongText());
        await AssertPdfRequiredAsync(await scene.Client.PostAsync("/api/student/diary", noFiles));

        using var onlyImage = StudentTestData.DiaryForm(StudentTestData.LongText(), null, ("rasm.png", "image/png", Png));
        await AssertPdfRequiredAsync(await scene.Client.PostAsync("/api/student/diary", onlyImage));

        (await Factory.WithDbAsync(db => db.DiaryEntries.AnyAsync(d => d.StudentUserId == scene.Student.Id))).Should().BeFalse();
        (await Factory.WithDbAsync(db => db.StoredFiles.AnyAsync(f => f.UploadedByUserId == scene.Student.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task PdfMajburiy_PdfBilan_201()
    {
        var scene = await Factory.CreateSceneAsync();
        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryPdfRequired, "true");
        using var form = StudentTestData.DiaryForm(
            StudentTestData.LongText(), null, ("rasm.png", "image/png", Png), ("hisobot.pdf", "application/pdf", Pdf));

        var response = await scene.Client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<DiaryEntryDto>())!.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task PdfMajburiy_QaytaYozishda_AvvalgiPdfHisobga_201()
    {
        var scene = await Factory.CreateSceneAsync();
        using var first = StudentTestData.DiaryForm(StudentTestData.LongText(), null, ("hisobot.pdf", "application/pdf", Pdf));
        var created = await (await scene.Client.PostAsync("/api/student/diary", first)).Content.ReadAsync<DiaryEntryDto>();
        await Factory.WithDbAsync(async db =>
        {
            var entry = await db.DiaryEntries.SingleAsync(d => d.Id == created!.Id);
            entry.RequestRewrite(scene.Tutor.Id, "Batafsilroq yozing", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryPdfRequired, "true");

        using var again = StudentTestData.DiaryForm(StudentTestData.LongText(300));
        var response = await scene.Client.PostAsync("/api/student/diary", again);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PdfMajburiyEmas_Pdfsiz_201()
    {
        var scene = await Factory.CreateSceneAsync();
        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryPdfRequired, "false");

        var today = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
        today!.Diary.PdfRequired.Should().BeFalse();

        using var form = StudentTestData.DiaryForm(StudentTestData.LongText(), null, ("rasm.png", "image/png", Png));
        var response = await scene.Client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertPdfRequiredAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("detail").GetString().Should().Be("Hisobotga PDF fayl biriktirilishi shart.");
        json.RootElement.GetProperty("errors").GetProperty("Files")[0].GetString()
            .Should().Be("Hisobotga PDF fayl biriktirilishi shart.");
    }

    [Fact]
    public async Task FaolDavrYoq_400()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);
        using var form = StudentTestData.DiaryForm(StudentTestData.LongText());

        var response = await client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("davri yo'q");
    }

    [Fact]
    public async Task Royxat_FaqatOziniki()
    {
        var sceneA = await Factory.CreateSceneAsync();
        var studentB = await Factory.CreateStudentAsync(group: sceneA.Group);
        await Factory.CreateApprovedApplicationAsync(studentB, sceneA.Period, sceneA.Company, sceneA.Tutor.Id);
        var clientB = await Factory.LoginAsStudentAsync(studentB);

        using var formA = StudentTestData.DiaryForm(StudentTestData.LongText(), null, ("a.pdf", "application/pdf", Pdf));
        var entryA = await (await sceneA.Client.PostAsync("/api/student/diary", formA)).Content.ReadAsync<DiaryEntryDto>();
        using var formB = StudentTestData.DiaryForm(StudentTestData.LongText());
        (await clientB.PostAsync("/api/student/diary", formB)).StatusCode.Should().Be(HttpStatusCode.Created);

        var listB = await (await clientB.GetAsync("/api/student/diary")).Content.ReadAsync<List<DiaryEntryDto>>();
        listB.Should().ContainSingle();
        listB![0].Id.Should().NotBe(entryA!.Id);

        // B talaba A ning faylini ko'ra olmaydi.
        (await clientB.GetAsync(entryA.Files[0].Url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/diary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
