using Amaliyotchi.Infrastructure.Bot;
using Amaliyotchi.Infrastructure.Identity;
using FluentAssertions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;

namespace Amaliyotchi.UnitTests.Bot;

public sealed class BotReplyPlannerTests
{
    private const string WebAppUrl = "https://app.amalyotchi.uz/";
    private const long ChatId = 555_000_111;

    private static Update Private(string? text, string? firstName = "Ali") => new()
    {
        Id = 1,
        Message = new Message
        {
            Id = 10,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = ChatId, Type = ChatType.Private, FirstName = firstName },
            From = new User { Id = ChatId, IsBot = false, FirstName = firstName ?? "" },
            Text = text,
        },
    };

    private static Update InChat(ChatType type, string text) => new()
    {
        Id = 2,
        Message = new Message
        {
            Id = 11,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = -100123, Type = type, Title = "Guruh" },
            From = new User { Id = ChatId, IsBot = false, FirstName = "Ali" },
            Text = text,
        },
    };

    [Fact]
    public void Start_SalomVaIsmBilanJavob_TugmadaWebAppUrl()
    {
        var reply = BotReplyPlanner.Plan(Private("/start"), WebAppUrl);

        reply.Should().NotBeNull();
        reply!.ChatId.Should().Be(ChatId);
        reply.Text.Should().StartWith("Assalomu alaykum, Ali!")
            .And.Contain("HEMIS ID va parolni kiriting");
        reply.Button.Should().Be(new BotWebAppButton("Ilovani ochish", WebAppUrl));
    }

    [Theory]
    [InlineData("/start xyz")]
    [InlineData("/start   ref_123")]
    [InlineData("/START")]
    [InlineData("/start@emu_amalyotchi_bot")]
    [InlineData("  /start@emu_amalyotchi_bot payload")]
    public void Start_ParametrVaBotNomiBilan_HamSalom(string text)
    {
        var reply = BotReplyPlanner.Plan(Private(text), WebAppUrl);

        reply!.Text.Should().StartWith("Assalomu alaykum, Ali!");
        reply.Button!.Url.Should().Be(WebAppUrl);
    }

    [Fact]
    public void Start_IsmYoq_UmumiySalom()
    {
        var reply = BotReplyPlanner.Plan(Private("/start", firstName: null), WebAppUrl);

        reply!.Text.Should().StartWith("Assalomu alaykum! Amaliyotchi");
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/help@emu_amalyotchi_bot")]
    public void Help_YordamMatni(string text)
    {
        var reply = BotReplyPlanner.Plan(Private(text), WebAppUrl);

        reply!.Text.Should().Be(BotReplyPlanner.HelpText);
        reply.Text.Should().Contain("QR").And.Contain("selfi").And.Contain("Kundalik")
            .And.Contain("Ruxsat so'rovi").And.Contain("tyutor");
        reply.Button!.Url.Should().Be(WebAppUrl);
    }

    [Theory]
    [InlineData("salom")]
    [InlineData("/nomalum")]
    [InlineData("/")]
    [InlineData("startni bosdim")]
    public void BoshqaMatn_QisqaJavobVaTugma(string text)
    {
        var reply = BotReplyPlanner.Plan(Private(text), WebAppUrl);

        reply!.Text.Should().Be(BotReplyPlanner.FallbackText);
        reply.Button.Should().Be(new BotWebAppButton("Ilovani ochish", WebAppUrl));
    }

    [Theory]
    [InlineData(ChatType.Group)]
    [InlineData(ChatType.Supergroup)]
    [InlineData(ChatType.Channel)]
    public void GuruhVaKanal_EtiborsizQoldiriladi(ChatType type)
    {
        BotReplyPlanner.Plan(InChat(type, "/start"), WebAppUrl).Should().BeNull();
        BotReplyPlanner.Plan(InChat(type, "salom"), WebAppUrl).Should().BeNull();
    }

    [Fact]
    public void MatnsizXabarVaBoshqaUpdate_EtiborsizQoldiriladi()
    {
        BotReplyPlanner.Plan(Private(text: null), WebAppUrl).Should().BeNull();
        BotReplyPlanner.Plan(new Update { Id = 3 }, WebAppUrl).Should().BeNull();
    }

    [Theory]
    [InlineData(false, "123:abc", "https://app.amalyotchi.uz", false)]
    [InlineData(true, "", "https://app.amalyotchi.uz", false)]
    [InlineData(true, "123:abc", "", false)]
    [InlineData(true, "123:abc", "http://app.amalyotchi.uz", false)]
    [InlineData(true, "123:abc", "https://app.amalyotchi.uz", true)]
    public void IsConfigured_FaqatYoqilganVaToliqSozlanganda(bool enabled, string token, string url, bool expected)
    {
        var settings = new TelegramOptions { BotEnabled = enabled, BotToken = token, WebAppUrl = url };

        TelegramBotService.IsConfigured(settings, out var reason).Should().Be(expected);
        if (!expected)
            reason.Should().NotBeNullOrWhiteSpace();
    }
}
