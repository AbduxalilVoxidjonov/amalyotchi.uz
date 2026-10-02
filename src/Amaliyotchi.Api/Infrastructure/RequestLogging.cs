using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace Amaliyotchi.Api.Infrastructure;

/// <summary>Serilog request logging sozlamalari.
/// Middleware tartibi muhim: <c>UseSerilogRequestLogging</c> <c>UseExceptionHandler</c> dan OLDIN (tashqarida)
/// turadi — aks holda domen xatosi (masalan <c>ForbiddenException</c>) request logging middleware'idan
/// ishlov berilmagan holda o'tadi va u 403 o'rniga "responded 500" ni ERR darajada, stack trace bilan yozadi.
/// Tashqarida turganda u <see cref="GlobalExceptionHandler"/> yozgan haqiqiy status kodni ko'radi.</summary>
public static class RequestLogging
{
    public static void Configure(RequestLoggingOptions options) => options.GetLevel = GetLevel;

    /// <summary>Docker healthcheck endpoint'i (har 15 s) — muvaffaqiyatli javobi log shovqini.</summary>
    public const string HealthPath = "/health";

    /// <summary>5xx (yoki middleware'gacha yetib kelgan exception) — Error; 4xx (domen xatolari, 401/403/404,
    /// validatsiya, 499 bekor qilingan so'rov) — Warning; muvaffaqiyatli <c>/health</c> — Verbose (minimal daraja
    /// Information bo'lgani uchun yozilmaydi; unhealthy 503 esa Error bo'lib qoladi); qolganlari — Information.</summary>
    public static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMs, Exception? exception)
    {
        if (exception is not null)
            return LogEventLevel.Error;

        return httpContext.Response.StatusCode switch
        {
            >= 500 => LogEventLevel.Error,
            >= 400 => LogEventLevel.Warning,
            _ when httpContext.Request.Path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase)
                => LogEventLevel.Verbose,
            _ => LogEventLevel.Information,
        };
    }
}
