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
