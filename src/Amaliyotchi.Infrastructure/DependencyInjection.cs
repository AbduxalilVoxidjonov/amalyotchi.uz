using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Infrastructure.Excel;
using Amaliyotchi.Infrastructure.Identity;
using Amaliyotchi.Infrastructure.Persistence;
using Amaliyotchi.Infrastructure.Persistence.Interceptors;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Amaliyotchi.Infrastructure.Services;
using Amaliyotchi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres sozlanmagan. appsettings yoki muhit o'zgaruvchisini tekshiring.");

        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            ConfigureNpgsql(options, connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && o.SigningKey.Length >= 32,
                "Jwt:SigningKey kamida 32 ta belgidan iborat bo'lishi kerak.")
            .ValidateOnStart();

        services.AddScoped<ITokenService, JwtTokenService>();

        // Telegram Mini App: bot token bo'sh bo'lsa ilova ishga tushadi, lekin /auth/telegram 403 qaytaradi
        // (validator "sozlanmagan" deb rad etadi) — admin/tyutor oqimi tokensiz ham ishlashi kerak.
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(o => o.MaxAgeSeconds > 0, "Telegram:MaxAgeSeconds musbat bo'lishi kerak.")
            .ValidateOnStart();
        services.AddSingleton<ITelegramInitDataValidator, TelegramInitDataValidator>();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.RootPath), "Storage:RootPath bo'sh bo'lishi mumkin emas.")
            .ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Talabalar importi: shablon yasash va yuklangan .xlsx ni o'qish (holatsiz — singleton).
        services.AddSingleton<IStudentImportExcel, StudentImportExcel>();

        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<DbSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }

    /// <summary>Npgsql sozlamalari bitta joyda — ishlab chiqarish DI va design-time factory
    /// (<c>dotnet ef</c>) bir xil modelni qurishi shart, aks holda snapshot farq qiladi.</summary>
    public static DbContextOptionsBuilder ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();          // M06: korxona koordinatalari uchun
            npgsql.EnableRetryOnFailure(3);
            npgsql.MigrationsHistoryTable("__migrations");
        });
}
