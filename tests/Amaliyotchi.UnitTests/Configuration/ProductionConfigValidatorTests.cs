using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Amaliyotchi.UnitTests.Configuration;

public sealed class ProductionConfigValidatorTests
{
    private static readonly Dictionary<string, string?> ValidProduction = new()
    {
        ["ConnectionStrings:Postgres"] = "Host=postgres;Database=amaliyotchi;Username=amaliyotchi;Password=Kuchli-Parol-2026!",
        ["Jwt:SigningKey"] = "q8Vh3Zt0mL1xN5bR7cK2wP9sE4yA6uJdFgHiOoTrQe+Z1/k=",
        ["Seed:Enabled"] = "true",
        ["Seed:Demo"] = "false",
        ["Seed:AdminPassword"] = "Juda-Kuchli-Admin-2026",
        ["Telegram:BotEnabled"] = "true",
        ["Telegram:BotToken"] = "7000000000:AAH-real-looking-token",
        ["Telegram:WebAppUrl"] = "https://app.amalyotchi.uz",
        ["Telegram:MaxAgeSeconds"] = "86400",
    };

    private static IConfiguration Config(Action<Dictionary<string, string?>>? mutate = null)
    {
        var values = new Dictionary<string, string?>(ValidProduction);
        mutate?.Invoke(values);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void ToGriSozlama_XatoYoq()
        => ProductionConfigValidator.Validate(Config()).Should().BeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("qisqa-kalit")]
    [InlineData("faqat-lokal-muhit-uchun-kalit-kamida-32-belgi!")]
    [InlineData("CHANGE_ME-CHANGE_ME-CHANGE_ME-CHANGE_ME")]
    public void JwtKalit_ZaifYokiDev_RadEtiladi(string key)
        => ProductionConfigValidator.Validate(Config(v => v["Jwt:SigningKey"] = key))
            .Should().ContainSingle(e => e.StartsWith("Jwt:SigningKey"));

    [Fact]
    public void ConnectionString_ChangeMe_RadEtiladi()
        => ProductionConfigValidator.Validate(Config(v => v["ConnectionStrings:Postgres"] = "Host=x;Password=CHANGE_ME"))
            .Should().ContainSingle(e => e.StartsWith("ConnectionStrings:Postgres"));

    [Theory]
    [InlineData("Host=postgres;Database=amaliyotchi;Username=amaliyotchi;Password=amaliyotchi")]
    [InlineData("Host=postgres;Database=amaliyotchi;Username=amaliyotchi")]
    public void ConnectionString_DefaultYokiBoshParol_RadEtiladi(string connectionString)
        => ProductionConfigValidator.Validate(Config(v => v["ConnectionStrings:Postgres"] = connectionString))
            .Should().ContainSingle(e => e.StartsWith("ConnectionStrings:Postgres"));

    [Theory]
    [InlineData("admin12345")]
    [InlineData("qisqa")]
    [InlineData("ADMIN12345")]
    [InlineData("tutor12345")]
    public void AdminParol_DefaultYokiQisqa_RadEtiladi(string password)
        => ProductionConfigValidator.Validate(Config(v => v["Seed:AdminPassword"] = password))
            .Should().ContainSingle(e => e.StartsWith("Seed:AdminPassword"));

    [Fact]
    public void AdminParol_Berilmagan_Ruxsat()
        => ProductionConfigValidator.Validate(Config(v => v.Remove("Seed:AdminPassword"))).Should().BeEmpty();

    [Fact]
    public void DemoSeed_Production_RadEtiladi()
        => ProductionConfigValidator.Validate(Config(v => v["Seed:Demo"] = "true"))
            .Should().ContainSingle(e => e.StartsWith("Seed:Demo"));

    [Fact]
    public void BotYoqilgan_TokenVaHttpsUrlSiz_RadEtiladi()
        => ProductionConfigValidator.Validate(Config(v =>
            {
                v["Telegram:BotToken"] = "";
                v["Telegram:WebAppUrl"] = "http://app.amalyotchi.uz";
            }))
            .Should().HaveCount(2).And.OnlyContain(e => e.StartsWith("Telegram:"));

    [Fact]
    public void BotOchiq_TokenSiz_Ruxsat()
        => ProductionConfigValidator.Validate(Config(v =>
            {
                v["Telegram:BotEnabled"] = "false";
                v["Telegram:BotToken"] = "";
                v["Telegram:WebAppUrl"] = "";
            }))
            .Should().BeEmpty();

    [Fact]
    public void DevBotToken_RadEtiladi()
        => ProductionConfigValidator.Validate(Config(v => v["Telegram:BotToken"] = "1234567890:DEV-TEST-TOKEN-amaliyotchi"))
            .Should().ContainSingle(e => e.StartsWith("Telegram:BotToken"));

    [Fact]
    public void ThrowIfInvalid_Production_BarchaXatolarBittaXabarda()
    {
        var config = Config(v =>
        {
            v["Jwt:SigningKey"] = "";
            v["Seed:AdminPassword"] = "admin12345";
        });

        var act = () => ProductionConfigValidator.ThrowIfInvalid(config, new Env(Environments.Production));

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Jwt:SigningKey").And.Contain("Seed:AdminPassword");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public void ThrowIfInvalid_BoshqaMuhit_TekshirilmaydI(string environment)
    {
        var config = Config(v =>
        {
            v["Jwt:SigningKey"] = "";
            v["Seed:Demo"] = "true";
        });

        var act = () => ProductionConfigValidator.ThrowIfInvalid(config, new Env(environment));

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("*", true)]
    [InlineData(null, true)]
    [InlineData("amalyotchi.uz;app.amalyotchi.uz;api;localhost", false)]
    public void AllowedHosts_Yulduzcha_Ogohlantirish_XatoEmas(string? allowedHosts, bool warns)
    {
        var config = Config(v => v["AllowedHosts"] = allowedHosts);

        ProductionConfigValidator.Validate(config).Should().BeEmpty();
        ProductionConfigValidator.Warnings(config).Any(w => w.StartsWith("AllowedHosts")).Should().Be(warns);
    }

    [Fact]
    public void RefreshTokenHash_Deterministik_VaXomTokendanFarqli()
    {
        var hash = RefreshTokenHash.Of("raw-token");

        hash.Should().Be(RefreshTokenHash.Of("raw-token")).And.NotBe("raw-token").And.HaveLength(44);
        RefreshTokenHash.OfOptional("  ").Should().BeNull();
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Amaliyotchi.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
