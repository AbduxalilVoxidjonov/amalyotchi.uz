using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminCompaniesTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Korxonalar_200_Shakl_Bayroqlar()
    {
        var marker = Guid.NewGuid().ToString("N")[..6];
        var plain = await Factory.CreateCompanyAsync(radiusM: 150, name: $"Korxona {marker} Oddiy");
        var large = await Factory.CreateCompanyAsync(radiusM: 600, name: $"Korxona {marker} Katta");
        var suspicious = await Factory.CreateCompanyAsync(radiusM: 600, name: $"Korxona {marker} Shubhali");

        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var s1 = await Factory.CreateStudentAsync(group: group);
        var s2 = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(s1, period, suspicious, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(s2, period, suspicious, tutor.Id);
        var today = Factory.Today();
        for (var d = 1; d <= 3; d++)
            await Factory.CheckInAsync(s1, period, today.AddDays(-d), suspicious: true, distanceM: 700);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/companies?q=korxona%20{marker}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var items = json.RootElement.GetProperty("items");
            items.GetArrayLength().Should().Be(3);
            items.EnumerateArray().Select(i => i.GetProperty("flag").ValueKind)
                .Should().Contain(JsonValueKind.Null).And.Contain(JsonValueKind.String);
            items.EnumerateArray().Select(i => i.GetProperty("flag").ToString())
                .Should().Contain("largeRadius").And.Contain("suspicious");
        }

        var page = await client.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={marker}");
        page.Total.Should().Be(3);

        var p = page.Items.Single(c => c.Id == plain.Id);
        p.Flag.Should().BeNull();
        p.Tin.Should().HaveLength(9).And.MatchRegex("^[0-9]{9}$", "STIR xom");
        p.Students.Should().Be(0);

        var l = page.Items.Single(c => c.Id == large.Id);
        l.RadiusM.Should().Be(600);
        l.Flag.Should().Be(CompanyFlag.LargeRadius);

        var s = page.Items.Single(c => c.Id == suspicious.Id);
        s.Students.Should().Be(2);
        s.SuspiciousDays.Should().Be(3);
        s.Flag.Should().Be(CompanyFlag.Suspicious, "shubhali bayroq katta radiusdan ustun");
    }

    [Fact]
    public async Task Korxonalar_StirBoyichaQidiruv_VaSahifalash()
    {
        var company = await Factory.CreateCompanyAsync();

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={company.Tin}&pageSize=5");

        page.Total.Should().Be(1);
        page.PageSize.Should().Be(5);
        page.Items.Single().Id.Should().Be(company.Id);
        page.Items.Single().Address.Should().Be(company.Address);
    }

    [Fact]
    public async Task Korxonalar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/companies")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
