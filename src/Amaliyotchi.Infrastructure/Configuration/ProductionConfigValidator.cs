using Amaliyotchi.Infrastructure.Identity;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Amaliyotchi.Infrastructure.Configuration;

/// <summary>Production muhitida xavfli/default sozlamalar bilan ishga tushishni taqiqlaydi (fail-fast).
/// Faqat <c>ASPNETCORE_ENVIRONMENT=Production</c> da ishlaydi — Development/Testing (ApiFixture) ga ta'sir qilmaydi.
/// Demo stend kerak bo'lsa — boshqa muhit nomi (masalan <c>Staging</c>) ishlatiladi.
/// Tekshiruv DI qurilishidan OLDIN, migratsiya va seed'dan oldin bajariladi: noto'g'ri sozlamada bazaga
/// hech narsa yozilmaydi (masalan zaif parolli admin yaratilmaydi).</summary>
public static class ProductionConfigValidator
{
    public const int MinSigningKeyLength = 32;
    public const int MinAdminPasswordLength = 12;

    /// <summary>Repo'dagi (appsettings.Development.json, Worker) ochiq kalit — hech qachon production'da emas.</summary>
    private static readonly string[] KnownDevSigningKeys = ["faqat-lokal-muhit-uchun-kalit-kamida-32-belgi!"];

    /// <summary>Repo/README/compose'da ochiq yozilgan parollar.</summary>
    private static readonly string[] KnownWeakPasswords =
    [
        SeedOptions.DevelopmentAdminPassword, DemoDataSeeder.TutorPassword, DemoDataSeeder.StudentPassword,
        "password", "password123", "123456789012", "admin", "administrator", "amaliyotchi"
    ];

    private const string Placeholder = "CHANGE_ME";

    private static readonly string[] KnownWeakDbPasswords = ["amaliyotchi", "postgres", "password"];

    private static bool IsWeakDbPassword(string connectionString)
    {
        try
        {
            var password = new NpgsqlConnectionStringBuilder(connectionString).Password;
            return string.IsNullOrEmpty(password) || KnownWeakDbPasswords.Contains(password, StringComparer.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // Format xatosini Npgsql o'zi ulanishda aniq aytadi.
        }
    }

    /// <summary>Production bo'lsa tekshiradi; xato(lar) bo'lsa barchasini bitta xabarda chiqarib ishga tushishni to'xtatadi.</summary>
    public static void ThrowIfInvalid(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var errors = Validate(configuration);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production sozlamalari xavfsiz emas — ilova ishga tushirilmadi:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => " - " + e)));
        }
    }

    /// <summary>Ishga tushishni to'xtatmaydigan, lekin log'ga yoziladigan ogohlantirishlar.</summary>
    public static IReadOnlyList<string> Warnings(IConfiguration configuration)
    {
        var warnings = new List<string>();

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(h => h.Trim() == "*"))
            warnings.Add("AllowedHosts='*' — Host sarlavhasi cheklanmagan. Ommaviy domenlarni bering (AllowedHosts=amalyotchi.uz;app.amalyotchi.uz;api;localhost).");

        return warnings;
    }

    /// <summary>Muhitdan qat'i nazar qoidalarni tekshiradi (unit testlar uchun ochiq).</summary>
    public static IReadOnlyList<string> Validate(IConfiguration configuration)
    {
        var errors = new List<string>();

        // --- Baza ---
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
            errors.Add("ConnectionStrings:Postgres berilmagan.");
        else if (connectionString.Contains(Placeholder, StringComparison.OrdinalIgnoreCase))
            errors.Add($"ConnectionStrings:Postgres ichida '{Placeholder}' qolgan — haqiqiy parolni bering (ConnectionStrings__Postgres).");
        else if (IsWeakDbPassword(connectionString))
            errors.Add("ConnectionStrings:Postgres paroli bo'sh yoki default (amaliyotchi/postgres) — POSTGRES_PASSWORD ga kuchli parol bering.");

        // --- JWT ---
        var signingKey = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < MinSigningKeyLength)
            errors.Add($"Jwt:SigningKey kamida {MinSigningKeyLength} belgi bo'lishi shart (openssl rand -base64 48).");
        else if (KnownDevSigningKeys.Contains(signingKey, StringComparer.Ordinal)
                 || signingKey.Contains(Placeholder, StringComparison.OrdinalIgnoreCase))
            errors.Add("Jwt:SigningKey — repo'dagi dev kalit yoki namuna qiymat. Yangi tasodifiy kalit yarating (openssl rand -base64 48).");

        // --- Seed ---
        if (configuration.GetValue<bool>($"{SeedOptions.SectionName}:{nameof(SeedOptions.Demo)}"))
        {
            errors.Add("Seed:Demo=true Production'da taqiqlangan: demo hisoblar ochiq parollar bilan yaratiladi " +
                       $"({DemoDataSeeder.TutorPassword}, {DemoDataSeeder.StudentPassword}). Seed__Demo=false qiling " +
                       "(demo stend uchun ASPNETCORE_ENVIRONMENT=Staging).");
        }

        var adminPassword = configuration[$"{SeedOptions.SectionName}:{nameof(SeedOptions.AdminPassword)}"];
        if (!string.IsNullOrEmpty(adminPassword))
        {
            if (adminPassword.Length < MinAdminPasswordLength)
                errors.Add($"Seed:AdminPassword kamida {MinAdminPasswordLength} belgi bo'lishi shart.");
            else if (KnownWeakPasswords.Contains(adminPassword, StringComparer.OrdinalIgnoreCase)
                     || adminPassword.Contains(Placeholder, StringComparison.OrdinalIgnoreCase))
                errors.Add("Seed:AdminPassword — ma'lum (default/namuna) parol. Kuchli parol bering yoki admin yaratilgandan keyin o'chirib qo'ying.");
        }

        // --- Telegram ---
        var botToken = configuration[$"{TelegramOptions.SectionName}:{nameof(TelegramOptions.BotToken)}"];
        if (!string.IsNullOrEmpty(botToken) && botToken.Contains("DEV-TEST-TOKEN", StringComparison.OrdinalIgnoreCase))
            errors.Add("Telegram:BotToken — dev/test token. BotFather bergan haqiqiy tokenni bering.");

        if (configuration.GetValue<bool>($"{TelegramOptions.SectionName}:{nameof(TelegramOptions.BotEnabled)}"))
        {
            if (string.IsNullOrWhiteSpace(botToken))
                errors.Add("Telegram:BotEnabled=true, lekin Telegram:BotToken berilmagan (talaba Mini App kirishi ham ishlamaydi).");

            var webAppUrl = configuration[$"{TelegramOptions.SectionName}:{nameof(TelegramOptions.WebAppUrl)}"];
            if (!Uri.TryCreate(webAppUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                errors.Add("Telegram:BotEnabled=true, lekin Telegram:WebAppUrl HTTPS absolyut manzil emas.");
        }

        var maxAge = configuration.GetValue<int?>($"{TelegramOptions.SectionName}:{nameof(TelegramOptions.MaxAgeSeconds)}");
        if (maxAge is <= 0 or > 7 * 24 * 60 * 60)
            errors.Add("Telegram:MaxAgeSeconds 1..604800 (7 kun) oralig'ida bo'lishi kerak.");

        return errors;
    }
}
