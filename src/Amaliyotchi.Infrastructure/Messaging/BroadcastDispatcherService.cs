using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Messaging;

/// <summary>"Xabarlar" fon tsikli: navbat bo'sh bo'lsa ~1 s kutadi, partiya to'la bo'lsa darhol keyingisini oladi,
/// 429 da Telegram aytgan vaqtcha to'xtaydi. Xato host'ni yiqitmaydi (log + backoff). Faqat API host'ida
/// (<c>AddBroadcastDispatcher</c>); <c>Messaging:DispatcherEnabled=false</c> bo'lsa ishlamaydi.</summary>
public sealed class BroadcastDispatcherService(
    BroadcastDispatcher dispatcher,
    IOptions<MessagingOptions> options,
    ILogger<BroadcastDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan MinErrorBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxErrorBackoff = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.DispatcherEnabled)
        {
            logger.LogInformation("Xabarlar dispetcheri o'chiq (Messaging:DispatcherEnabled=false).");
            return;
        }

        await Task.Yield();
        var poll = TimeSpan.FromMilliseconds(Math.Clamp(settings.PollIntervalMs, 100, 60_000));
        var errorBackoff = MinErrorBackoff;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                var result = await dispatcher.RunOnceAsync(stoppingToken);
                errorBackoff = MinErrorBackoff;
                delay = result.PauseFor ?? (result.Claimed >= Math.Max(settings.BatchSize, 1) ? TimeSpan.Zero : poll);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Stack trace'siz — DB uzilishi log'ni to'ldirmasin; xabar matni/token bu yerga tushmaydi.
                logger.LogWarning("Xabarlar dispetcheri xatosi ({Type}: {Message}); {Delay} dan keyin qayta urinish.",
                    ex.GetType().Name, ex.Message, errorBackoff);
                delay = errorBackoff;
                errorBackoff = errorBackoff * 2 > MaxErrorBackoff ? MaxErrorBackoff : errorBackoff * 2;
            }

            if (delay <= TimeSpan.Zero)
                continue;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Xabarlar dispetcheri to'xtatildi.");
    }
}
