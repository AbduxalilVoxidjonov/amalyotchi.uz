using System.Globalization;
using Amaliyotchi.Infrastructure;
using Amaliyotchi.Infrastructure.Configuration;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Serilog;

namespace Amaliyotchi.Api.Infrastructure;

/// <summary>
/// CLI rejimi: <c>dotnet Amaliyotchi.Api.dll purge-demo [--apply]</c> — demo seed ma'lumotini o'chirish
/// (<see cref="DemoDataPurger"/>). Konfiguratsiya va DI oddiy API bilan bir xil manbalardan (appsettings*.json, muhit
/// o'zgaruvchilari, <c>ASPNETCORE_ENVIRONMENT</c>; ConnectionStrings, Storage:RootPath), lekin host ishga TUSHIRILMAYDI:
/// Kestrel, Telegram bot va boshqa BackgroundService'lar start bo'lmaydi, migratsiya/seed qilinmaydi.
/// <see cref="ProductionConfigValidator"/> ham xuddi API'dagidek ishlaydi — production sirlari noto'g'ri muhitda purge qilinmaydi.
/// <para>Chiqish kodlari: 0 — muvaffaqiyat yoki demo topilmadi; 1 — xato; 2 — to'siqlar bor (hech narsa o'chirilmadi);
/// 64 — noto'g'ri argument.</para>
/// </summary>
public static class PurgeDemoCommand
{
    public const string Name = "purge-demo";

    public const int ExitOk = 0;
    public const int ExitError = 1;
    public const int ExitBlocked = 2;
    public const int ExitUsage = 64;

    public static bool IsInvoked(string[] args) => args.Length > 0 && args[0] == Name;

    /// <param name="args">To'liq <c>Main</c> argumentlari (<c>purge-demo</c> birinchi).</param>
    /// <param name="output">Hisobot (default — stdout).</param>
    /// <param name="error">Xatolar (default — stderr).</param>
    /// <param name="configure">Testlar uchun: builder'ga qo'shimcha sozlama (ProductionConfigValidator'dan OLDIN qo'llanadi).</param>
    /// <param name="environmentName">Testlar uchun muhit nomi; null — odatdagidek <c>ASPNETCORE_ENVIRONMENT</c>.</param>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        Action<WebApplicationBuilder>? configure = null,
        string? environmentName = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;

        var apply = false;
        foreach (var arg in args.Skip(1))
        {
            switch (arg)
            {
                case "--apply":
                    apply = true;
                    break;
                case "-h" or "--help":
                    await output.WriteLineAsync(Usage);
                    return ExitOk;
                default:
                    await error.WriteLineAsync($"purge-demo: noma'lum argument '{arg}'.{Environment.NewLine}{Usage}");
                    return ExitUsage;
            }
        }

        try
        {
            // Args bo'sh: "--apply" konfiguratsiya kaliti sifatida o'qilmasin. Qolgan manbalar — WebApplication.CreateBuilder
            // standarti (API bilan bir xil).
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = environmentName });
            configure?.Invoke(builder);

            ProductionConfigValidator.ThrowIfInvalid(builder.Configuration, builder.Environment);

            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));
            builder.Services.AddInfrastructure(builder.Configuration);

            // Build — faqat DI konteyner; StartAsync/Run chaqirilmaydi, shuning uchun IHostedService'lar ishlamaydi.
            await using var app = builder.Build();
            app.Services.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();

            using var scope = app.Services.CreateScope();
            var purger = scope.ServiceProvider.GetRequiredService<DemoDataPurger>();
            var report = apply ? await purger.PurgeAsync() : await purger.AnalyzeAsync();

            return await WriteReportAsync(report, apply, output, error);
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"purge-demo: XATO — {ex.Message}");
            await error.WriteLineAsync("Hech narsa o'chirilmadi (tranzaksiya qaytarildi yoki boshlanmadi).");
            return ExitError;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public const string Usage =
        "Foydalanish: dotnet Amaliyotchi.Api.dll purge-demo [--apply]\n" +
        "  (argumentsiz)  dry-run — nima o'chishini va to'siqlarni ko'rsatadi, hech narsa o'zgarmaydi\n" +
        "  --apply        demo ma'lumotni bitta tranzaksiyada o'chiradi (to'siq bo'lsa — hech narsa, exit 2)\n" +
        "Chiqish kodlari: 0 ok/topilmadi, 1 xato, 2 to'siqlar, 64 noto'g'ri argument.";

    private static async Task<int> WriteReportAsync(DemoPurgeReport report, bool apply, TextWriter output, TextWriter error)
    {
        var mode = apply ? "APPLY" : "DRY-RUN (hech narsa o'zgartirilmaydi)";
        await output.WriteLineAsync($"purge-demo: {mode}");

        if (!report.Found)
        {
            await output.WriteLineAsync("Demo ma'lumot topilmadi — o'chiriladigan narsa yo'q.");
            return ExitOk;
        }

        await output.WriteLineAsync($"Demo ma'lumot langari: {report.Anchor ?? "-"}");
        await output.WriteLineAsync();
        await output.WriteLineAsync($"  {"Jadval",-26}{(report.Applied ? "O'chirildi" : "O'chiriladi"),12}");
        foreach (var row in report.Counts)
            await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"  {row.Table,-26}{row.Count,12}"));
        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"  {"JAMI",-26}{report.TotalRows,12}"));
        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"  Diskdagi fayllar (stored_files): {report.Count("stored_files")} ta — commit'dan keyin o'chiriladi"));
        await output.WriteLineAsync();

        if (report.Blockers.Count > 0)
        {
            await output.WriteLineAsync($"TO'SIQLAR ({report.Blockers.Count}) — demo ma'lumotga demo bo'lmagan ma'lumot bog'langan, " +
                                        "hech narsa o'chirilmaydi:");
            foreach (var blocker in report.Blockers)
                await output.WriteLineAsync($"  - {blocker}");
            await output.WriteLineAsync("Bog'lanishlarni admin panel orqali bartaraf qiling (yoki ma'lumotni qo'lda ko'chiring) va qayta ishga tushiring.");
            return ExitBlocked;
        }

        if (!report.Applied)
        {
            await output.WriteLineAsync("To'siqlar yo'q. O'chirish uchun: dotnet Amaliyotchi.Api.dll purge-demo --apply");
            return ExitOk;
        }

        await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
            $"Tayyor: {report.TotalRows} qator bitta tranzaksiyada o'chirildi; diskdan {report.FilesDeleted} ta fayl o'chirildi."));
        if (report.FileErrors.Count > 0)
        {
            await error.WriteLineAsync($"OGOHLANTIRISH: {report.FileErrors.Count} ta fayl diskdan o'chmadi (bazada yozuvi yo'q — yetim fayl):");
            foreach (var fileError in report.FileErrors)
                await error.WriteLineAsync($"  - {fileError}");
        }

        return ExitOk;
    }
}
