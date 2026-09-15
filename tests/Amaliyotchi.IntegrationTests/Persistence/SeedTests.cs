using System.Net;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public sealed class SeedTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Seed_IkkiMarta_Idempotent()
    {
        // Fixture allaqachon bir marta seed qilgan; yana ikki marta.
        await DatabaseInitializer.MigrateAndSeedAsync(Factory.Services, includeDemo: false);
        await DatabaseInitializer.MigrateAndSeedAsync(Factory.Services, includeDemo: false);

        await Factory.WithDbAsync(async db =>
        {
            var settings = await db.AppSettings.ToListAsync();
            settings.Should().HaveCount(SettingKeys.All.Count);
            settings.Select(s => s.Key).Should().BeEquivalentTo(SettingKeys.All.Select(d => d.Key));
            settings.Single(s => s.Key == SettingKeys.GeofenceRadius).Value.Should().Be("200");
            settings.Single(s => s.Key == SettingKeys.WorkDays).Value.Should().Be("1,2,3,4,5,6");

            (await db.Users.CountAsync(u => u.HemisId == ApiFactory.SeedAdminHemisId)).Should().Be(1);
            (await db.Holidays.CountAsync()).Should().Be(7);
            (await db.Holidays.AllAsync(h => h.IsRecurring)).Should().BeTrue();
            (await db.DocumentTemplates.AnyAsync()).Should().BeFalse("shablonlar bo'sh — admin yuklaydi");
        });
    }

    [Fact]
    public async Task SeedAdmin_LoginIshlaydi_RoleAdminString()
    {
        var client = Factory.CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login", new { HemisId = ApiFactory.SeedAdminHemisId, Password = ApiFactory.SeedAdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"role\":\"admin\"");

        var auth = await response.Content.ReadAsync<AuthResultDto>();
        auth!.User.Role.Should().Be(UserRole.Admin);
        auth.User.GroupId.Should().BeNull();
        auth.User.HemisId.Should().Be(ApiFactory.SeedAdminHemisId);
    }
}
