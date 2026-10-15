using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.PeriodDays;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>POST /api/student/diary</c> <c>date</c> bilan (har kunga alohida PDF hisobot, v3.30): o'tgan kunga PDF-only 201,
/// takror 409, matnsiz va PDFsiz 400, kelajak / dam olish kuni / yopiq davr 400 <c>errors.Date</c>, rewrite,
/// <c>submittedLate</c>, <c>GET /api/student/period-days</c> → <c>canSubmitDiary</c> POST bilan bir xil;
/// sozlama <c>diaryAllowPastDays</c> = false (v3.31): o'tgan kunga yangi yozuv 400, rewrite — mumkin.</summary>
[Collection(ApiCollection.Name)]
public sealed class DiaryDateTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static readonly byte[] Pdf = Encoding.UTF8.GetBytes("%PDF-1.4 kunlik hisobot");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    [Fact]
    public async Task OtganKun_PdfOnly_201_SubmittedLate_Takror409()
    {
        var scene = await Factory.CreateSceneAsync();
        var day = await PastWorkDayAsync(2);

        using var form = PdfOnlyForm(day);
        var response = await scene.Client.PostAsync("/api/student/diary", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var entry = (await response.Content.ReadAsync<DiaryEntryDto>())!;
        entry.Date.Should().Be(day);
        entry.Text.Should().BeEmpty();
        entry.Files.Should().ContainSingle(f => f.Name == "hisobot.pdf");
        entry.Status.Should().Be(DiaryStatus.Submitted);
        entry.PeriodId.Should().Be(scene.Period.Id);
        entry.SubmittedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("submittedLate").GetBoolean().Should().BeTrue();

        // Takror — 409, yangi matn bilan.
        using var again = PdfOnlyForm(day);
        var conflict = await scene.Client.PostAsync("/api/student/diary", again);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(conflict)).Should().Be("Bu kun uchun hisobot allaqachon yuborilgan.");

        // Admin ro'yxatida ham kech belgisi va bo'sh matn.
        var admin = await Factory.LoginAsAdminAsync();
        using var list = JsonDocument.Parse(await (await admin.GetAsync("/api/admin/diaries?limit=1000")).Content.ReadAsStringAsync());
        var row = list.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == entry.Id);
        row.GetProperty("submittedLate").GetBoolean().Should().BeTrue();
        row.GetProperty("text").GetString().Should().BeEmpty();
        row.GetProperty("files").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task MatnsizVaPdfsiz_400_ErrorsText()
    {
        var scene = await Factory.CreateSceneAsync();
        var day = await PastWorkDayAsync(1);

        using var noText = Form(day, text: null);
        var response = await scene.Client.PostAsync("/api/student/diary", noText);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("errors").TryGetProperty("Text", out _).Should().BeTrue();

        // Faqat rasm — PDF emas: matn baribir shart.
        using var imageOnly = Form(day, text: null, ("rasm.png", "image/png", Png));
        (await scene.Client.PostAsync("/api/student/diary", imageOnly)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // PDF bilan qisqa matn — qabul qilinadi (minimal uzunlik qo'llanmaydi).
        using var shortWithPdf = Form(day, text: "Qisqa izoh", ("hisobot.pdf", "application/pdf", Pdf));
        var ok = await scene.Client.PostAsync("/api/student/diary", shortWithPdf);
        ok.StatusCode.Should().Be(HttpStatusCode.Created, await ok.Content.ReadAsStringAsync());
        (await ok.Content.ReadAsync<DiaryEntryDto>())!.Text.Should().Be("Qisqa izoh");
    }

    [Fact]
    public async Task Kelajak_400_ErrorsDate()
    {
        var scene = await Factory.CreateSceneAsync();

        using var form = PdfOnlyForm(Factory.LocalToday().AddDays(1));
        var response = await scene.Client.PostAsync("/api/student/diary", form);

        await AssertDateErrorAsync(response, "Kelajakdagi kun uchun hisobot yuborib bo'lmaydi.");
        (await Factory.WithDbAsync(db => db.DiaryEntries.AnyAsync(d => d.StudentUserId == scene.Student.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task DamOlishKuni_400_ErrorsDate()
    {
        var today = Factory.LocalToday();
        var scene = await Factory.CreateSceneAsync(
            period: (group, tutorId) => Factory.CreatePeriodAtAsync(group, tutorId, today, workDays: WorkDays.MondayToFriday));
        var weekend = Enumerable.Range(1, 7).Select(i => today.AddDays(-i))
            .First(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        using var form = PdfOnlyForm(weekend);
        await AssertDateErrorAsync(await scene.Client.PostAsync("/api/student/diary", form), "Dam olish kuni uchun hisobot yuborilmaydi.");
    }

    [Fact]
    public async Task DavrTashqarisi_VaYopiqDavr_400_ErrorsDate()
    {
        var scene = await Factory.CreateSceneAsync();

        // Davr boshlanishidan oldingi kun (davr: bugun −14 … +30).
        using var before = PdfOnlyForm(scene.Period.StartDate.AddDays(-1));
        await AssertDateErrorAsync(
            await scene.Client.PostAsync("/api/student/diary", before), "Amaliyot davri hali boshlanmagan.");

        var day = await PastWorkDayAsync(1);
        await Factory.WithDbAsync(async db =>
        {
            var period = await db.PracticePeriods.FirstAsync(p => p.Id == scene.Period.Id);
            period.Close();
            await db.SaveChangesAsync();
        });

        using var closed = PdfOnlyForm(day);
        await AssertDateErrorAsync(
            await scene.Client.PostAsync("/api/student/diary", closed),
            "Amaliyot davri yakunlangan — yangi kundalik yozuvi qo'shib bo'lmaydi.");
    }

    [Fact]
    public async Task NotogriSanaFormati_400_ErrorsDate()
    {
        var scene = await Factory.CreateSceneAsync();
        using var form = StudentTestData.DiaryForm("", null, ("hisobot.pdf", "application/pdf", Pdf));
        form.Add(new StringContent("14.10.2026"), "date");

        await AssertDateErrorAsync(
            await scene.Client.PostAsync("/api/student/diary", form), "Sana noto'g'ri formatda — YYYY-MM-DD kutiladi.");
    }

    [Fact]
    public async Task Rewrite_OtganKun_QaytaYuborish_FayllarQoshiladi()
    {
        var scene = await Factory.CreateSceneAsync();
        var day = await PastWorkDayAsync(3);

        using var first = PdfOnlyForm(day);
        var created = (await (await scene.Client.PostAsync("/api/student/diary", first)).Content.ReadAsync<DiaryEntryDto>())!;
        await Factory.WithDbAsync(async db =>
        {
            var entry = await db.DiaryEntries.SingleAsync(d => d.Id == created.Id);
            entry.RequestRewrite(scene.Admin.Id, "Boshqa PDF yuklang", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        (await DayAsync(scene.Client, day)).CanSubmitDiary.Should().BeTrue("rewrite holatida qayta yuborish mumkin");

        using var second = Form(day, text: null, ("tuzatilgan.pdf", "application/pdf", Pdf));
        var response = await scene.Client.PostAsync("/api/student/diary", second);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var entry = (await response.Content.ReadAsync<DiaryEntryDto>())!;
        entry.Id.Should().Be(created.Id);
        entry.Status.Should().Be(DiaryStatus.Submitted);
        entry.Files.Select(f => f.Name).Should().BeEquivalentTo(["hisobot.pdf", "tuzatilgan.pdf"]);
        (await DayAsync(scene.Client, day)).CanSubmitDiary.Should().BeFalse();
    }

    [Fact]
    public async Task EskiYol_DateSiz_MatnBilan_Bugun_OzgarmaganVaBugunSanasiBilanHam409()
    {
        var scene = await Factory.CreateSceneAsync();
        var today = Factory.LocalToday();

        using var legacy = StudentTestData.DiaryForm(StudentTestData.LongText());
        var response = await scene.Client.PostAsync("/api/student/diary", legacy);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var entry = (await response.Content.ReadAsync<DiaryEntryDto>())!;
        entry.Date.Should().Be(today);
        entry.Text.Should().HaveLength(200);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("submittedLate").GetBoolean().Should().BeFalse();

        using var again = StudentTestData.DiaryForm(StudentTestData.LongText());
        var conflict = await scene.Client.PostAsync("/api/student/diary", again);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(conflict)).Should().Be("Bugungi hisobot allaqachon yuborilgan.");

        using var explicitToday = PdfOnlyForm(today);
        (await scene.Client.PostAsync("/api/student/diary", explicitToday)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PeriodDays_CanSubmitDiary_PostBilanBirXil()
    {
        var today = Factory.LocalToday();
        var scene = await Factory.CreateSceneAsync(
            period: (group, tutorId) => Factory.CreatePeriodAtAsync(group, tutorId, today, workDays: WorkDays.MondayToFriday));
        var holidays = await Factory.WithDbAsync(db => db.Holidays.AsNoTracking().ToListAsync());
        var workDay = Enumerable.Range(1, 13).Select(i => today.AddDays(-i))
            .First(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Any(h => h.AppliesTo(d)));

        var dto = await PeriodDaysAsync(scene.Client);
        foreach (var day in dto.Days)
        {
            var expected = day.Date <= today && day.IsWorkDay;
            day.CanSubmitDiary.Should().Be(expected, $"{day.Date:yyyy-MM-dd} (isWorkDay={day.IsWorkDay})");
        }

        using var form = PdfOnlyForm(workDay);
        (await scene.Client.PostAsync("/api/student/diary", form)).StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await DayAsync(scene.Client, workDay);
        after.CanSubmitDiary.Should().BeFalse();
        after.Diary.Should().NotBeNull();

        // JSON kontrakti: camelCase maydon.
        using var json = JsonDocument.Parse(await (await scene.Client.GetAsync("/api/student/period-days")).Content.ReadAsStringAsync());
        json.RootElement.GetProperty("days")[0].TryGetProperty("canSubmitDiary", out _).Should().BeTrue();
    }

    [Fact]
    public async Task OtganKunlarYopiq_OtganKun_400_CanSubmitFalse_BugunVaEskiYolOchiq()
    {
        var scene = await Factory.CreateSceneAsync();
        var today = Factory.LocalToday();
        var day = await PastWorkDayAsync(2);
        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryAllowPastDays, "false");

        using var past = PdfOnlyForm(day);
        await AssertDateErrorAsync(
            await scene.Client.PostAsync("/api/student/diary", past),
            "O'tgan kunlar uchun hisobot yuklash yopilgan — faqat bugungi hisobotni yuklash mumkin.");
        (await Factory.WithDbAsync(db => db.DiaryEntries.AnyAsync(d => d.StudentUserId == scene.Student.Id))).Should().BeFalse();

        // canSubmitDiary — o'sha qoida: o'tgan kunlar yopiq, bugun ochiq.
        var dto = await PeriodDaysAsync(scene.Client);
        dto.Days.Where(d => d.Date < today).Should().OnlyContain(d => !d.CanSubmitDiary);
        dto.Days.Single(d => d.Date == today).CanSubmitDiary.Should().Be(dto.Days.Single(d => d.Date == today).IsWorkDay);

        // date'siz eski yo'l (bugun) ta'sirlanmaydi.
        using var legacy = StudentTestData.DiaryForm(StudentTestData.LongText());
        var response = await scene.Client.PostAsync("/api/student/diary", legacy);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<DiaryEntryDto>())!.Date.Should().Be(today);
    }

    [Fact]
    public async Task OtganKunlarYopiq_Rewrite_QaytaYuklashMumkin()
    {
        var scene = await Factory.CreateSceneAsync();
        var day = await PastWorkDayAsync(3);

        using var first = PdfOnlyForm(day);
        var created = (await (await scene.Client.PostAsync("/api/student/diary", first)).Content.ReadAsync<DiaryEntryDto>())!;
        await Factory.WithDbAsync(async db =>
        {
            var entry = await db.DiaryEntries.SingleAsync(d => d.Id == created.Id);
            entry.RequestRewrite(scene.Admin.Id, "Boshqa PDF yuklang", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        await using var setting = await Factory.UseSettingAsync(SettingKeys.DiaryAllowPastDays, "false");
        (await DayAsync(scene.Client, day)).CanSubmitDiary.Should().BeTrue("rewrite — admin qayta yuklashni so'ragan");

        using var second = Form(day, text: null, ("tuzatilgan.pdf", "application/pdf", Pdf));
        var response = await scene.Client.PostAsync("/api/student/diary", second);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<DiaryEntryDto>())!.Id.Should().Be(created.Id);
        (await DayAsync(scene.Client, day)).CanSubmitDiary.Should().BeFalse();
    }

    private Task<DateOnly> PastWorkDayAsync(int daysAgo) =>
        Factory.WithDbAsync(async db =>
        {
            var holidays = await db.Holidays.AsNoTracking().ToListAsync();
            var day = Factory.LocalToday().AddDays(-daysAgo);
            while (holidays.Any(h => h.AppliesTo(day)))
                day = day.AddDays(-1);
            return day;
        });

    private static MultipartFormDataContent PdfOnlyForm(DateOnly date)
        => Form(date, text: null, ("hisobot.pdf", "application/pdf", Pdf));

    private static MultipartFormDataContent Form(
        DateOnly date, string? text, params (string Name, string ContentType, byte[] Bytes)[] files)
    {
        var form = StudentTestData.DiaryForm(text ?? string.Empty, null, files);
        form.Add(new StringContent(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "date");
        return form;
    }

    private static async Task AssertDateErrorAsync(HttpResponseMessage response, string message)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("detail").GetString().Should().Be(message);
        json.RootElement.GetProperty("errors").GetProperty("Date")[0].GetString().Should().Be(message);
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }

    private static async Task<StudentPeriodDaysDto> PeriodDaysAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/student/period-days");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<StudentPeriodDaysDto>())!;
    }

    private static async Task<StudentPeriodDayDto> DayAsync(HttpClient client, DateOnly date)
        => (await PeriodDaysAsync(client)).Days.Single(d => d.Date == date);
}
