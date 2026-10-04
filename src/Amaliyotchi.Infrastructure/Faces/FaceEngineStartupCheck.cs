using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Infrastructure.Faces;

/// <summary>Startup'da yuz dvigatelini yuklaydi (birinchi check-in kutmasin) va holatini log'ga yozadi: modellar yo'q bo'lsa
/// OGOHLANTIRISH — <c>faceVerificationEnabled</c> yoqilsa check-in/etalon so'rovlari 503 oladi.</summary>
internal sealed class FaceEngineStartupCheck(IFaceEngine engine, ILogger<FaceEngineStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (engine.IsReady)
            logger.LogInformation("Yuz dvigateli tayyor (YuNet + SFace).");
        else
            logger.LogWarning(
                "Yuz dvigateli ISHLAMAYDI: {Reason} 'faceVerificationEnabled' yoqilsa check-in va yuz yuborish 503 qaytaradi.",
                engine.UnavailableReason);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
