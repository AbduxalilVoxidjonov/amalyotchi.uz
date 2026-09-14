using System.Net;
using Amaliyotchi.Application.Features.Reports;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class ReportsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Katalog_Tyutor_GuruhlarVaDavrSanalari()
    {
        var s = await Factory.CreateTutorScenarioAsync();

        var response = await s.Client.GetAsync("/api/reports");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadAsync<ReportsCatalog>())!;
        body.Filter.Groups.Should().Equal(s.Group.GroupName);
        body.Filter.Scope.Should().Be(s.Group.GroupName);
        body.Filter.StudentCount.Should().Be(1);
        body.Filter.DateFrom.Should().Be(s.Period.StartDate);
        body.Filter.DateTo.Should().Be(s.Period.EndDate);
        body.Reports.Should().NotBeEmpty();
        body.Reports.Should().OnlyContain(r => !r.Available && r.Note != null && r.Formats.Count > 0);
        body.Reports.Select(r => r.Id).Should().Contain(["attendance", "portfolio", "company-reference"]);
        body.Reports.Single(r => r.Id == "attendance").Formats.Should().Equal("xlsx");
    }

    [Fact]
    public async Task Katalog_Admin_BarchaFakultetlar_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var body = (await (await admin.GetAsync("/api/reports")).Content.ReadAsync<ReportsCatalog>())!;
        body.Filter.Scope.Should().Be("Barcha fakultetlar");
        body.Filter.Groups.Should().BeEmpty();
        body.Filter.StudentCount.Should().BeGreaterThanOrEqualTo(1);
        body.Filter.DateFrom.Should().NotBeNull();

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync("/api/reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/reports")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
