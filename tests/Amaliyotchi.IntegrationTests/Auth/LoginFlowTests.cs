using System.Net;
using System.Net.Http.Headers;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginFlowTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Login_Me_Refresh_Logout_ToliqOqim()
    {
        var tutor = await Factory.CreateTutorAsync();

        // 1. Login
        var (client, first) = await Factory.LoginWithResultAsync(tutor);
        first.User.Id.Should().Be(tutor.Id);
        first.User.Role.Should().Be(UserRole.Tutor);
        first.User.FacultyId.Should().Be(tutor.FacultyId);
        first.User.GroupId.Should().BeNull("tyutorda profil yo'q");
        first.User.HemisId.Should().Be(tutor.HemisId);
        first.RefreshToken.Should().NotBeNullOrEmpty();

        // 2. Me
        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await me.Content.ReadAsync<UserSummaryDto>();
        summary!.FullName.Should().Be(tutor.FullName);
        summary.PhoneNumber.Should().Be(tutor.PhoneNumber);

        // 3. Refresh — rotatsiya
        var anonymous = Factory.CreateClient();
        var refresh = await anonymous.PostJsonAsync("/api/auth/refresh", new { first.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await refresh.Content.ReadAsync<AuthResultDto>();
        second!.RefreshToken.Should().NotBe(first.RefreshToken);
        second.AccessToken.Should().NotBeNullOrEmpty();

        // Eski refresh token endi ishlamaydi
        var reuse = await anonymous.PostJsonAsync("/api/auth/refresh", new { first.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Logout (yangi access token bilan, yangi refresh tokenni bekor qilamiz)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        var logout = await client.PostJsonAsync("/api/auth/logout", new { second.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Bekor qilingan token bilan refresh — 403
        var afterLogout = await anonymous.PostJsonAsync("/api/auth/refresh", new { second.RefreshToken });
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefreshToken_BazadaOchiqEmas_FaqatXeshSaqlanadi()
    {
        var tutor = await Factory.CreateTutorAsync();
        var (_, auth) = await Factory.LoginWithResultAsync(tutor);

        var stored = await Factory.WithDbAsync(db =>
            db.RefreshTokens.Where(t => t.UserId == tutor.Id).Select(t => t.Token).ToListAsync());

        stored.Should().NotContain(auth.RefreshToken, "baza sizib chiqsa ham xom token bilan sessiya olib bo'lmasin");
        stored.Should().Contain(RefreshTokenHash.Of(auth.RefreshToken));

        // Xeshning o'zi refresh token sifatida qabul qilinmaydi.
        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { refreshToken = RefreshTokenHash.Of(auth.RefreshToken) }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_NotogriParol_403_VaProblemDetails()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = Factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login", new { admin.HemisId, Password = "notogri-parol" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":403").And.Contain("\"traceId\"");
    }

    [Fact]
    public async Task Login_QisqaParol_400_Errors()
    {
        var client = Factory.CreateClient();

        var response = await client.PostJsonAsync("/api/auth/login", new { HemisId = "100000000001", Password = "123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"errors\"").And.Contain("\"Password\"");
    }

    [Fact]
    public async Task Me_TokensizSorov_401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginAsAdmin_Helper_AdminRoli()
    {
        var client = await Factory.LoginAsAdminAsync();

        var me = await (await client.GetAsync("/api/auth/me")).Content.ReadAsync<UserSummaryDto>();

        me!.Role.Should().Be(UserRole.Admin);
        me.FacultyId.Should().BeNull();
    }
}
