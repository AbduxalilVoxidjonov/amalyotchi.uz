using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Amaliyotchi.Api.Infrastructure;

public static class RateLimitPolicies
{
    /// <summary>Parol/Telegram bilan kirish: qattiq chegara (brute-force ga qarshi).</summary>
    public const string Auth = "auth";

    /// <summary>Token yangilash: ko'p qurilma/tab bir IP (NAT, ofis) ortida bo'lishi mumkin,
    /// shuning uchun login chelagidan alohida va kengroq.</summary>
    public const string Refresh = "refresh";

    private const int DefaultAuthPerMinute = 10;
    private const int DefaultRefreshPerMinute = 60;

    /// <summary>Chegaralar <c>RateLimiting:AuthPerMinute</c> / <c>RateLimiting:RefreshPerMinute</c> dan
    /// o'zgartiriladi (integratsiya testlari bitta IP'dan yuzlab so'rov yuboradi).</summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPerMinute = configuration.GetValue<int?>("RateLimiting:AuthPerMinute") ?? DefaultAuthPerMinute;
        var refreshPerMinute = configuration.GetValue<int?>("RateLimiting:RefreshPerMinute") ?? DefaultRefreshPerMinute;

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Parol tanlab ko'rishga qarshi: bitta IP dan daqiqasiga 10 urinish.
            options.AddPolicy(Auth, context => PerIp(context, authPerMinute));

            // Refresh: bitta IP dan daqiqasiga 60 urinish.
            options.AddPolicy(Refresh, context => PerIp(context, refreshPerMinute));
        });
    }

    // RemoteIpAddress to'g'ri bo'lishi uchun Program.cs da UseForwardedHeaders limiter dan oldin turadi.
    private static RateLimitPartition<string> PerIp(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}
