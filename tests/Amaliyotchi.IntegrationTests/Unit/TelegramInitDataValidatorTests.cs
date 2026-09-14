using Amaliyotchi.Infrastructure.Identity;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.IntegrationTests.Unit;

/// <summary>Validator'ning sof testlari (baza/konteyner kerak emas). Ma'lum bot token + qo'lda hisoblangan hash.</summary>
public sealed class TelegramInitDataValidatorTests
{
    private const string BotToken = "7000000000:AAF-known-test-token";
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    private static TelegramInitDataValidator Create(string botToken = BotToken, int maxAgeSeconds = 86400) =>
        new(Options.Create(new TelegramOptions { BotToken = botToken, MaxAgeSeconds = maxAgeSeconds }));

    [Fact]
    public void QoldaHisoblanganHash_TasdiqlanadiVaUserOqiladi()
    {
        // data_check_string (kalitlar alifbo tartibida):
        //   auth_date=1757844000\nquery_id=AAHdF6IQAAAAAN0XohDhrOrc\nuser={"id":123456789,"first_name":"Ali","username":"ali_student","language_code":"uz"}
        // secret = HMAC_SHA256("WebAppData", BotToken); hash = hex(HMAC_SHA256(secret, dcs)) — TelegramInitDataFactory mustaqil hisoblaydi.
        var authDate = DateTimeOffset.FromUnixTimeSeconds(1757844000);
        var initData = TelegramInitDataFactory.Create(123456789, BotToken, authDate);

        var ok = Create().TryValidate(initData, authDate.AddMinutes(10), out var user, out var error);

        ok.Should().BeTrue(error);
        user.Should().NotBeNull();
        user!.Id.Should().Be(123456789);
        user.FirstName.Should().Be("Ali");
        user.Username.Should().Be("ali_student");
        user.AuthDate.Should().Be(authDate);
    }

    [Fact]
    public void RasmiyMisol_MaydonTartibiOrdinal_Tasdiqlanadi()
    {
        // Tartibsiz berilgan maydonlar ham to'g'ri tartiblanadi; '+' va % belgilari URL-decode qilinadi.
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["user"] = """{"id":42,"first_name":"Olim O'g'li","language_code":"uz"}""",
            ["auth_date"] = Now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["chat_type"] = "private",
            ["start_param"] = "ref 1+1"
        };
        var hash = TelegramInitDataFactory.Sign(fields, BotToken);
        var initData = $"user={Uri.EscapeDataString(fields["user"])}&start_param={Uri.EscapeDataString(fields["start_param"])}"
                       + $"&hash={hash}&chat_type=private&auth_date={fields["auth_date"]}";

        var ok = Create().TryValidate(initData, Now, out var user, out var error);

        ok.Should().BeTrue(error);
        user!.Id.Should().Be(42);
        user.FirstName.Should().Be("Olim O'g'li");
    }

    [Fact]
    public void NotogriHash_RadEtiladi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now, hashOverride: new string('a', 64));

        var ok = Create().TryValidate(initData, Now, out var user, out var error);

        ok.Should().BeFalse();
        user.Should().BeNull();
        error.Should().Contain("imzo");
    }

    [Fact]
    public void BoshqaBotTokeni_RadEtiladi()
    {
        var initData = TelegramInitDataFactory.Create(1, "8000000000:boshqa", Now);

        Create().TryValidate(initData, Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("imzo");
    }

    [Fact]
    public void OzgartirilganMaydon_RadEtiladi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now).Replace("%22id%22%3A1%2C", "%22id%22%3A2%2C", StringComparison.Ordinal);

        Create().TryValidate(initData, Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("imzo");
    }

    [Fact]
    public void AuthDate24SoatdanEski_RadEtiladi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now.AddHours(-24).AddSeconds(-1));

        Create().TryValidate(initData, Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("eskirgan");
    }

    [Fact]
    public void AuthDateChegaraIchida_Tasdiqlanadi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now.AddHours(-23));

        Create().TryValidate(initData, Now, out _, out var error).Should().BeTrue(error);
    }

    [Fact]
    public void MaxAgeSozlanadi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now.AddMinutes(-10));

        Create(maxAgeSeconds: 300).TryValidate(initData, Now, out _, out _).Should().BeFalse();
        Create(maxAgeSeconds: 900).TryValidate(initData, Now, out _, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("user=%7B%22id%22%3A1%7D&auth_date=1")]
    [InlineData("hash=abc")]
    [InlineData("auth_date=1&hash=zz")]
    public void NotoliqInitData_RadEtiladi(string initData)
    {
        Create().TryValidate(initData, Now, out var user, out _).Should().BeFalse();
        user.Should().BeNull();
    }

    [Fact]
    public void BotTokenSozlanmagan_RadEtiladi()
    {
        var initData = TelegramInitDataFactory.Create(1, BotToken, Now);

        Create(botToken: "").TryValidate(initData, Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("BotToken");
    }
}
