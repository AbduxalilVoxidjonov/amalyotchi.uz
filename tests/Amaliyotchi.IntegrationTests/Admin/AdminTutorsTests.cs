using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminTutorsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Tyutorlar_200_Shakl_KechikkanAriza_Late()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateTutorAsync(groupA, "Kechikkan Tyutor", groupB.GroupId);
        var period = await Factory.CreateActivePeriodAsync(groupA, tutor.Id, groupB.GroupId);
        var company = await Factory.CreateCompanyAsync();
        var s1 = await Factory.CreateStudentAsync(group: groupA);
        var s2 = await Factory.CreateStudentAsync(group: groupB);
        await Factory.CreatePendingApplicationAsync(s1, period, company, TimeSpan.FromHours(50));
        await Factory.CreatePendingApplicationAsync(s2, period, company, TimeSpan.FromHours(1));
        await Factory.LoginAsync(tutor); // LastLoginAt

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/tutors?q={tutor.PhoneNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var item = json.RootElement.GetProperty("items")[0];
            item.GetProperty("status").GetString().Should().Be("late");
            item.GetProperty("phone").GetString().Should().Be(tutor.PhoneNumber, "E.164 xom");
            item.GetProperty("groups").ValueKind.Should().Be(JsonValueKind.Array);
        }

        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.PhoneNumber}");
        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(tutor.Id);
        row.FullName.Should().Be("Kechikkan Tyutor");
        row.FacultyId.Should().Be(groupA.FacultyId);
        row.FacultyCode.Should().NotBeNullOrEmpty();
        row.Groups.Should().BeEquivalentTo([groupA.GroupName, groupB.GroupName]);
        row.Students.Should().Be(2);
        row.Pending.Should().Be(2);
        row.OldestPendingAt.Should().NotBeNull();
        row.LastActiveAt.Should().NotBeNull();
        row.Status.Should().Be(TutorStatus.Late);
    }

    [Fact]
    public async Task Tyutorlar_YangiAriza_Active_VaIsmBoyichaQidiruv()
    {
        var group = await Factory.CreateGroupAsync();
        var marker = "Faol" + Guid.NewGuid().ToString("N")[..6];
        var tutor = await Factory.CreateTutorAsync(group, marker);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreatePendingApplicationAsync(student, period, company, TimeSpan.FromHours(2));

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={marker.ToUpperInvariant()}");

        page.Total.Should().Be(1);
        page.Items.Single().Pending.Should().Be(1);
        page.Items.Single().Status.Should().Be(TutorStatus.Active);
    }

    [Fact]
    public async Task Tyutorlar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/tutors")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
