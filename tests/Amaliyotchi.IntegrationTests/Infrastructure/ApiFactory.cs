using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Infrastructure.Persistence;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Haqiqiy API (Program.cs) — faqat konfiguratsiya almashtiriladi: Testcontainers Postgres,
/// test JWT kaliti, test Telegram bot tokeni, vaqtinchalik fayl papkasi, keng rate-limit, jim log.
/// <c>IClock</c> — <see cref="MutableClock"/> (sukut bo'yicha haqiqiy vaqt; <see cref="Clock"/>.Set/Reset).
/// Startup seed o'chirilgan — <see cref="InitializeDatabaseAsync"/> migratsiya + <c>DbSeeder</c> ni o'zi chaqiradi.</summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>Integratsiya testlari shu token bilan initData imzolaydi (<c>TelegramInitDataFactory</c>).</summary>
    public const string TelegramBotToken = "1234567890:TEST-bot-token-for-integration-tests";

    public const string JwtSigningKey = "integration-tests-signing-key-kamida-32-belgi-!!";

    /// <summary><c>DbSeeder</c> yaratadigan birinchi admin (Testing muhitida konfigdan olinadi).</summary>
    public const string SeedAdminHemisId = "900000000001";
    public const string SeedAdminPassword = "Seed-Admin-12345";

    public string StorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "amaliyotchi-tests", Guid.CreateVersion7().ToString("N"));

    /// <summary>API'dagi <c>IClock</c> — vaqtga bog'liq testlar uchun (<c>Set</c> → <c>finally { Reset(); }</c>).</summary>
    public MutableClock Clock { get; } = new();

    /// <summary>Telegram o'rniga (<c>ITelegramMessenger</c>) — "Xabarlar" testlari natijalarni shu yerda skriptlaydi.</summary>
    public FakeTelegramMessenger Messenger { get; } = new();

    /// <summary>Yuz dvigateli o'rniga (<c>IFaceEngine</c>) — deterministik soxta (<see cref="FakeFaceEngine"/>).</summary>
    public FakeFaceEngine FaceEngine { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
            services.Replace(ServiceDescriptor.Singleton<ITelegramMessenger>(Messenger));
            services.Replace(ServiceDescriptor.Singleton<IFaceEngine>(FaceEngine));
        });

        builder.UseSetting("ConnectionStrings:Postgres", connectionString);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Jwt:AccessTokenMinutes", "30");
        builder.UseSetting("Telegram:BotToken", TelegramBotToken);
        builder.UseSetting("Telegram:MaxAgeSeconds", "86400");
        // Bot polling testlarda hech qachon ishga tushmasin (tarmoq yo'q, soxta token).
        builder.UseSetting("Telegram:BotEnabled", "false");
        // "Xabarlar" fon tsikli o'chiq — testlar BroadcastDispatcher.RunOnceAsync ni o'zi chaqiradi (deterministik).
        builder.UseSetting("Messaging:DispatcherEnabled", "false");
        builder.UseSetting("Messaging:MessagesPerSecond", "1000");
        builder.UseSetting("Messaging:BatchSize", "200");
        builder.UseSetting("Storage:RootPath", StorageRoot);
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("Seed:Demo", "false");
        builder.UseSetting("Seed:AdminHemisId", SeedAdminHemisId);
        builder.UseSetting("Seed:AdminPassword", SeedAdminPassword);
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:RefreshPerMinute", "100000");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command", "Warning");
    }

    /// <summary>Migratsiya + asosiy seed (demo emas) — ishlab chiqarishdagi bilan bir xil yo'l.</summary>
    public Task InitializeDatabaseAsync() =>
        DatabaseInitializer.MigrateAndSeedAsync(Services, includeDemo: false);

    /// <summary>Test ma'lumotlarini to'g'ridan-to'g'ri bazaga yozish uchun (alohida scope — so'rovnikidan ajratilgan).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}
