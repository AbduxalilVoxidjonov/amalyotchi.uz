using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Persistence.Seeding;

/// <summary>Har startup'da ishlaydigan idempotent seed: sozlama default'lari (yo'q kalitlar),
/// birinchi admin (admin bo'lmasa), O'zbekiston bayramlari (bo'sh bo'lsa).
/// Hujjat shablonlari — bo'sh (admin yuklaydi).</summary>
public sealed class DbSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IClock clock,
    IOptions<SeedOptions> options,
    IHostEnvironment environment,
    ILogger<DbSeeder> logger)
{
    /// <summary>Takrorlanadigan bayramlar (oy, kun, nom). Sana yili ahamiyatsiz — <c>IsRecurring</c>.</summary>
    private static readonly (int Month, int Day, string Name)[] RecurringHolidays =
    [
        (1, 1, "Yangi yil"),
        (3, 8, "Xalqaro xotin-qizlar kuni"),
        (3, 21, "Navro'z bayrami"),
        (5, 9, "Xotira va qadrlash kuni"),
        (9, 1, "Mustaqillik kuni"),
        (10, 1, "O'qituvchi va murabbiylar kuni"),
        (12, 8, "Konstitutsiya kuni")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var changed = false;

        changed |= await SeedSettingsAsync(now, cancellationToken);
        changed |= await SeedAdminAsync(cancellationToken);
        changed |= await SeedHolidaysAsync(cancellationToken);

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> SeedSettingsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.AppSettings.Select(s => s.Key).ToListAsync(cancellationToken);
        var missing = SettingKeys.All.Where(d => !existing.Contains(d.Key)).ToList();
        foreach (var definition in missing)
            db.AppSettings.Add(AppSetting.CreateDefault(definition.Key, now));

        if (missing.Count > 0)
            logger.LogInformation("Seed: {Count} ta sozlama default qiymat bilan qo'shildi", missing.Count);

        return missing.Count > 0;
    }

    private async Task<bool> SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
            return false;

        var (hemisId, password) = ResolveAdminCredentials();
        if (hemisId is null || password is null)
        {
            logger.LogWarning(
                "Seed: admin yo'q va Seed:AdminHemisId/Seed:AdminPassword sozlanmagan — admin yaratilmadi");
            return false;
        }

        var admin = User.CreateWithPassword(
            options.Value.AdminFullName, hemisId, phoneNumber: null, passwordHasher.Hash(password), UserRole.Admin);
        db.Users.Add(admin);
        logger.LogInformation("Seed: birinchi admin yaratildi (HEMIS ID {HemisId})", HemisId.Normalize(hemisId));
        return true;
    }

    private async Task<bool> SeedHolidaysAsync(CancellationToken cancellationToken)
    {
        if (await db.Holidays.AnyAsync(cancellationToken))
            return false;

        foreach (var (month, day, name) in RecurringHolidays)
            db.Holidays.Add(Holiday.Create(new DateOnly(2000, month, day), name, isRecurring: true));

        logger.LogInformation("Seed: {Count} ta takrorlanadigan bayram qo'shildi", RecurringHolidays.Length);
        return true;
    }

    /// <summary>Konfigdan; Development'da bo'sh bo'lsa — mock'lardagi standart hisob.</summary>
    private (string? HemisId, string? Password) ResolveAdminCredentials()
    {
        var hemisId = options.Value.AdminHemisId;
        var password = options.Value.AdminPassword;

        if (environment.IsDevelopment())
        {
            hemisId = string.IsNullOrWhiteSpace(hemisId) ? SeedOptions.DevelopmentAdminHemisId : hemisId;
            password = string.IsNullOrWhiteSpace(password) ? SeedOptions.DevelopmentAdminPassword : password;
        }

        return (string.IsNullOrWhiteSpace(hemisId) ? null : hemisId, string.IsNullOrWhiteSpace(password) ? null : password);
    }
}
