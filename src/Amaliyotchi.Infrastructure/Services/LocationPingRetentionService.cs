using Amaliyotchi.Application.Features.Supervision.Tracking;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Infrastructure.Services;

/// <summary>Live joylashuv ping'larining saqlash muddati (60 kun) — fon tsikli: ishga tushgandan ~1 daqiqa keyin, so'ng har
/// 6 soatda <see cref="PurgeOldLocationPingsCommand"/>. Xato host'ni yiqitmaydi (log + keyingi tsikl). Faqat API host'ida
/// (<c>AddLocationPingRetention</c>); bir nechta instansiyada parallel ishlashi zararsiz (DELETE idempotent).</summary>
public sealed class LocationPingRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<LocationPingRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new PurgeOldLocationPingsCommand(), stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Live joylashuv: {Count} ta eski ping o'chirildi (60 kundan eski).", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Live joylashuv: eski ping'larni o'chirishda xato ({Type}: {Message}).", ex.GetType().Name, ex.Message);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
