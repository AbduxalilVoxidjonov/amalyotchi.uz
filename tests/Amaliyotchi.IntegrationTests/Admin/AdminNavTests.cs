using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Nav;
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
        var tutor = await Factory.CreateTutorAsync(group, "Nav Tyutor");
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
            foreach (var key in new[] { "faculties", "tutors", "companies", "students" })
                counts.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Number, key);
            json.RootElement.GetProperty("context").TryGetProperty("academicYear", out _).Should().BeTrue();
        }

        var nav = JsonSerializer.Deserialize<AdminNavDto>(body, JsonDefaults.Options)!;
        nav.Counts.Faculties.Should().Be(await TotalAsync(client, "faculties"));
        nav.Counts.Tutors.Should().Be(await TotalAsync(client, "tutors"));
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

    [Fact]
    public async Task Nav_Tyutor_403_Anonim_401()
    {
        var tutor = await Factory.LoginAsTutorAsync();
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
        nav.Counts.Should().Be(new AdminNavCounts(0, 0, 0, 0));
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
