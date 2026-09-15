using System.Net;
using Amaliyotchi.Application.Features.Admin.Directions;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminDirectionsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_200_Hisoblar()
    {
        var group = await Factory.CreateGroupAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        _ = student;

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<DirectionRow>($"/api/admin/departments/{group.DepartmentId}/directions");

        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(group.DirectionId);
        row.Groups.Should().Be(1);
        row.Students.Should().Be(1);
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Royxat_OtaTopilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/admin/departments/{Guid.CreateVersion7()}/directions");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_201_RoyxatdaKorinadi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Yo'nalish Yaratish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6];

        var response = await client.PostJsonAsync(
            $"/api/admin/departments/{departmentId}/directions", new { name = $"Yangi yo'nalish {marker}", code = $"y{marker[..4]}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<DirectionDto>();
        dto!.DepartmentId.Should().Be(departmentId);
        dto.FacultyId.Should().Be(facultyId);
        dto.Code.Should().Be($"Y{marker[..4]}".ToUpperInvariant());

        var page = await client.GetPagedAsync<DirectionRow>($"/api/admin/departments/{departmentId}/directions");
        page.Items.Should().ContainSingle(d => d.Id == dto.Id);
    }

    [Fact]
    public async Task Yaratish_OtaTopilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/departments/{Guid.CreateVersion7()}/directions", new { name = "Yo'nalish", code = "KOD" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_TakroriyKod_409()
    {
        var facultyId = await Factory.CreateFacultyAsync("Takroriy Yo'nalish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId, "Birinchi yo'nalish");
        var code = await Factory.WithDbAsync(async db =>
            (await db.Directions.SingleAsync(d => d.Id == directionId)).Code);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.PostJsonAsync(
            $"/api/admin/departments/{departmentId}/directions", new { name = "Ikkinchi yo'nalish", code = code.ToLowerInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_Validatsiya_BoshNom_400()
    {
        var facultyId = await Factory.CreateFacultyAsync("Validatsiya Fakulteti 2");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/departments/{departmentId}/directions", new { name = "", code = "AB" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Yaratish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Tyutor Yo'nalish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/departments/{departmentId}/directions", new { name = "Yo'nalish", code = "KOD" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Yangilash_200_NomVaKodOzgaradi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Yangilash Fakulteti 2");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId, "Eski nom");
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var response = await client.PutAsJsonAsync($"/api/admin/directions/{directionId}", new { name = "Yangi nom", code = marker });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<DirectionDto>();
        dto!.Name.Should().Be("Yangi nom");
        dto.Code.Should().Be(marker);
        dto.DepartmentId.Should().Be(departmentId);
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/directions/{Guid.CreateVersion7()}", new { name = "Yoq", code = "YQ" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holat_DeactivateVaActivate()
    {
        var facultyId = await Factory.CreateFacultyAsync("Holat Fakulteti 2");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsAdminAsync();

        var deactivate = await client.PatchAsJsonAsync($"/api/admin/directions/{directionId}/status", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivate.Content.ReadAsync<DirectionDto>())!.IsActive.Should().BeFalse();

        var activate = await client.PatchAsJsonAsync($"/api/admin/directions/{directionId}/status", new { isActive = true });
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activate.Content.ReadAsync<DirectionDto>())!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Ochirish_204_QaytaOchirish404()
    {
        var facultyId = await Factory.CreateFacultyAsync("Ochirish Fakulteti 2");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsAdminAsync();

        var delete = await client.DeleteAsync($"/api/admin/directions/{directionId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await client.DeleteAsync($"/api/admin/directions/{directionId}");
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ochirish_BolasiBolsa_409()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.DeleteAsync($"/api/admin/directions/{group.DirectionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ochirish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Tyutor Ochirish Fakulteti 2");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.DeleteAsync($"/api/admin/directions/{directionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
