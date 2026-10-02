using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Application.Features.Auth.ChangeLogin;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Auth;

/// <summary>Admin sozlamalari: login (HEMIS ID) bandligini tekshirish (<c>GET /api/auth/login-available</c>),
/// loginni almashtirish (<c>POST /api/auth/change-login</c>) va parol almashtirish regressiyasi.</summary>
[Collection(ApiCollection.Name)]
public sealed class ChangeLoginTests(ApiFixture fixture)
{
    private const string NewPassword = "Yangi-Parol-77";

    private ApiFactory Factory => fixture.Factory;

    // ---------- login-available ----------

    [Fact]
    public async Task Available_BoshLogin_True_Normallashtirilgan()
    {
        var admin = await Factory.LoginAsAdminAsync();
        var login = TestClients.RandomHemisId();

        var dto = await AvailableAsync(admin, $"  {login} ");

        dto.Available.Should().BeTrue();
        dto.Normalized.Should().Be(login);
        dto.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Available_BoshqaAdmin_Tyutor_Talaba_OchirilganHisob_Band()
    {
        var admin = await Factory.LoginAsAdminAsync();
        var otherAdmin = await Factory.CreateAdminAsync();
        var tutor = await Factory.CreateTutorAsync();
        var student = await Factory.CreateStudentAsync();
        var studentHemis = await ProfileHemisIdAsync(student.Id);

        var deleted = await Factory.CreateTutorAsync();
        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == deleted.Id);
            user.IsDeleted = true;
            user.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        foreach (var login in new[] { otherAdmin.HemisId, tutor.HemisId, studentHemis, deleted.HemisId })
        {
            var dto = await AvailableAsync(admin, login);
            dto.Available.Should().BeFalse(login);
            dto.Normalized.Should().Be(login);
            dto.Reason.Should().Be("Bu login allaqachon band.");
        }
    }

    [Fact]
    public async Task Available_OzLogini_Va_NotogriFormat_False_200()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);

        var own = await AvailableAsync(client, admin.HemisId);
        own.Available.Should().BeFalse();
        own.Reason.Should().Be("Bu sizning joriy loginingiz.");

        foreach (var bad in new[] { "12ab5", "123", "123456789012345678901", " " })
        {
            var dto = await AvailableAsync(client, bad);
            dto.Available.Should().BeFalse(bad);
            dto.Reason.Should().NotBeNullOrWhiteSpace();
        }

        (await AvailableAsync(client, "abc12")).Normalized.Should().Be("abc12");
    }

    [Fact]
    public async Task Available_Tyutor_Talaba_403_Anonim_401()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/auth/login-available?login=123456")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var student = await Factory.LoginAsStudentAsync(await Factory.CreateStudentAsync());
        (await student.GetAsync("/api/auth/login-available?login=123456")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().GetAsync("/api/auth/login-available?login=123456"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- change-login ----------

    [Fact]
    public async Task ChangeLogin_200_YangiLoginIshlaydi_EskisiYoq_EskiAccess401_JoriySessiyaRefreshBilan_Audit()
    {
        var admin = await Factory.CreateAdminAsync();
        var (client, auth) = await Factory.LoginWithResultAsync(admin);
        var newLogin = TestClients.RandomHemisId();

        var response = await client.PostJsonAsync("/api/auth/change-login",
            new { newLogin = $" {newLogin} ", currentPassword = admin.Password });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var summary = await response.Content.ReadAsync<UserSummaryDto>();
        summary!.Id.Should().Be(admin.Id);
        summary.Role.Should().Be(UserRole.Admin);
        summary.HemisId.Should().Be(newLogin);

        // Login o'zgardi → eski access token (security stamp) rad etiladi; joriy sessiya refresh bilan davom etadi
        // (mijoz 401 da avtomatik refresh qiladi). refreshToken yuborilmadi — refresh tokenlarga tegilmaydi.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var refreshed = await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await refreshed.Content.ReadAsync<AuthResultDto>())!.AccessToken);

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.Content.ReadAsync<UserSummaryDto>())!.HemisId.Should().Be(newLogin);

        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new { hemisId = newLogin, password = admin.Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostJsonAsync("/api/auth/login", new { admin.HemisId, admin.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "eski login endi ishlamaydi (login xatosi — 403)");

        var log = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == AuditAction.LoginChanged && a.EntityId == admin.Id.ToString()));
        log.UserId.Should().Be(admin.Id);
        log.Changes.Should().Contain(admin.HemisId).And.Contain(newLogin).And.NotContain(admin.Password);
        using var changes = JsonDocument.Parse(log.Changes!);
        changes.RootElement.GetProperty("oldLogin").GetString().Should().Be(admin.HemisId);
        changes.RootElement.GetProperty("newLogin").GetString().Should().Be(newLogin);

        // Yangi login endi band (o'zi uchun — "joriy loginingiz").
        (await AvailableAsync(client, newLogin)).Reason.Should().Be("Bu sizning joriy loginingiz.");
        (await AvailableAsync(await Factory.LoginAsAdminAsync(), newLogin)).Reason.Should().Be("Bu login allaqachon band.");
    }

    [Fact]
    public async Task ChangeLogin_RefreshTokenBerilsa_BoshqaSessiyalarBekor_JoriySaqlanadi()
    {
        var admin = await Factory.CreateAdminAsync();
        var (_, other) = await Factory.LoginWithResultAsync(admin);
        var (client, current) = await Factory.LoginWithResultAsync(admin);

        (await client.PostJsonAsync("/api/auth/change-login",
                new { newLogin = TestClients.RandomHemisId(), currentPassword = admin.Password, current.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { other.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "boshqa sessiya bekor qilindi");
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { current.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "joriy sessiya saqlanadi");
    }

    [Fact]
    public async Task ChangeLogin_NotogriParol_400_OzLogini_400_Format_400_HechNarsaOzgarmaydi()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);

        var wrong = await client.PostJsonAsync("/api/auth/change-login",
            new { newLogin = TestClients.RandomHemisId(), currentPassword = "Notogri-Parol" });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(wrong, "CurrentPassword")).Should().Be("Joriy parol noto'g'ri.");

        var same = await client.PostJsonAsync("/api/auth/change-login",
            new { newLogin = admin.HemisId, currentPassword = admin.Password });
        same.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(same, "NewLogin")).Should().Be("Yangi login joriy logindan farq qilishi kerak.");

        var format = await client.PostJsonAsync("/api/auth/change-login",
            new { newLogin = "12ab", currentPassword = admin.Password });
        format.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(format, "NewLogin")).Should().NotBeNullOrWhiteSpace();

        var empty = await client.PostJsonAsync("/api/auth/change-login", new { newLogin = "", currentPassword = "" });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(empty, "NewLogin")).Should().NotBeNullOrWhiteSpace();
        (await ErrorAsync(empty, "CurrentPassword")).Should().NotBeNullOrWhiteSpace();

        var stored = await Factory.WithDbAsync(db =>
            db.Users.AsNoTracking().Where(u => u.Id == admin.Id).Select(u => u.HemisId).SingleAsync());
        stored.Should().Be(admin.HemisId);
    }

    [Fact]
    public async Task ChangeLogin_TyutorVaTalabaLogini_409()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);
        var tutor = await Factory.CreateTutorAsync();
        var student = await Factory.CreateStudentAsync();
        var studentHemis = await ProfileHemisIdAsync(student.Id);

        foreach (var taken in new[] { tutor.HemisId, studentHemis })
        {
            var response = await client.PostJsonAsync("/api/auth/change-login",
                new { newLogin = taken, currentPassword = admin.Password });
            response.StatusCode.Should().Be(HttpStatusCode.Conflict, taken);
            (await DetailAsync(response)).Should().Be("Bu login allaqachon band.");
        }

        var logged = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.LoginChanged && a.EntityId == admin.Id.ToString()));
        logged.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeLogin_Tyutor_Talaba_403_Anonim_401()
    {
        var tutor = await Factory.CreateTutorAsync();
        var tutorClient = await Factory.LoginAsync(tutor);
        (await tutorClient.PostJsonAsync("/api/auth/change-login",
                new { newLogin = TestClients.RandomHemisId(), currentPassword = tutor.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var student = await Factory.LoginAsStudentAsync(await Factory.CreateStudentAsync());
        (await student.PostJsonAsync("/api/auth/change-login",
                new { newLogin = TestClients.RandomHemisId(), currentPassword = "Istalgan-Parol" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().PostJsonAsync("/api/auth/change-login",
                new { newLogin = TestClients.RandomHemisId(), currentPassword = "Istalgan-Parol" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- change-password (admin regressiyasi) ----------

    [Fact]
    public async Task ChangePassword_Admin_204_YangiParolBilanKiradi()
    {
        var admin = await Factory.CreateAdminAsync();
        var (client, current) = await Factory.LoginWithResultAsync(admin);

        (await client.PostJsonAsync("/api/auth/change-password",
                new { currentPassword = admin.Password, newPassword = NewPassword, current.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new { admin.HemisId, password = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostJsonAsync("/api/auth/login", new { admin.HemisId, admin.Password }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Parol o'zgardi → eski access token rad; joriy sessiya saqlangan refresh token bilan yangilanadi.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var refreshed = await anonymous.PostJsonAsync("/api/auth/refresh", new { current.RefreshToken });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await refreshed.Content.ReadAsync<AuthResultDto>())!.AccessToken);

        var wrong = await client.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = "Notogri-Parol", newPassword = "Boshqa-Parol-1" });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(wrong, "CurrentPassword")).Should().Be("Joriy parol noto'g'ri.");
    }

    // ---------- yordamchilar ----------

    private static async Task<LoginAvailabilityDto> AvailableAsync(HttpClient client, string login)
    {
        var response = await client.GetAsync($"/api/auth/login-available?login={Uri.EscapeDataString(login)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<LoginAvailabilityDto>())!;
    }

    private Task<string> ProfileHemisIdAsync(Guid studentId) =>
        Factory.WithDbAsync(db => db.StudentProfiles.Where(p => p.UserId == studentId).Select(p => p.HemisId).SingleAsync());

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response, string key)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").GetProperty(key)[0].GetString();
    }
}
