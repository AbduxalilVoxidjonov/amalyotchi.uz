using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Amaliyotchi.Infrastructure.Messaging;

/// <summary><see cref="ITelegramMessenger"/> — Telegram.Bot orqali. Faqat <c>Telegram:BotToken</c> ga bog'liq (polling'ga emas).
/// Klient bir marta yaratiladi; HttpClient'ni kutubxona o'zi yaratadi — IHttpClientFactory so'rov URL'ini (tokenli)
/// log'ga yozardi (<see cref="Bot.TelegramBotService"/> dagi kabi). Kutubxonaning avtomatik 429 qayta urinishi o'chiriladi
/// (<c>RetryThreshold = 0</c>) — kechiktirishni dispetcher DB orqali boshqaradi. Log'ga token ham, xabar matni ham yozilmaydi.</summary>
public sealed class TelegramMessenger(IOptions<TelegramOptions> options, ILogger<TelegramMessenger> logger)
    : ITelegramMessenger, IDisposable
{
    public const string AppButtonText = "Ilovani ochish";

    public const string BlockedError = "Talaba botni bloklagan yoki botni ishga tushirmagan";
    public const string NotConfiguredError = "Bot tokeni sozlanmagan";
    public const string InvalidTokenFormatError = "Bot tokeni formati noto'g'ri";
    public const string UnauthorizedError = "Bot tokeni yaroqsiz (401)";

    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _disposed = new();
    private TelegramBotClient? _client;
    private bool _invalidToken;

    public async Task<TelegramSendResult> SendTextAsync(
        long chatId, string text, bool withAppButton, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken))
            return new TelegramSendResult(TelegramSendOutcome.NotConfigured, Error: NotConfiguredError);

        var client = GetClient(settings.BotToken);
        if (client is null)
            return new TelegramSendResult(TelegramSendOutcome.NotConfigured, Error: InvalidTokenFormatError);

        var markup = withAppButton && TryGetWebAppUrl(settings.WebAppUrl, out var url)
            ? new InlineKeyboardMarkup(InlineKeyboardButton.WithWebApp(AppButtonText, new WebAppInfo { Url = url }))
            : null;

        try
        {
            // parse_mode yo'q — oddiy matn (HTML/Markdown injection va escaping xatolari bo'lmaydi).
            var message = await client.SendMessage(
                chatId,
                text,
                replyMarkup: markup,
                linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
                cancellationToken: cancellationToken);
            return TelegramSendResult.Sent(message.MessageId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiRequestException ex)
        {
            return Classify(ex);
        }
        catch (Exception ex)
        {
            // Tarmoq/timeout (RequestException, HttpRequestException, TaskCanceledException ...). Xabar matni (URL bo'lishi
            // mumkin) yozilmaydi — faqat tur nomi.
            logger.LogDebug("Telegram sendMessage tarmoq xatosi: {Type}", ex.GetType().Name);
            return new TelegramSendResult(TelegramSendOutcome.TransientError,
                Error: $"Tarmoq xatosi ({ex.GetType().Name})");
        }
    }

    /// <summary>Telegram javob kodi → natija. Ochiq qilingan — unit testlar uchun.</summary>
    public static TelegramSendResult Classify(ApiRequestException ex)
    {
        var description = Describe(ex.Message);
        switch (ex.ErrorCode)
        {
            case 403:
                return new TelegramSendResult(TelegramSendOutcome.Blocked, Error: BlockedError);
            case 400 when description.Contains("chat not found", StringComparison.OrdinalIgnoreCase)
                          || description.Contains("user not found", StringComparison.OrdinalIgnoreCase):
                return new TelegramSendResult(TelegramSendOutcome.Blocked, Error: BlockedError);
            case 429:
                var retryAfter = ex.Parameters?.RetryAfter is int seconds && seconds > 0 ? seconds : 1;
                return new TelegramSendResult(TelegramSendOutcome.RateLimited, RetryAfter: TimeSpan.FromSeconds(retryAfter),
                    Error: $"Telegram cheklovi (429): {retryAfter} s dan keyin qayta urinish");
            case 401:
                return new TelegramSendResult(TelegramSendOutcome.PermanentError, Error: UnauthorizedError);
            case >= 500:
                return new TelegramSendResult(TelegramSendOutcome.TransientError, Error: $"Telegram xatosi ({ex.ErrorCode})");
            default:
                return new TelegramSendResult(TelegramSendOutcome.PermanentError,
                    Error: $"Telegram so'rovni rad etdi ({ex.ErrorCode}): {description}");
        }
    }

    public static bool TryGetWebAppUrl(string? value, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
            return false;

        url = value;
        return true;
    }

    private TelegramBotClient? GetClient(string token)
    {
        lock (_gate)
        {
            if (_client is not null)
                return _client;
            if (_invalidToken)
                return null;

            try
            {
                _client = new TelegramBotClient(
                    new TelegramBotClientOptions(token) { RetryThreshold = 0, RetryCount = 0 },
                    cancellationToken: _disposed.Token);
                return _client;
            }
            catch (ArgumentException)
            {
                _invalidToken = true;
                logger.LogError("Telegram xabar yuborish: Telegram:BotToken formati noto'g'ri.");
                return null;
            }
        }
    }

    /// <summary>Telegram description'i (masalan "Bad Request: message is too long") — qisqa; token unda bo'lmaydi.</summary>
    private static string Describe(string message)
    {
        var value = message.Trim();
        return value.Length > 200 ? value[..200] : value;
    }

    public void Dispose()
    {
        _disposed.Cancel();
        _disposed.Dispose();
    }
}
