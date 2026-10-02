using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Common;

/// <summary>Production zanjiri: cloudflared → nginx (compose subnet) → API. nginx haqiqiy klient IP'ni
/// bitta qiymatli <c>X-Forwarded-For</c> da yuboradi; API uni faqat ishonchli tarmoqdan (KnownNetworks)
/// qabul qiladi va auth rate limit chelagi klient IP bo'yicha bo'linadi (hamma bitta chelakda emas).
/// TestServer'da TCP manba yo'q — <see cref="PeerHeader"/> bilan simulyatsiya qilinadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class ForwardedClientIpRateLimitTests(ApiFixture fixture)
{
    private const string PeerHeader = "X-Test-Peer";
    private const string TrustedNetwork = "10.99.0.0/16";
    private const string NginxIp = "10.99.0.5";
    private const int AuthPerMinute = 2;

    [Fact]
    public async Task IshonchliProksi_XForwardedFor_HarKlientOzChelagida()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        // Klient A limitni tugatadi.
        for (var i = 0; i < AuthPerMinute; i++)
            (await LoginAsync(client, NginxIp, "198.51.100.10")).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await LoginAsync(client, NginxIp, "198.51.100.10")).Should().Be(HttpStatusCode.TooManyRequests);

        // Xuddi shu nginx orqali boshqa klient — ta'sirlanmaydi (avval butun universitet bitta chelakda edi).
        (await LoginAsync(client, NginxIp, "198.51.100.11")).Should().NotBe(HttpStatusCode.TooManyRequests);
        // IPv6: bir /64 ichidagi manzillar bitta chelak.
        (await LoginAsync(client, NginxIp, "2001:db8:1:2::1")).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await LoginAsync(client, NginxIp, "2001:db8:1:2::ffff")).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await LoginAsync(client, NginxIp, "2001:db8:1:2:abcd::9")).Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task IshonchsizManba_XForwardedFor_EtiborsizQoldiriladi()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        const string attacker = "203.0.113.9";

        // Ishonchsiz manba har safar boshqa X-Forwarded-For yuborsa ham — o'z TCP IP'si bo'yicha bitta chelak.
        for (var i = 0; i < AuthPerMinute; i++)
            (await LoginAsync(client, attacker, $"192.0.2.{i + 1}")).Should().NotBe(HttpStatusCode.TooManyRequests);
        (await LoginAsync(client, attacker, "192.0.2.200")).Should().Be(HttpStatusCode.TooManyRequests);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPerMinute", AuthPerMinute.ToString());
            builder.UseSetting("ForwardedHeaders:KnownNetworks:0", TrustedNetwork);
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, FakePeerStartupFilter>());
        });

    private static async Task<HttpStatusCode> LoginAsync(HttpClient client, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { hemisId = "000000000000", password = "noto'g'ri-parol" })
        };
        request.Headers.Add(PeerHeader, peer);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>Pipeline boshida (UseForwardedHeaders'dan oldin) TCP manba IP'sini o'rnatadi.</summary>
    private sealed class FakePeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate nextDelegate) =>
            {
                if (context.Request.Headers.TryGetValue(PeerHeader, out var peer))
                    context.Connection.RemoteIpAddress = IPAddress.Parse(peer.ToString());
                return nextDelegate(context);
            });
            next(app);
        };
    }
}
