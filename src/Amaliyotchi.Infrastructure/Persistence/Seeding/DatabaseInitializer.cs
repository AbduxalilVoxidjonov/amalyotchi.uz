using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary>Startup: migratsiya → asosiy seed → (<c>Seed:Demo</c>) demo seed.
/// <c>Program.cs</c> dan <c>Seed:Enabled</c> bo'lsa chaqiriladi; integratsiya testlari ham shu usulni ishlatadi.</summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(
        IServiceProvider services, bool includeDemo, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var db = provider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Migratsiyalar qo'llandi");

        await provider.GetRequiredService<DbSeeder>().SeedAsync(cancellationToken);

        if (!includeDemo)
            return;

        // Demo ma'lumot faqat demo/stend uchun: Seed:Demo default false (appsettings.json), Development'da true.
        // Docker stendida Production muhitida ham Seed__Demo=true bilan yuklanadi — muhitga bog'lanmaydi.
        var environment = provider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
            logger.LogWarning("Seed:Demo=true — demo ma'lumot {Environment} muhitiga yuklanmoqda (faqat demo/stend uchun)", environment.EnvironmentName);

        await provider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken);
    }

    /// <summary><c>Seed:Enabled</c> — konfigda aniq berilmagan bo'lsa Development'da <c>true</c>.</summary>
    public static bool IsEnabled(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var raw = configuration[$"{SeedOptions.SectionName}:{nameof(SeedOptions.Enabled)}"];
        if (bool.TryParse(raw, out var enabled))
            return enabled;
        return environment.IsDevelopment();
    }

    public static bool IsDemoEnabled(IServiceProvider services)
        => services.GetRequiredService<IOptions<SeedOptions>>().Value.Demo;
}
