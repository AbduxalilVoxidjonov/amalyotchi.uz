using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Settings;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminSettingsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Sozlamalar_Get_200_Shakl()
    {
        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync("/api/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var root = json.RootElement;
            root.TryGetProperty("settings", out _).Should().BeTrue();
            root.TryGetProperty("holidays", out _).Should().BeTrue();
            root.TryGetProperty("templates", out _).Should().BeTrue();
            var first = root.GetProperty("settings")[0];
            first.GetProperty("type").ValueKind.Should().Be(JsonValueKind.String, "enum string: int/bool/weekdays");
            first.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String, "xom qiymat");
        }

        var dto = JsonSerializer.Deserialize<AdminSettingsDto>(body, JsonDefaults.Options)!;
        dto.Settings.Select(s => s.Key).Should().Equal(SettingKeys.All.Select(d => d.Key));
        var radius = dto.Settings.Single(s => s.Key == SettingKeys.GeofenceRadius);
        radius.Type.Should().Be(SettingType.Int);
        radius.Unit.Should().Be("m");
        radius.Min.Should().Be(50);
        radius.Max.Should().Be(1000);
        radius.Label.Should().NotBeNullOrEmpty();
        dto.Settings.Single(s => s.Key == SettingKeys.WorkDays).Type.Should().Be(SettingType.Weekdays);
        dto.Settings.Single(s => s.Key == SettingKeys.DailyReportRequired).Value.Should().BeOneOf("true", "false");

        dto.Holidays.Should().NotBeEmpty("seed bayramlari");
        dto.Holidays.Should().OnlyContain(h => h.IsRecurring);
        dto.Holidays.Select(h => (h.Date.Month, h.Date.Day)).Should().BeInAscendingOrder();
        body.Should().MatchRegex("\"date\":\"\\d{4}-\\d{2}-\\d{2}\"", "DateOnly ISO");
    }

    [Fact]
    public async Task Sozlamalar_Put_Valid_200_Audit_VaQaytarish()
    {
        var client = await Factory.LoginAsAdminAsync();
        var before = (await client.GetFromJsonAsync<AdminSettingsDto>("/api/admin/settings"))!;
        var oldRadius = before.Settings.Single(s => s.Key == SettingKeys.GeofenceRadius).Value;
        var oldRequired = before.Settings.Single(s => s.Key == SettingKeys.DailyReportRequired).Value;
        var newRadius = oldRadius == "250" ? "300" : "250";

        try
        {
            var response = await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string>
                {
                    [SettingKeys.GeofenceRadius] = $" {newRadius} ",
                    [SettingKeys.DailyReportRequired] = "ha" // normalizatsiya → "true"
                }
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var updated = (await response.Content.ReadAsync<AdminSettingsDto>())!;
            updated.Settings.Single(s => s.Key == SettingKeys.GeofenceRadius).Value.Should().Be(newRadius);
            updated.Settings.Single(s => s.Key == SettingKeys.GeofenceRadius).UpdatedAt.Should().NotBeNull();
            updated.Settings.Single(s => s.Key == SettingKeys.DailyReportRequired).Value.Should().Be("true");

            var again = (await client.GetFromJsonAsync<AdminSettingsDto>("/api/admin/settings"))!;
            again.Settings.Single(s => s.Key == SettingKeys.GeofenceRadius).Value.Should().Be(newRadius, "saqlangan");

            var audit = await client.GetPagedAsync<AuditEntryDto>("/api/admin/audit?action=settingsChanged&pageSize=5");
            audit.Items.Should().NotBeEmpty();
            var entry = audit.Items[0];
            entry.Action.Should().Be(AuditAction.SettingsChanged);
            entry.EntityName.Should().Be(nameof(AppSetting));
            entry.UserRole.Should().Be(UserRole.Admin);
            entry.Changes.Should().Contain(SettingKeys.GeofenceRadius).And.Contain(newRadius);
        }
        finally
        {
            // Umumiy baza — boshqa testlar (SeedTests) default qiymatga tayanadi.
            var restore = await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string>
                {
                    [SettingKeys.GeofenceRadius] = oldRadius,
                    [SettingKeys.DailyReportRequired] = oldRequired
                }
            });
            restore.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Sozlamalar_Put_Invalid_400_ErrorsKalitBoyicha()
    {
        var client = await Factory.LoginAsAdminAsync();
        var before = (await client.GetFromJsonAsync<AdminSettingsDto>("/api/admin/settings"))!;

        var response = await client.PutAsJsonAsync("/api/admin/settings", new
        {
            values = new Dictionary<string, string>
            {
                [SettingKeys.GeofenceRadius] = "5000",   // max 1000
                [SettingKeys.WorkDays] = "1,9",          // 1..7
                [SettingKeys.DailyReportRequired] = "balki",
                ["nomalumKalit"] = "1"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(400);
        var errors = root.GetProperty("errors");
        errors.TryGetProperty(SettingKeys.GeofenceRadius, out var radiusErrors).Should().BeTrue();
        radiusErrors[0].GetString().Should().Contain("50–1000");
        errors.TryGetProperty(SettingKeys.WorkDays, out _).Should().BeTrue();
        errors.TryGetProperty(SettingKeys.DailyReportRequired, out _).Should().BeTrue();
        errors.TryGetProperty("nomalumKalit", out _).Should().BeTrue();

        var after = (await client.GetFromJsonAsync<AdminSettingsDto>("/api/admin/settings"))!;
        after.Settings.Select(s => s.Value).Should().Equal(before.Settings.Select(s => s.Value), "hech narsa o'zgarmagan");
    }

    [Fact]
    public async Task Sozlamalar_Put_BoshValues_400()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync("/api/admin/settings", new { values = new Dictionary<string, string>() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("Values", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Sozlamalar_Tyutor_403_GetVaPut()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync("/api/admin/settings", new { values = new { geofenceRadius = "300" } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
