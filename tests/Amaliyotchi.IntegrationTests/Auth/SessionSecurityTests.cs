using System.Net;
using System.Net.Http.Headers;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Auth;

/// <summary>Production xavfsizligi: hisob lockout, refresh token qayta ishlatilishini aniqlash va access token
/// security stamp (faolsizlantirish / parol almashtirish eski access tokenni bekor qiladi).</summary>
[Collection(ApiCollection.Name)]
public sealed class SessionSecurityTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, TestUser user, string password)
        => client.PostJsonAsync("/api/auth/login", new { user.HemisId, password });

    [Fact]
    public async Task Lockout_KopNotogriParol_ToGriParolHamRad_XabarBirXil_OynadanKeyinOchiladi()
    {
        var tutor = await Factory.CreateTutorAsync();
        var client = Factory.CreateClient();

        string? wrongDetail = null;
        for (var i = 0; i < AuthSecurity.LockoutThreshold; i++)
        {
            var wrong = await LoginAsync(client, tutor, "Notogri-Parol-123");
            wrong.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            wrongDetail = (await wrong.Content.ReadAsync<ProblemDetails>())!.Detail;
        }

        // Bloklangan: to'g'ri parol ham rad, xabar "noto'g'ri parol" bilan bir xil (hisob borligi oshkor emas).
        var locked = await LoginAsync(client, tutor, tutor.Password);
        locked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await locked.Content.ReadAsync<ProblemDetails>())!.Detail.Should().Be(wrongDetail);

        // Boshqa hisob bloklanmaydi.
        var other = await Factory.CreateTutorAsync();
        (await LoginAsync(client, other, other.Password)).StatusCode.Should().Be(HttpStatusCode.OK);

        fixture.Clock.Set(DateTimeOffset.UtcNow + AuthSecurity.LockoutWindow + TimeSpan.FromMinutes(1));
        try
        {
            (await LoginAsync(client, tutor, tutor.Password)).StatusCode.Should().Be(HttpStatusCode.OK, "blok oynasi o'tdi");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Lockout_MuvaffaqiyatliKirish_HisoblagichniNollaydi()
    {
        var tutor = await Factory.CreateTutorAsync();
        var client = Factory.CreateClient();

        for (var i = 0; i < AuthSecurity.LockoutThreshold - 1; i++)
            await LoginAsync(client, tutor, "Notogri-Parol-123");
        (await LoginAsync(client, tutor, tutor.Password)).StatusCode.Should().Be(HttpStatusCode.OK);

        await LoginAsync(client, tutor, "Notogri-Parol-123");
        (await LoginAsync(client, tutor, tutor.Password)).StatusCode.Should().Be(HttpStatusCode.OK,
            "oldingi xatolar muvaffaqiyatli kirishdan keyin hisoblanmaydi");
    }

    [Fact]
    public async Task RefreshReuse_GraceDanKeyin_BarchaSessiyalarBekor()
    {
        var tutor = await Factory.CreateTutorAsync();
        var (_, first) = await Factory.LoginWithResultAsync(tutor);
        var (_, otherDevice) = await Factory.LoginWithResultAsync(tutor);
        var anonymous = Factory.CreateClient();

        var rotated = await anonymous.PostJsonAsync("/api/auth/refresh", new { first.RefreshToken });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadAsync<AuthResultDto>())!;

        fixture.Clock.Set(DateTimeOffset.UtcNow + AuthSecurity.RefreshReuseGracePeriod + TimeSpan.FromSeconds(5));
        try
        {
            // O'g'irlangan (almashtirilgan) token qayta keldi → 403 va butun zanjir bekor.
            (await anonymous.PostJsonAsync("/api/auth/refresh", new { first.RefreshToken })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await anonymous.PostJsonAsync("/api/auth/refresh", new { second.RefreshToken })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, "yangi token ham bekor qilindi");
            (await anonymous.PostJsonAsync("/api/auth/refresh", new { otherDevice.RefreshToken })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, "foydalanuvchining barcha sessiyalari bekor");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var reasons = await Factory.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.UserId == tutor.Id && t.RevokedReason == AuthSecurity.RefreshReuseRevokeReason)
            .CountAsync());
        reasons.Should().Be(2);
    }

    [Fact]
    public async Task AccessToken_HisobFaolsizlantirilsa_Darhol401()
    {
        var tutor = await Factory.CreateTutorAsync();
        var client = await Factory.LoginAsync(tutor);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.Include(u => u.RefreshTokens).FirstAsync(u => u.Id == tutor.Id);
            user.Deactivate();
            await db.SaveChangesAsync();
        });

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessToken_ParolAlmashtirilsa_Eskisi401_JoriySessiyaRefreshBilanDavomEtadi()
    {
        var tutor = await Factory.CreateTutorAsync();
        var (client, auth) = await Factory.LoginWithResultAsync(tutor);
        var (otherClient, _) = await Factory.LoginWithResultAsync(tutor);

        var change = await client.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = tutor.Password, newPassword = "Yangi-Parol-2026!", auth.RefreshToken });
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await otherClient.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Joriy sessiya refresh token bilan yangi access token oladi (mijoz 401 da shunday qiladi).
        var refresh = await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = (await refresh.Content.ReadAsync<AuthResultDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
