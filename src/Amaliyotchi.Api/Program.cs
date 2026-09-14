using Amaliyotchi.Api.Infrastructure;
using Amaliyotchi.Application;
using Amaliyotchi.Infrastructure;
using Amaliyotchi.Infrastructure.Persistence;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddAppRateLimiting(builder.Configuration);

// Reverse proxy (nginx/Caddy) ortida turganda haqiqiy klient IP va sxemasini X-Forwarded-* dan olamiz —
// rate limiter va loglar shunga tayanadi. Xavfsizlik: X-Forwarded-For ni klient o'zi ham yubora oladi,
// shuning uchun sarlavhalar faqat ishonchli proksilardan (KnownProxies) qabul qilinadi. Ro'yxat bo'sh
// bo'lsa ASP.NET Core standarti amal qiladi — faqat loopback (127.0.0.1/::1) dan kelgan sarlavhalar
// ishonchli, boshqa manbalarniki e'tiborsiz qoldiriladi (spoofing imkonsiz, lekin proksi boshqa
// hostda bo'lsa uning IP sini appsettings "ForwardedHeaders:KnownProxies" ga yozish shart).
// Docker/Kubernetes ichida proksi IP si o'zgaruvchan — bunda butun ichki tarmoqni CIDR ko'rinishida
// "ForwardedHeaders:KnownNetworks" ga yoziladi (masalan "172.30.0.0/16" — compose tarmog'i).
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

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

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
if (DatabaseInitializer.IsEnabled(app.Services))
    await DatabaseInitializer.MigrateAndSeedAsync(app.Services, DatabaseInitializer.IsDemoEnabled(app.Services));

app.Run();

/// <summary>Integratsiya testlari uchun ochiq: WebApplicationFactory shu tipga tayanadi.</summary>
public partial class Program;
