using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Groups;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

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
}
