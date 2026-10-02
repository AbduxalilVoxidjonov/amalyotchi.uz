using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace Amaliyotchi.IntegrationTests.Common;

/// <summary>Host darajasidagi himoya: AllowedHosts (Host sarlavhasi filtri) va haqiqiy Kestrel limitlari
/// (TestServer Kestrel limitlarini qo'llamaydi — shuning uchun bu yerda haqiqiy Kestrel ko'tariladi).</summary>
[Collection(ApiCollection.Name)]
public sealed class HostingHardeningTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task AllowedHosts_RoyxatdagiHost_Otadi_Begonasi400()
    {
        using var factory = Factory.WithWebHostBuilder(b =>
            b.UseSetting("AllowedHosts", "amalyotchi.uz;app.amalyotchi.uz;api;localhost"));
        var client = factory.CreateClient();

        async Task<HttpStatusCode> MeAs(string host)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Host = host;
            return (await client.SendAsync(request)).StatusCode;
        }

        (await MeAs("amalyotchi.uz")).Should().Be(HttpStatusCode.Unauthorized, "nginx ommaviy domenni Host sifatida yuboradi");
        (await MeAs("app.amalyotchi.uz")).Should().Be(HttpStatusCode.Unauthorized);
        (await MeAs("localhost:8080")).Should().Be(HttpStatusCode.Unauthorized, "Docker healthcheck localhost:8080 ga");
        (await MeAs("api:8080")).Should().Be(HttpStatusCode.Unauthorized, "compose ichidan service nomi bilan");
        (await MeAs("evil.example.com")).Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kestrel_ServerSarlavhasiYoq_JsonTana2MbdanKatta413_FaylEndpointiOzLimitiBilan()
    {
        var admin = await Factory.CreateAdminAsync();

        using var factory = Factory.WithWebHostBuilder(_ => { });
        factory.UseKestrel(0);
        factory.StartServer();
        var client = factory.CreateClient();

        // Kichik JSON — oddiy ishlaydi va "Server" sarlavhasi yo'q.
        var login = await client.PostJsonAsync("/api/auth/login", new { admin.HemisId, admin.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.Server.Should().BeEmpty();
        var auth = (await login.Content.ReadAsync<AuthResultDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Global JSON limitidan katta tana → 413 (500 emas).
        var hugeJson = "{\"hemisId\":\"1\",\"password\":\"" + new string('a', (int)Program.MaxJsonRequestBodyBytes + 1024) + "\"}";
        // Expect: 100-continue — Kestrel tanani o'qimasdan 413 qaytaradi (aks holda klient yuborishda broken pipe oladi).
        using var hugeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(hugeJson, Encoding.UTF8, "application/json")
        };
        hugeRequest.Headers.ExpectContinue = true;
        var tooLarge = await client.SendAsync(hugeRequest);
        tooLarge.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        // Fayl endpoint'i (Excel import, limit 5 MB) 2 MB dan katta faylni qabul qiladi — o'z [RequestSizeLimit] i amal qiladi.
        var file = new byte[(int)Program.MaxJsonRequestBodyBytes + 512 * 1024];
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(ExcelImport.ContentType);
        form.Add(content, "file", "talabalar.xlsx");
        var import = await client.PostAsync("/api/admin/students/import", form);
        import.StatusCode.Should().Be(HttpStatusCode.BadRequest, "tana qabul qilindi; fayl esa haqiqiy xlsx emas");
        (await import.Content.ReadAsStringAsync()).Should().Contain(".xlsx");
    }
}
