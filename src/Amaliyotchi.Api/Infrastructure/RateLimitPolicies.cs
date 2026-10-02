using System.Net;
using System.Net.Sockets;
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

    // RemoteIpAddress to'g'ri bo'lishi uchun Program.cs da UseForwardedHeaders limiter dan oldin turadi
    // (production: nginx CF-Connecting-IP → X-Forwarded-For; aks holda hamma cloudflared IP'si bilan bitta chelakda).
    private static RateLimitPartition<string> PerIp(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    /// <summary>Chelak kaliti: IPv4 (IPv4-mapped IPv6 ham — <c>::ffff:1.2.3.4</c> → <c>1.2.3.4</c>) — to'liq manzil;
    /// IPv6 — /64 prefiks (bitta abonent odatda butun /64 oladi — har so'rovda manzil almashtirib limitni
    /// chetlab o'tolmasin).</summary>
    public static string PartitionKey(IPAddress? address)
    {
        if (address is null)
            return "unknown";
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return new IPAddress(bytes).ToString() + "/64";
    }
}
