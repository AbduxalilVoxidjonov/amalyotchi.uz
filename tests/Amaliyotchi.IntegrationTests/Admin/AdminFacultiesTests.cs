using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Faculties;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminFacultiesTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Fakultetlar_200_Shakl_Hisoblar()
    {
        var facultyId = await Factory.CreateFacultyAsync("Fakultet Sinov Alpha");
        var group = await Factory.CreateGroupAsync(facultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var company = await Factory.CreateCompanyAsync();
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var s1 = await Factory.CreateStudentAsync(group: group);
        var s2 = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(s1, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(s2, period, company, tutor.Id);
        await Factory.CheckInAsync(s1, period, Factory.Today());

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync("/api/admin/faculties?q=Sinov%20Alpha");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var root = json.RootElement;
            root.TryGetProperty("items", out _).Should().BeTrue();
            root.TryGetProperty("pageSize", out _).Should().BeTrue("camelCase");
            root.GetProperty("items")[0].GetProperty("status").ValueKind.Should().Be(JsonValueKind.String, "enum string");
        }

        var page = JsonSerializer.Deserialize<Paged<FacultyRow>>(body, JsonDefaults.Options)!;
        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(facultyId);
        row.Directions.Should().Be(1);
        row.Groups.Should().Be(1);
        row.Students.Should().Be(2);
        row.Tutors.Should().Be(1);
        row.AttendancePct.Should().BeInRange(0, 100);
        row.Status.Should().BeOneOf(FacultyStatus.Active, FacultyStatus.Attention);
        if (row.AttendancePct > 0)
        {
            // Bugun ish kuni bo'lsa: 2 talabadan 1 tasi keldi → 50% → attention.
            row.AttendancePct.Should().Be(50);
            row.Status.Should().Be(FacultyStatus.Attention);
        }
    }

    [Fact]
    public async Task Fakultetlar_Qidiruv_CaseInsensitive_VaSahifalash()
    {
        var marker = Guid.NewGuid().ToString("N")[..6];
        await Factory.CreateFacultyAsync($"Qidiruv {marker} Birinchi");
        await Factory.CreateFacultyAsync($"Qidiruv {marker} Ikkinchi");

        var client = await Factory.LoginAsAdminAsync();

        var page1 = await client.GetFacultiesAsync($"/api/admin/faculties?q={marker.ToUpperInvariant()}&page=1&pageSize=1");
        page1.Total.Should().Be(2);
        page1.Items.Should().HaveCount(1);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(1);

        var page2 = await client.GetFacultiesAsync($"/api/admin/faculties?q={marker}&page=2&pageSize=1");
        page2.Items.Should().HaveCount(1);
        page2.Items.Single().Id.Should().NotBe(page1.Items.Single().Id);

        var none = await client.GetFacultiesAsync("/api/admin/faculties?q=bunday-fakultet-yoq-%25%25");
        none.Total.Should().Be(0);
        none.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Fakultetlar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/faculties")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Fakultetlar_UzunQidiruv_400Validation()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/faculties?q=" + new string('a', 150));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("Q", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Yaratish_201_KodniKichikHarfdanUppercaseGaOtkazadiVaRoyxatdaKorinadi()
    {
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6];

        var response = await client.PostJsonAsync("/api/admin/faculties", new { name = $"Yangi Fakultet {marker}", code = $"y{marker[..4]}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<FacultyDto>();
        dto.Should().NotBeNull();
        dto!.Name.Should().Be($"Yangi Fakultet {marker}");
        dto.Code.Should().Be($"Y{marker[..4]}".ToUpperInvariant());
        dto.IsActive.Should().BeTrue();

        var page = await client.GetFacultiesAsync($"/api/admin/faculties?q={marker}");
        page.Items.Should().ContainSingle(f => f.Id == dto.Id && f.Code == dto.Code);
    }

    [Fact]
    public async Task Yaratish_TakroriyKod_409()
    {
        var client = await Factory.LoginAsAdminAsync();
        var code = "DUP" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        await Factory.CreateFacultyAsync($"Birinchi {code}");
        await Factory.WithDbAsync(async db =>
        {
            var faculty = Amaliyotchi.Domain.Organization.Faculty.Create($"Birinchi {code}", code);
            db.Faculties.Add(faculty);
            await db.SaveChangesAsync();
        });

        var response = await client.PostJsonAsync("/api/admin/faculties", new { name = "Ikkinchi", code = code.ToLowerInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_Validatsiya_BoshNom_400()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/faculties", new { name = "", code = "AB" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("Name", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Yangilash_200_NomVaKodOzgaradi()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync("Eski Nom");
        var marker = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var response = await client.PutAsJsonAsync($"/api/admin/faculties/{facultyId}", new { name = "Yangi Nom", code = marker });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<FacultyDto>();
        dto!.Id.Should().Be(facultyId);
        dto.Name.Should().Be("Yangi Nom");
        dto.Code.Should().Be(marker);
    }

    [Fact]
    public async Task Yangilash_BoshqaFakultetningKodiga_409()
    {
        var client = await Factory.LoginAsAdminAsync();
        var other = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await Factory.WithDbAsync(async db =>
        {
            var faculty = Amaliyotchi.Domain.Organization.Faculty.Create("Band Kod", other);
            db.Faculties.Add(faculty);
            await db.SaveChangesAsync();
        });
        var facultyId = await Factory.CreateFacultyAsync("Ozgaruvchi");

        var response = await client.PutAsJsonAsync($"/api/admin/faculties/{facultyId}", new { name = "Ozgaruvchi", code = other });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/faculties/{Guid.CreateVersion7()}", new { name = "Yoq", code = "YQ" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("detail").GetString().Should().Be("Fakultet topilmadi.");
    }

    [Fact]
    public async Task Ochirish_204_RoyxatdanChiqadi_QaytaOchirish404()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync("Ochiriladigan");

        var delete = await client.DeleteAsync($"/api/admin/faculties/{facultyId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await client.GetFacultiesAsync($"/api/admin/faculties?q={facultyId}");
        page.Items.Should().BeEmpty();

        var again = await client.DeleteAsync($"/api/admin/faculties/{facultyId}");
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ochirish_BogliqGuruhVaTyutorBolsa_409()
    {
        var client = await Factory.LoginAsAdminAsync();
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateTutorAsync(group);

        var response = await client.DeleteAsync($"/api/admin/faculties/{group.FacultyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ochirish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Tyutor Uchun");
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.DeleteAsync($"/api/admin/faculties/{facultyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Holat_DeactivateVaActivate_RoyxatdaKorinadi()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync("Holat Sinovi");

        var deactivate = await client.PatchAsJsonAsync($"/api/admin/faculties/{facultyId}/status", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());
        var deactivated = await deactivate.Content.ReadAsync<FacultyDto>();
        deactivated!.IsActive.Should().BeFalse();

        var afterDeactivate = await client.GetFacultiesAsync($"/api/admin/faculties?q=Holat Sinovi");
        afterDeactivate.Items.Single(f => f.Id == facultyId).IsActive.Should().BeFalse();

        var activate = await client.PatchAsJsonAsync($"/api/admin/faculties/{facultyId}/status", new { isActive = true });
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        var activated = await activate.Content.ReadAsync<FacultyDto>();
        activated!.IsActive.Should().BeTrue();

        var afterActivate = await client.GetFacultiesAsync($"/api/admin/faculties?q=Holat Sinovi");
        afterActivate.Items.Single(f => f.Id == facultyId).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Holat_Topilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PatchAsJsonAsync(
            $"/api/admin/faculties/{Guid.CreateVersion7()}/status", new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

internal static class AdminHttp
{
    public static async Task<Paged<FacultyRow>> GetFacultiesAsync(this HttpClient client, string url)
        => await client.GetPagedAsync<FacultyRow>(url);

    public static async Task<Paged<T>> GetPagedAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<Paged<T>>())!;
    }
}
