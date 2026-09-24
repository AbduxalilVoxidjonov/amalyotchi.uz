using Amaliyotchi.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Amaliyotchi.Infrastructure.Bot;

/// <summary>Telegram bot — long polling (webhook emas: tunnel/nginx sozlamasiga bog'liq emas).
/// Ishga tushishda webhook o'chiriladi, buyruqlar ro'yxati va Menu Button (Mini App) o'rnatiladi,
/// so'ng <c>getUpdates</c> tsikli. Tarmoq xatolari va 409 Conflict (boshqa instansiya polling qilmoqda)
/// log'ga yoziladi va eksponensial backoff bilan qayta uriniladi — xizmat yiqilmaydi.
/// Javob mantig'i — <see cref="BotReplyPlanner"/>.</summary>
public sealed class TelegramBotService(
    IOptions<TelegramOptions> options,
    ILogger<TelegramBotService> logger) : BackgroundService
{
    private const int PollTimeoutSeconds = 30;
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConflictBackoff = TimeSpan.FromSeconds(10);

    public const string MenuButtonText = "Amalyotchi";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!IsConfigured(settings, out var reason))
        {
            logger.LogInformation("Telegram bot o'chiq: {Reason}", reason);
            return;
        }

        // Host startup'ini bloklamaslik uchun (ExecuteAsync'ning sinxron qismi StartAsync ichida bajariladi).
        await Task.Yield();

        TelegramBotClient bot;
        try
        {
            // HttpClient'ni kutubxona o'zi yaratadi: IHttpClientFactory so'rov URL'ini (tokenli) log'ga yozardi.
            bot = new TelegramBotClient(settings.BotToken, cancellationToken: stoppingToken);
        }
        catch (ArgumentException)
        {
            logger.LogError("Telegram bot ishga tushmadi: Telegram:BotToken formati noto'g'ri.");
            return;
        }

        if (!await ConfigureWithRetryAsync(bot, settings.WebAppUrl, stoppingToken))
            return;

        await PollAsync(bot, settings.WebAppUrl, stoppingToken);
    }

    /// <summary>Bot yoqilganmi va to'liq sozlanganmi (token, HTTPS WebAppUrl).</summary>
    public static bool IsConfigured(TelegramOptions settings, out string reason)
    {
        if (!settings.BotEnabled)
            reason = "Telegram:BotEnabled=false.";
        else if (string.IsNullOrWhiteSpace(settings.BotToken))
            reason = "Telegram:BotToken berilmagan.";
        else if (string.IsNullOrWhiteSpace(settings.WebAppUrl))
            reason = "Telegram:WebAppUrl berilmagan.";
        else if (!Uri.TryCreate(settings.WebAppUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            reason = "Telegram:WebAppUrl HTTPS absolyut manzil bo'lishi kerak.";
        else
        {
            reason = string.Empty;
            return true;
        }

        return false;
    }

    private async Task<bool> ConfigureWithRetryAsync(ITelegramBotClient bot, string webAppUrl, CancellationToken ct)
    {
        var backoff = MinBackoff;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Polling uchun webhook bo'lmasligi shart (aks holda getUpdates 409 qaytaradi).
                // Kutib turgan update'lar saqlanadi — bot o'chiq paytda yozilgan /start ham javob oladi.
                await bot.DeleteWebhook(dropPendingUpdates: false, cancellationToken: ct);
                await bot.SetMyCommands(
                [
                    new BotCommand { Command = "start", Description = "Boshlash va ilovani ochish" },
                    new BotCommand { Command = "help", Description = "Yordam" },
                ], cancellationToken: ct);
                // Qiymat avvalgisi bilan bir xil bo'lsa ham qayta o'rnatish zararsiz.
                await bot.SetChatMenuButton(
                    menuButton: new MenuButtonWebApp { Text = MenuButtonText, WebApp = new WebAppInfo { Url = webAppUrl } },
                    cancellationToken: ct);

                var me = await bot.GetMe(ct);
                logger.LogInformation("Telegram bot @{Username} long polling rejimida ishga tushdi.", me.Username);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 401)
            {
                logger.LogError("Telegram bot ishga tushmadi: token yaroqsiz (401 Unauthorized).");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Telegram bot sozlanmadi ({Error}); {Delay} dan keyin qayta urinish.",
                    Describe(ex), backoff);
                if (!await DelayAsync(backoff, ct))
                    return false;
                backoff = Next(backoff);
            }
        }

        return false;
    }

    private async Task PollAsync(ITelegramBotClient bot, string webAppUrl, CancellationToken ct)
    {
        int? offset = null;
        var backoff = MinBackoff;

        while (!ct.IsCancellationRequested)
        {
            Update[] updates;
            try
            {
                updates = await bot.GetUpdates(
                    offset, limit: 100, timeout: PollTimeoutSeconds,
                    allowedUpdates: [UpdateType.Message], cancellationToken: ct);
                backoff = MinBackoff;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ApiRequestException ex) when (ex.ErrorCode == 409)
            {
                var delay = backoff < ConflictBackoff ? ConflictBackoff : backoff;
                logger.LogWarning(
                    "Telegram getUpdates 409 Conflict: boshqa instansiya (yoki webhook) shu bot bilan ishlamoqda. " +
                    "Bir vaqtda faqat bitta API polling qilishi kerak. {Delay} dan keyin qayta urinish.", delay);
                if (!await DelayAsync(delay, ct))
                    break;
                backoff = Next(delay);
                continue;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Telegram getUpdates xatosi ({Error}); {Delay} dan keyin qayta urinish.",
                    Describe(ex), backoff);
                if (!await DelayAsync(backoff, ct))
                    break;
                backoff = Next(backoff);
                continue;
            }

            foreach (var update in updates)
            {
                // Offset avval suriladi: bitta "zaharli" update tsiklni to'xtatib qo'ymasin.
                offset = update.Id + 1;
                try
                {
                    await HandleAsync(bot, update, webAppUrl, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Telegram update {UpdateId} ga javob yuborilmadi ({Error}).",
                        update.Id, Describe(ex));
                }
            }
        }

        logger.LogInformation("Telegram bot polling to'xtatildi.");
    }

    private static async Task HandleAsync(ITelegramBotClient bot, Update update, string webAppUrl, CancellationToken ct)
    {
        var reply = BotReplyPlanner.Plan(update, webAppUrl);
        if (reply is null)
            return;

        var markup = reply.Button is null
            ? null
            : new InlineKeyboardMarkup(
                InlineKeyboardButton.WithWebApp(reply.Button.Text, new WebAppInfo { Url = reply.Button.Url }));

        await bot.SendMessage(reply.ChatId, reply.Text, replyMarkup: markup, cancellationToken: ct);
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static TimeSpan Next(TimeSpan current)
    {
        var next = current * 2;
        return next > MaxBackoff ? MaxBackoff : next;
    }

    /// <summary>Qisqa xato tavsifi (stack trace'siz — tarmoq uzilishlari log'ni to'ldirmasin).</summary>
    private static string Describe(Exception ex) => ex switch
    {
        ApiRequestException api => $"{api.ErrorCode}: {api.Message}",
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };
}
