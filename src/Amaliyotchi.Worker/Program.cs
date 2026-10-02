using Amaliyotchi.Infrastructure;
using Amaliyotchi.Infrastructure.Configuration;
using Serilog;

// M11 (Sprint 7) da bu yerga Hangfire jadvali qo'shiladi:
// 08:45 eslatma · 10:30 "kelmadi" · 18:00 avtomatik yopish · 21:00 kun yakuni.
var builder = Host.CreateApplicationBuilder(args);

// API bilan bir xil: Production'da default/dev sirlar bilan ishga tushmaydi (dev kalit faqat appsettings.Development.json da).
ProductionConfigValidator.ThrowIfInvalid(builder.Configuration, builder.Environment);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSerilog((services, configuration) =>
    configuration.ReadFrom.Configuration(builder.Configuration));

var host = builder.Build();
await host.RunAsync();
