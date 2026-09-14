using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminAuditTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Audit_200_Shakl_YangisiBirinchi()
    {
        var tutor = await Factory.CreateTutorAsync(fullName: "Audit Tyutor");
        await Factory.LoginAsync(tutor); // loggedIn yozuvi

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync("/api/admin/audit?pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("pageSize").GetInt32().Should().Be(10);
        root.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        var first = root.GetProperty("items")[0];
        first.TryGetProperty("entityName", out _).Should().BeTrue("camelCase");
        first.GetProperty("action").ValueKind.Should().Be(JsonValueKind.String, "enum string");
        first.GetProperty("at").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T", "ISO 8601");

        var page = await client.GetPagedAsync<AuditEntryDto>("/api/admin/audit?pageSize=10");
        page.Items.Should().BeInDescendingOrder(a => a.At);
    }

    [Fact]
    public async Task Audit_ActionFiltri_VaQidiruv()
    {
        var tutor = await Factory.CreateTutorAsync(fullName: "Audit Filtr Tyutor");
        await Factory.LoginAsync(tutor);

        var client = await Factory.LoginAsAdminAsync();

        var byAction = await client.GetPagedAsync<AuditEntryDto>("/api/admin/audit?action=loggedIn&pageSize=50");
        byAction.Total.Should().BeGreaterThan(0);
        byAction.Items.Should().OnlyContain(a => a.Action == AuditAction.LoggedIn);
        byAction.Items.Should().Contain(a => a.UserId == tutor.Id && a.UserName == "Audit Filtr Tyutor" && a.UserRole == UserRole.Tutor);

        var byName = await client.GetPagedAsync<AuditEntryDto>("/api/admin/audit?q=audit%20filtr%20tyutor");
        byName.Total.Should().BeGreaterThan(0);
        byName.Items.Should().OnlyContain(a => a.UserId == tutor.Id);

        var byEntityId = await client.GetPagedAsync<AuditEntryDto>($"/api/admin/audit?q={tutor.Id}&action=created");
        byEntityId.Items.Should().Contain(a => a.EntityName == "User" && a.EntityId == tutor.Id.ToString());
    }

    [Fact]
    public async Task Audit_NotogriAction_400()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/audit?action=bunday-amal-yoq");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Audit_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
