using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Application.Common.Behaviors;

/// <summary>Sekin so'rovlarni ogohlantirish bilan belgilaydi. 09:00 da yuzlab talaba
/// bir vaqtda check-in bosganda qaysi so'rov sekinlashganini shu log ko'rsatadi.</summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<TRequest> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long WarningThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > WarningThresholdMs)
        {
            logger.LogWarning(
                "Sekin so'rov: {RequestName} — {ElapsedMilliseconds} ms",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
