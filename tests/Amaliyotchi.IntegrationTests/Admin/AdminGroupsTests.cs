using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Groups;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminGroupsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Guruhlar_200_Shakl_TyutorVaDavr()
    {
        var group = await Factory.CreateGroupAsync(course: 4);
        var tutor = await Factory.CreateTutorAsync(group, "Guruh Tyutori");
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id);
        var today = Factory.Today();
        // O'tgan 14 kunning har birida "keldi" — foiz 100 atrofida (ish kunlari bo'yicha).
        for (var d = 1; d <= 14; d++)
            await Factory.CheckInAsync(student, period, today.AddDays(-d));

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/groups?q={group.GroupName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var item = json.RootElement.GetProperty("items")[0];
            item.TryGetProperty("code", out _).Should().BeTrue("kontrakt: code");
            item.GetProperty("period").GetProperty("status").GetString().Should().Be("active", "enum camelCase string");
        }

        var page = await client.GetPagedAsync<GroupRow>($"/api/admin/groups?q={group.GroupName}");
        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(group.GroupId);
        row.Code.Should().Be(group.GroupName);
        row.Course.Should().Be(4);
        row.TutorId.Should().Be(tutor.Id);
        row.Tutor.Should().Be("Guruh Tyutori");
        row.Students.Should().Be(1);
        row.Period.Should().NotBeNull();
        row.Period!.Id.Should().Be(period.Id);
        row.Period.Status.Should().Be(PracticePeriodStatus.Active);
        row.Period.StartDate.Should().Be(period.StartDate);
        row.AttendancePct.Should().Be(100, "har o'tgan kun kelgan");
    }

    [Fact]
    public async Task Guruhlar_TyutorIsmiBoyichaQidiruv_DavrsizGuruhNull()
    {
        var marker = "Tyutor" + Guid.NewGuid().ToString("N")[..6];
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateTutorAsync(group, marker);

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<GroupRow>($"/api/admin/groups?q={marker.ToLowerInvariant()}");

        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(group.GroupId);
        page.Items.Single().Period.Should().BeNull();
        page.Items.Single().AttendancePct.Should().Be(0);
    }

    [Fact]
    public async Task Guruhlar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/groups")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoyxatYonalishBoyicha_200_OtaTopilmasa404()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var page = await client.GetPagedAsync<GroupRow>($"/api/admin/directions/{group.DirectionId}/groups");
        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(group.GroupId);

        var notFound = await client.GetAsync($"/api/admin/directions/{Guid.CreateVersion7()}/groups");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_201_RoyxatdaKorinadi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Guruh Yaratish Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var response = await client.PostJsonAsync(
            $"/api/admin/directions/{directionId}/groups", new { name = $"G-{marker}", course = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<GroupDto>();
        dto!.DirectionId.Should().Be(directionId);
        dto.Name.Should().Be($"G-{marker}");
        dto.Course.Should().Be(2);
        dto.IsActive.Should().BeTrue();
        dto.AcademicYear.Should().NotBeNullOrEmpty();

        var page = await client.GetPagedAsync<GroupRow>($"/api/admin/directions/{directionId}/groups");
        page.Items.Should().ContainSingle(g => g.Id == dto.Id);
    }

    [Fact]
    public async Task Yaratish_OtaTopilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/directions/{Guid.CreateVersion7()}/groups", new { name = "G-1", course = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Yaratish_TakroriyNom_409()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/directions/{group.DirectionId}/groups", new { name = group.GroupName.ToLowerInvariant(), course = group.Course });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_Validatsiya_NotogriNom_400()
    {
        var facultyId = await Factory.CreateFacultyAsync("Guruh Validatsiya Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/directions/{directionId}/groups", new { name = "guruh nomi bo'sh joy bilan", course = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Yaratish_TyutorTokeni_403()
    {
        var facultyId = await Factory.CreateFacultyAsync("Guruh Tyutor Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.PostJsonAsync(
            $"/api/admin/directions/{directionId}/groups", new { name = "G-1", course = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Yaratish_FaolOquvYiliYoq_409()
    {
        var facultyId = await Factory.CreateFacultyAsync("Guruh O'quv Yili Fakulteti");
        var departmentId = await Factory.CreateDepartmentAsync(facultyId);
        var directionId = await Factory.CreateDirectionAsync(departmentId);
        var client = await Factory.LoginAsAdminAsync();

        var activeYearIds = await Factory.WithDbAsync(async db =>
        {
            var active = await db.AcademicYears.Where(y => y.IsActive).ToListAsync();
            foreach (var year in active)
                year.Archive();
            await db.SaveChangesAsync();
            return active.Select(y => y.Id).ToList();
        });

        try
        {
            var response = await client.PostJsonAsync(
                $"/api/admin/directions/{directionId}/groups", new { name = "G-YQ", course = 1 });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            await Factory.WithDbAsync(async db =>
            {
                var years = await db.AcademicYears.Where(y => activeYearIds.Contains(y.Id)).ToListAsync();
                foreach (var year in years)
                    year.Activate();
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Yangilash_200_NomVaKursOzgaradi()
    {
        var group = await Factory.CreateGroupAsync(course: 2);
        var client = await Factory.LoginAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var response = await client.PutAsJsonAsync($"/api/admin/groups/{group.GroupId}", new { name = $"G-{marker}", course = 3 });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadAsync<GroupDto>();
        dto!.Name.Should().Be($"G-{marker}");
        dto.Course.Should().Be(3);
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/groups/{Guid.CreateVersion7()}", new { name = "G-YQ", course = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holat_DeactivateVaActivate()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var deactivate = await client.PatchAsJsonAsync($"/api/admin/groups/{group.GroupId}/status", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivate.Content.ReadAsync<GroupDto>())!.IsActive.Should().BeFalse();

        var activate = await client.PatchAsJsonAsync($"/api/admin/groups/{group.GroupId}/status", new { isActive = true });
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await activate.Content.ReadAsync<GroupDto>())!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Ochirish_204_QaytaOchirish404()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var delete = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ochirish_TalabaBolsa_409()
    {
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateStudentAsync(group: group);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ochirish_GuruhDarajasidagiKolamBolsa_409()
    {
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateTutorAsync(group); // guruh darajasidagi ko'lam
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ochirish_OtaKolamdanKelganBiriktiruv_204_BiriktiruvFaolsizlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var sibling = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(sibling);
        var client = await Factory.LoginAsAdminAsync();
        var scoped = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "department", id = group.DepartmentId } } });
        scoped.StatusCode.Should().Be(HttpStatusCode.OK, await scoped.Content.ReadAsStringAsync());
        (await scoped.Content.ReadAsync<TutorDetail>())!.Groups.Select(g => g.GroupId).Should().Contain(group.GroupId);

        var response = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = (await client.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!;
        detail.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([sibling.GroupId]);
        detail.Scopes.Should().ContainSingle(s => s.Level == TutorScopeLevel.Department, "ko'lamning o'zi qoladi");
        var assignment = await Factory.WithDbAsync(db =>
            db.TutorAssignments.SingleAsync(a => a.TutorUserId == tutor.Id && a.StudentGroupId == group.GroupId));
        assignment.IsActive.Should().BeFalse("tarix saqlanadi, faolsizlantiriladi");
    }

    [Fact]
    public async Task Ochirish_TyutorTokeni_403()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.DeleteAsync($"/api/admin/groups/{group.GroupId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
