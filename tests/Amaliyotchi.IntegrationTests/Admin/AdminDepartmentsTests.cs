using System.Net;
using Amaliyotchi.Application.Features.Admin.Departments;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminDepartmentsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_200_Hisoblar()
    {
        var facultyId = await Factory.CreateFacultyAsync("Kafedra Ro'yxati Fakulteti");
        var group = await Factory.CreateGroupAsync(facultyId);
        var student = await Factory.CreateStudentAsync(group: group);
        _ = student;

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<DepartmentRow>($"/api/admin/faculties/{facultyId}/departments");

        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(group.DepartmentId);
        row.Directions.Should().Be(1);
        row.Groups.Should().Be(1);
        row.Students.Should().Be(1);
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Royxat_OtaTopilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/admin/faculties/{Guid.CreateVersion7()}/departments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_201_RoyxatdaKorinadi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Yaratish Fakulteti");
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6];

        var response = await client.PostJsonAsync(
            $"/api/admin/faculties/{facultyId}/departments", new { name = $"Yangi kafedra {marker}", code = $"k{marker[..4]}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<DepartmentDto>();
        dto!.FacultyId.Should().Be(facultyId);
        dto.Name.Should().Be($"Yangi kafedra {marker}");
        dto.Code.Should().Be($"K{marker[..4]}".ToUpperInvariant());
        dto.IsActive.Should().BeTrue();

        var page = await client.GetPagedAsync<DepartmentRow>($"/api/admin/faculties/{facultyId}/departments");
        page.Items.Should().ContainSingle(d => d.Id == dto.Id);
    }

    [Fact]
    public async Task Yaratish_OtaTopilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/faculties/{Guid.CreateVersion7()}/departments", new { name = "Kafedra", code = "KOD" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_TakroriyKod_409()
    {
        var facultyId = await Factory.CreateFacultyAsync("Takroriy Kod Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId, "Birinchi kafedra");
        var code = await Factory.WithDbAsync(async db =>
            (await db.Departments.SingleAsync(d => d.Id == departmentId)).Code);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.PostJsonAsync(
            $"/api/admin/faculties/{facultyId}/departments", new { name = "Ikkinchi kafedra", code = code.ToLowerInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_Validatsiya_BoshNom_400()
    {
        var facultyId = await Factory.CreateFacultyAsync("Validatsiya Fakulteti");
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/faculties/{facultyId}/departments", new { name = "", code = "AB" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Yaratish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Tyutor Fakulteti");
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/faculties/{facultyId}/departments", new { name = "Kafedra", code = "KOD" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Yangilash_200_NomVaKodOzgaradi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Yangilash Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId, "Eski nom");
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var response = await client.PutAsJsonAsync($"/api/admin/departments/{departmentId}", new { name = "Yangi nom", code = marker });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<DepartmentDto>();
        dto!.Name.Should().Be("Yangi nom");
        dto.Code.Should().Be(marker);
        dto.FacultyId.Should().Be(facultyId);
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/departments/{Guid.CreateVersion7()}", new { name = "Yoq", code = "YQ" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holat_DeactivateVaActivate()
    {
        var facultyId = await Factory.CreateFacultyAsync("Holat Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsAdminAsync();

        var deactivate = await client.PatchAsJsonAsync($"/api/admin/departments/{departmentId}/status", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivate.Content.ReadAsync<DepartmentDto>())!.IsActive.Should().BeFalse();

        var activate = await client.PatchAsJsonAsync($"/api/admin/departments/{departmentId}/status", new { isActive = true });
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activate.Content.ReadAsync<DepartmentDto>())!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Ochirish_204_QaytaOchirish404()
    {
        var facultyId = await Factory.CreateFacultyAsync("Ochirish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsAdminAsync();

        var delete = await client.DeleteAsync($"/api/admin/departments/{departmentId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await client.DeleteAsync($"/api/admin/departments/{departmentId}");
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ochirish_BolasiBolsa_409()
    {
        var facultyId = await Factory.CreateFacultyAsync("Bolali Fakultet");
        var group = await Factory.CreateGroupAsync(facultyId);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.DeleteAsync($"/api/admin/departments/{group.DepartmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ochirish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Tyutor Ochirish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.DeleteAsync($"/api/admin/departments/{departmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
