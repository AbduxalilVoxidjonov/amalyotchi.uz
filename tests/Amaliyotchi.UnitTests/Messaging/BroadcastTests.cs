using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Messages;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Messaging;
using Amaliyotchi.Infrastructure.Identity;
using Amaliyotchi.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot.Exceptions;
using ResponseParameters = Telegram.Bot.Types.ResponseParameters;
using Xunit;

namespace Amaliyotchi.UnitTests.Messaging;

public sealed class AudienceLabelTests
{
    [Fact]
    public void All_VaBoshFiltr_BarchaUlanganlar()
    {
        AudienceLabel.Build(BroadcastAudienceKind.All).Should().Be("Barcha ulanganlar");
        AudienceLabel.Build(BroadcastAudienceKind.Filter).Should().Be("Barcha ulanganlar");
    }

    [Fact]
    public void Selected_Soni()
        => AudienceLabel.Build(BroadcastAudienceKind.Selected, selectedCount: 3).Should().Be("Tanlangan: 3 ta talaba");

    [Fact]
    public void Filter_QismlarTartibBilan()
    {
        AudienceLabel.Build(BroadcastAudienceKind.Filter, facultyName: "AT fakulteti", course: 3, groupName: "412-22")
            .Should().Be("AT fakulteti · 3-kurs · 412-22");
        AudienceLabel.Build(BroadcastAudienceKind.Filter, facultyName: " AT ", directionName: "Dasturiy injiniring", q: " Ali ")
            .Should().Be("AT · Dasturiy injiniring · «Ali»");
    }
}

public sealed class BroadcastStatusRuleTests
{
    [Theory]
    [InlineData(3, 3, BroadcastMessageStatus.Queued)]
    [InlineData(3, 1, BroadcastMessageStatus.Sending)]
    [InlineData(3, 0, BroadcastMessageStatus.Completed)]
    [InlineData(0, 0, BroadcastMessageStatus.Completed)]
    public void Holat(int total, int pending, BroadcastMessageStatus expected)
        => BroadcastStatusRule.For(total, pending).Should().Be(expected);
}

public sealed class BroadcastDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(10, 600)]
    [InlineData(100, 600)]
    public void Backoff_EksponensialVaCheklangan(int attempts, int seconds)
        => BroadcastDelivery.BackoffFor(attempts).Should().Be(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Transient_BeshUrinishdanKeyinFailed()
    {
        var delivery = BroadcastDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), 42, Now);

        for (var i = 1; i < BroadcastDelivery.MaxAttempts; i++)
        {
            delivery.RegisterTransientFailure("Tarmoq xatosi", Now);
            delivery.Status.Should().Be(BroadcastDeliveryStatus.Pending);
            delivery.Attempts.Should().Be(i);
            delivery.NextAttemptAt.Should().Be(Now + BroadcastDelivery.BackoffFor(i));
        }

        delivery.RegisterTransientFailure("Tarmoq xatosi", Now);
        delivery.Status.Should().Be(BroadcastDeliveryStatus.Failed);
        delivery.Attempts.Should().Be(BroadcastDelivery.MaxAttempts);
    }

    [Fact]
    public void Requeue_FaqatFailed_BlockedTegilmaydi()
    {
        var failed = BroadcastDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        failed.MarkFailed("x");
        failed.Requeue(Now.AddMinutes(1)).Should().BeTrue();
        (failed.Status, failed.Attempts, failed.Error, failed.NextAttemptAt)
            .Should().Be((BroadcastDeliveryStatus.Pending, 0, (string?)null, Now.AddMinutes(1)));

        var blocked = BroadcastDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), 2, Now);
        blocked.MarkBlocked("bloklagan");
        blocked.Requeue(Now).Should().BeFalse();
        blocked.Status.Should().Be(BroadcastDeliveryStatus.Blocked);
    }

    [Fact]
    public void Defer_UrinishniOshirmaydi_XatoQisqartiriladi()
    {
        var delivery = BroadcastDelivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        delivery.Defer(Now.AddSeconds(30));
        (delivery.Attempts, delivery.NextAttemptAt).Should().Be((0, Now.AddSeconds(30)));

        delivery.MarkFailed(new string('x', 900));
        delivery.Error.Should().HaveLength(BroadcastDelivery.ErrorMaxLength);
    }

    [Fact]
    public void Message_MatnTrimVaChegara()
    {
        BroadcastMessage.Create("  Salom  ", false, BroadcastAudienceKind.All, "Barcha ulanganlar", "{}").Text.Should().Be("Salom");
        var empty = () => BroadcastMessage.Create("   ", false, BroadcastAudienceKind.All, "x", "{}");
        empty.Should().Throw<DomainException>();
        var tooLong = () => BroadcastMessage.Create(new string('a', 4001), false, BroadcastAudienceKind.All, "x", "{}");
        tooLong.Should().Throw<DomainException>();
    }
}

public sealed class UserTelegramBlockTests
{
    [Fact]
    public void LinkTelegram_LinkedAtBirinchiVaqt_BlockBelgisi()
    {
        var at = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var student = User.CreateStudent("Ali Valiyev", Guid.NewGuid());
        student.LinkTelegram(111, linkedAt: at);
        student.LinkTelegram(111, linkedAt: at.AddDays(1));
        student.TelegramLinkedAt.Should().Be(at);

        student.MarkBotBlocked(at);
        student.MarkBotBlocked(at.AddHours(1));
        student.TelegramBotBlockedAt.Should().Be(at, "birinchi blok vaqti saqlanadi");
        student.ClearBotBlocked();
        student.TelegramBotBlockedAt.Should().BeNull();
    }
}

public sealed class TelegramMessengerClassifyTests
{
    [Theory]
    [InlineData(403, "Forbidden: bot was blocked by the user", TelegramSendOutcome.Blocked)]
    [InlineData(403, "Forbidden: user is deactivated", TelegramSendOutcome.Blocked)]
    [InlineData(403, "Forbidden: bot can't initiate conversation with a user", TelegramSendOutcome.Blocked)]
    [InlineData(400, "Bad Request: chat not found", TelegramSendOutcome.Blocked)]
    [InlineData(400, "Bad Request: message is too long", TelegramSendOutcome.PermanentError)]
    [InlineData(401, "Unauthorized", TelegramSendOutcome.PermanentError)]
    [InlineData(502, "Bad Gateway", TelegramSendOutcome.TransientError)]
    public void KodBoyicha(int code, string message, TelegramSendOutcome expected)
    {
        var result = TelegramMessenger.Classify(new ApiRequestException(message, code));
        result.Outcome.Should().Be(expected);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Blocked_OzbekchaXabar()
        => TelegramMessenger.Classify(new ApiRequestException("Forbidden: bot was blocked by the user", 403))
            .Error.Should().Be("Talaba botni bloklagan yoki botni ishga tushirmagan");

    [Fact]
    public void TooManyRequests_RetryAfter()
    {
        var result = TelegramMessenger.Classify(new ApiRequestException(
            "Too Many Requests: retry after 17", 429, new ResponseParameters { RetryAfter = 17 }));
        result.Outcome.Should().Be(TelegramSendOutcome.RateLimited);
        result.RetryAfter.Should().Be(TimeSpan.FromSeconds(17));
    }

    [Fact]
    public async Task TokenBosh_NotConfigured_TarmoqqaChiqmaydi()
    {
        var messenger = new TelegramMessenger(Options.Create(new TelegramOptions()), NullLogger<TelegramMessenger>.Instance);
        var result = await messenger.SendTextAsync(1, "salom", withAppButton: true, CancellationToken.None);
        result.Outcome.Should().Be(TelegramSendOutcome.NotConfigured);
        result.Error.Should().Be("Bot tokeni sozlanmagan");
        messenger.Dispose();
    }

    [Theory]
    [InlineData("https://app.example.uz", true)]
    [InlineData("http://app.example.uz", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void WebAppUrl_FaqatHttps(string? url, bool expected)
        => TelegramMessenger.TryGetWebAppUrl(url, out _).Should().Be(expected);
}
