using Amaliyotchi.Api.Infrastructure;
using Amaliyotchi.Application;
using Amaliyotchi.Infrastructure;
using Amaliyotchi.Infrastructure.Configuration;
using Amaliyotchi.Infrastructure.Persistence;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Production'da default/dev sirlar (JWT kalit, admin paroli, CHANGE_ME, demo seed, bot sozlamasi) bilan
// ishga tushmaymiz — migratsiya va seed'dan OLDIN, aniq xabar bilan to'xtaydi. Boshqa muhitlarga ta'sir qilmaydi.
ProductionConfigValidator.ThrowIfInvalid(builder.Configuration, builder.Environment);

// Kestrel: "Server: Kestrel" sarlavhasi chiqmasin; JSON so'rovlar uchun global tana limiti 2 MB (standart 30 MB).
// Fayl yuklash endpoint'lari o'z [RequestSizeLimit] atributi bilan bu limitni oshiradi (6 MB, 30 MB, Excel 5 MB).
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = MaxJsonRequestBodyBytes;
});

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Telegram bot (long polling) — faqat Telegram:BotEnabled=true va token + WebAppUrl berilganda ishlaydi.
builder.Services.AddTelegramBot();
// Fon xizmati (bot) kutilmagan xato bilan yiqilsa butun API to'xtamasin — xato log'ga yoziladi, HTTP ishlashda davom etadi.
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
builder.Services.AddJwtAuth(builder.Configuration);

// DataProtection kalitlari: konteynerda default (~/.aspnet/DataProtection-Keys) image qatlamida qoladi va har
// qayta yaratishda yo'qoladi (startup WRN). DataProtection:KeysPath berilsa — persist papka (compose: api-data
// volume ichidagi /app/data/keys). Bo'sh bo'lsa ASP.NET standart xatti-harakati (lokal dev, testlar).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("amaliyotchi");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddAppRateLimiting(builder.Configuration);

// Reverse proxy ortida haqiqiy klient IP va sxemasini X-Forwarded-* dan olamiz — rate limiter (RateLimitPolicies)
// va audit log (CurrentUser.IpAddress) shunga tayanadi. Production zanjiri:
//   Brauzer → Cloudflare → cloudflared → nginx (web/*/nginx.conf) → API.
// nginx CF-Connecting-IP ni faqat cloudflared'dan (qat'iy IP) qabul qiladi (real_ip) va API'ga BITTA
// qiymatli `X-Forwarded-For: <klient IP>` + `X-Forwarded-Proto` yuboradi. Shuning uchun ForwardLimit=1:
// faqat oxirgi qiymat olinadi va faqat ishonchli manbadan (KnownProxies/KnownNetworks) kelganda.
// Konfiguratsiyadagi qiymatlar ASP.NET Core standartiga (loopback: 127.0.0.0/8, ::1) QO'SHILADI — ro'yxatlar
// bo'sh bo'lsa faqat loopback ishonchli (spoofing imkonsiz). Compose'da dashboard/twa nginx konteynerlari qat'iy
// IP oladi va faqat ular "ForwardedHeaders:KnownProxies" ga yoziladi (DASHBOARD_IP, TWA_IP) — tarmoqdagi
// boshqa konteyner (yoki host porti orqali kelgan so'rov) soxta X-Forwarded-For yubora olmaydi.
// KnownNetworks (CIDR) — test/maxsus holatlar uchun qoldirilgan.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
        options.KnownProxies.Add(IPAddress.Parse(proxy));

    var knownNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
    foreach (var network in knownNetworks)
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});

// Kontrakt: enum'lar string va camelCase ("submitted", "revisionNeeded"), property'lar camelCase.
// Ikkala JSON yo'li ham bir xil sozlanadi: MVC (controller javoblari/body) va
// HttpContext.Response.WriteAsJsonAsync (ProblemDetails, GlobalExceptionHandler).
builder.Services.AddControllers()
    .AddJsonOptions(options => JsonConventions.Apply(options.JsonSerializerOptions));
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    JsonConventions.Apply(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres");

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsProduction())
{
    foreach (var warning in ProductionConfigValidator.Warnings(app.Configuration))
        app.Logger.LogWarning("Production sozlamasi: {Warning}", warning);
}

app.UseForwardedHeaders();
// Tartib: request logging exception handler'dan TASHQARIDA — log'dagi status klient olgan javob bilan bir xil
// (403/404/409 ..., 500 emas), 4xx esa Warning darajada va stack trace'siz (RequestLogging.cs).
app.UseSerilogRequestLogging(RequestLogging.Configure);
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// Startup: migratsiya + seed (Seed:Enabled; Development'da default true). Testlar buni o'zi chaqiradi.
// ValidateOnStart'dagi options (Jwt, Telegram, Storage) migratsiyadan OLDIN tekshiriladi — xato sozlamada bazaga tegmaymiz.
app.Services.GetRequiredService<Microsoft.Extensions.Options.IStartupValidator>().Validate();

if (DatabaseInitializer.IsEnabled(app.Services))
{
    try
    {
        await DatabaseInitializer.MigrateAndSeedAsync(app.Services, DatabaseInitializer.IsDemoEnabled(app.Services));
    }
    catch (Exception ex)
    {
        // Migratsiya/seed xatosi — ilova yarim holatda ishlamasin: aniq log (Seq ham) va nol bo'lmagan exit kod.
        app.Logger.LogCritical(ex, "Startup: migratsiya yoki seed muvaffaqiyatsiz — ilova to'xtatildi");
        await Log.CloseAndFlushAsync();
        throw;
    }
}

app.Run();

/// <summary>Integratsiya testlari uchun ochiq: WebApplicationFactory shu tipga tayanadi.</summary>
public partial class Program
{
    /// <summary>Fayl yuklamaydigan (JSON) so'rovlar uchun Kestrel tana limiti.</summary>
    public const long MaxJsonRequestBodyBytes = 2 * 1024 * 1024;
}
