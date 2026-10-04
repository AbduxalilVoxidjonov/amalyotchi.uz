using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Nav;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Supervision.Applications;
using Amaliyotchi.Application.Features.Supervision.Diaries;
using Amaliyotchi.Application.Features.Supervision.Today;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.IntegrationTests.Supervision;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminNavTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Nav_Sonlar_RoyxatlarTotaliBilanTeng_VaYangiYozuvniSanaydi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateAdminAsync();
        await Factory.CreateCompanyAsync();
        await Factory.CreateStudentAsync(group: group, fullName: "Nav Talaba 1");
        await Factory.CreateStudentAsync(group: group, fullName: "Nav Talaba 2", active: false, linkTelegram: false);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/nav");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var counts = json.RootElement.GetProperty("counts");
            counts.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                ["faculties", "companies", "students", "today", "applications", "diaries", "pendingFaceEnrollments"]);
            foreach (var key in counts.EnumerateObject())
                key.Value.ValueKind.Should().Be(JsonValueKind.Number, key.Name);
            json.RootElement.GetProperty("context").TryGetProperty("academicYear", out _).Should().BeTrue();
        }

        var nav = JsonSerializer.Deserialize<AdminNavDto>(body, JsonDefaults.Options)!;
        nav.Counts.Faculties.Should().Be(await TotalAsync(client, "faculties"));
        nav.Counts.Companies.Should().Be(await TotalAsync(client, "companies"));
        nav.Counts.Students.Should().Be(await TotalAsync(client, "students"));
        nav.Counts.Students.Should().BeGreaterThanOrEqualTo(2, "nofaol/Telegram'siz talaba ham ro'yxatda sanaladi");

        var currentYear = await Factory.WithDbAsync(db => db.AcademicYears
            .Where(y => y.IsActive).OrderByDescending(y => y.StartDate).Select(y => y.Name).FirstOrDefaultAsync());
        nav.Context.AcademicYear.Should().Be(currentYear).And.NotBeNull();

        // Yangi talaba + korxona → badge ham, ro'yxat total'i ham +1.
        await Factory.CreateStudentAsync(group: group);
        await Factory.CreateCompanyAsync();
        var after = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        after.Counts.Students.Should().Be(nav.Counts.Students + 1).And.Be(await TotalAsync(client, "students"));
        after.Counts.Companies.Should().Be(nav.Counts.Companies + 1).And.Be(await TotalAsync(client, "companies"));
    }

    /// <summary>Sobiq tyutor badge'lari endi admin nav'ida: har son — tegishli admin sahifasining sukut holati bilan teng,
    /// yangi yuborilgan ariza / tekshirilmagan kundalik +1 qiladi.</summary>
    [Fact]
    public async Task Nav_AmaliyotBadgelari_SahifalarBilanTeng_VaYangiYozuvniSanaydi()
    {
        var s = await Factory.CreateAdminScenarioAsync(approved: false);
        var client = s.Client;

        var nav = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        var today = (await (await client.GetAsync("/api/admin/today")).Content.ReadAsync<TodayResponse>())!;
        nav.Counts.Today.Should().Be(today.Stats.Total).And.Be(today.Rows.Total);
        var applications = (await (await client.GetAsync("/api/admin/applications")).Content.ReadAsync<ApplicationListResponse>())!;
        nav.Counts.Applications.Should().Be(applications.Counts.Submitted).And.BePositive();
        var diaries = (await (await client.GetAsync("/api/admin/diaries")).Content.ReadAsync<List<DiaryReviewEntry>>())!;
        nav.Counts.Diaries.Should().Be(diaries.Count(d => d.Status is DiaryStatus.Submitted or DiaryStatus.Seen));
        var faces = (await (await client.GetAsync("/api/admin/face-enrollments")).Content.ReadAsync<FaceEnrollmentList>())!;
        nav.Counts.PendingFaceEnrollments.Should().Be(faces.Items.Count);

        var second = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateSubmittedApplicationAsync(second, s.Period, s.Company);
        await Factory.AddDiaryAsync(second, s.Period, (await Factory.PastWorkDaysAsync(s.Period, 1))[0]);

        var after = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        after.Counts.Applications.Should().Be(nav.Counts.Applications + 1);
        after.Counts.Diaries.Should().Be(nav.Counts.Diaries + 1);
        after.Counts.Today.Should().Be(nav.Counts.Today + 1);
    }

    [Fact]
    public async Task Nav_Talaba_403_Anonim_401()
    {
        var tutor = await Factory.LoginAsNewStudentAsync();
        (await tutor.GetAsync("/api/admin/nav")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().GetAsync("/api/admin/nav")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<int> TotalAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync($"/api/admin/{path}?pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("total").GetInt32();
    }
}

/// <summary>Bo'sh baza (faqat seed: admin, sozlamalar, bayramlar, o'quv yili) — nol sonlar; faol o'quv yili
/// bo'lmasa <c>academicYear: null</c>.</summary>
[Collection(AdminEmptyDbCollection.Name)]
public sealed class AdminNavEmptyDbTests(AdminEmptyDbFixture fixture)
{
    [Fact]
    public async Task Nav_BoshBaza_NolSonlar_FaolYilYoq_Null()
    {
        var factory = fixture.Factory;
        var client = await factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/nav");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var nav = (await response.Content.ReadAsync<AdminNavDto>())!;
        nav.Counts.Should().Be(new AdminNavCounts(0, 0, 0, 0, 0, 0, 0));
        var seededYear = await factory.WithDbAsync(db => db.AcademicYears
            .Where(y => y.IsActive).OrderByDescending(y => y.StartDate).Select(y => y.Name).FirstOrDefaultAsync());
        nav.Context.AcademicYear.Should().Be(seededYear);

        var archived = await factory.WithDbAsync(async db =>
        {
            var years = await db.AcademicYears.Where(y => y.IsActive).ToListAsync();
            years.ForEach(y => y.Archive());
            await db.SaveChangesAsync();
            return years.Select(y => y.Id).ToList();
        });
        try
        {
            var body = await (await client.GetAsync("/api/admin/nav")).Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("context").GetProperty("academicYear").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            await factory.WithDbAsync(async db =>
            {
                var years = await db.AcademicYears.Where(y => archived.Contains(y.Id)).ToListAsync();
                years.ForEach(y => y.Activate());
                await db.SaveChangesAsync();
            });
        }
    }
}
