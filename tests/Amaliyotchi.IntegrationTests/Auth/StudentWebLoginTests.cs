using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Auth;

/// <summary>Talabaning brauzer rejimi: xodim parol o'rnatadi (<c>POST /api/{admin|tutor}/students/{id}/password</c>),
/// talaba profil HEMIS ID'si + parol bilan kiradi (<c>POST /api/auth/login</c>) va parolini o'zi almashtiradi
/// (<c>POST /api/auth/change-password</c>).</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentWebLoginTests(ApiFixture fixture)
{
    private const string TempPassword = "Vaqtinchalik-1";
    private const string NewPassword = "Yangi-Parol-77";

    private ApiFactory Factory => fixture.Factory;

    // ---------- login ----------

    [Fact]
    public async Task Login_AdminParolOrnatgan_Talaba_200_StudentRoli_MustChangePassword()
    {
        var student = await Factory.CreateStudentAsync();
        var hemisId = await ProfileHemisIdAsync(student.Id);
        var admin = await Factory.LoginAsAdminAsync();

        (await admin.PostJsonAsync($"/api/admin/students/{student.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password = TempPassword });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var auth = await response.Content.ReadAsync<AuthResultDto>();
        auth!.User.Id.Should().Be(student.Id);
        auth.User.Role.Should().Be(UserRole.Student);
        auth.User.HemisId.Should().Be(hemisId);
        auth.User.GroupId.Should().Be(student.GroupId);
        auth.MustChangePassword.Should().BeTrue();

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue("JSON camelCase");
        json.RootElement.GetProperty("user").GetProperty("role").GetString().Should().Be("student");

        // Talaba token bilan o'z endpoint'lariga kira oladi.
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        (await client.GetAsync("/api/student/profile")).StatusCode.Should().Be(HttpStatusCode.OK);

        var logged = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.LoggedIn && a.EntityId == student.Id.ToString()));
        logged.Should().BeTrue();
    }

    [Fact]
    public async Task Login_ParoliYoqTalaba_403_UmumiyXabar_VaAudit()
    {
        var student = await Factory.CreateStudentAsync();
        var hemisId = await ProfileHemisIdAsync(student.Id);
        var admin = await Factory.CreateAdminAsync();
        var anonymous = Factory.CreateClient();

        var noPassword = await anonymous.PostJsonAsync("/api/auth/login", new { hemisId, password = "Istalgan-Parol" });
        var unknown = await anonymous.PostJsonAsync("/api/auth/login", new { hemisId = TestClients.RandomHemisId(), password = "Istalgan-Parol" });
        var wrongAdmin = await anonymous.PostJsonAsync("/api/auth/login", new { admin.HemisId, password = "Notogri-Parol" });

        noPassword.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unknown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(noPassword)).Should().Be(await Detail(unknown), "hisob borligi oshkor qilinmaydi")
            .And.Be(await Detail(wrongAdmin));

        var failed = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.LoginFailed && a.EntityId == student.Id.ToString()));
        failed.Should().BeTrue();
    }

    [Fact]
    public async Task Login_NotogriParol_403_FaolEmas_403_OchirilganProfil_403()
    {
        var group = await Factory.CreateGroupAsync();
        var active = await CreateStudentWithPasswordAsync(group);
        var inactive = await CreateStudentWithPasswordAsync(group, active: false);
        var deleted = await CreateStudentWithPasswordAsync(group);
        await Factory.WithDbAsync(async db =>
        {
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == deleted.Id);
            profile.IsDeleted = true;
            await db.SaveChangesAsync();
        });
        var anonymous = Factory.CreateClient();

        var wrong = await anonymous.PostJsonAsync("/api/auth/login", new { active.HemisId, password = "Notogri-Parol" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(wrong)).Should().Be("HEMIS ID yoki parol noto'g'ri.");

        var notActive = await anonymous.PostJsonAsync("/api/auth/login", new { inactive.HemisId, password = TempPassword });
        notActive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(notActive)).Should().Contain("faol emas");

        var removed = await anonymous.PostJsonAsync("/api/auth/login", new { deleted.HemisId, password = TempPassword });
        removed.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(removed)).Should().Be("HEMIS ID yoki parol noto'g'ri.", "o'chirilgan profil topilmaydi");

        (await anonymous.PostJsonAsync("/api/auth/login", new { active.HemisId, password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TelegramLogin_Va_Refresh_MustChangePasswordQaytaradi()
    {
        var student = await Factory.CreateStudentAsync();

        var (_, auth) = await Factory.TelegramLoginAsync(student.TelegramId!.Value);
        auth.MustChangePassword.Should().BeFalse();

        var refresh = await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();
    }

    // ---------- xodim parol o'rnatadi ----------

    [Fact]
    public async Task AdminParolOrnatadi_RefreshTokenlarBekor_Audit_HasPassword()
    {
        var student = await Factory.CreateStudentAsync();
        var (_, telegram) = await Factory.TelegramLoginAsync(student.TelegramId!.Value);
        var admin = await Factory.LoginAsAdminAsync();

        (await admin.GetFromJsonAsync<AdminStudentDetail>($"/api/admin/students/{student.Id}", JsonDefaults.Options))!
            .HasPassword.Should().BeFalse();

        (await admin.PostJsonAsync($"/api/admin/students/{student.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { telegram.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "talabaning eski sessiyasi bekor qilindi");

        var detailJson = await admin.GetStringAsync($"/api/admin/students/{student.Id}");
        using (var json = JsonDocument.Parse(detailJson))
            json.RootElement.GetProperty("hasPassword").GetBoolean().Should().BeTrue();

        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.Id == student.Id));
        user.MustChangePassword.Should().BeTrue();

        var audited = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.StudentPasswordSet && a.EntityId == student.Id.ToString()));
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task AdminParolOrnatish_Qisqa_400_Topilmasa_404_TyutorId_404()
    {
        var student = await Factory.CreateStudentAsync();
        var tutor = await Factory.CreateTutorAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var shortPassword = await admin.PostJsonAsync($"/api/admin/students/{student.Id}/password", new { password = "1234567" });
        shortPassword.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(shortPassword)).TryGetProperty("Password", out _).Should().BeTrue();

        (await admin.PostJsonAsync($"/api/admin/students/{Guid.CreateVersion7()}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostJsonAsync($"/api/admin/students/{tutor.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "talaba emas");
    }

    [Fact]
    public async Task TyutorParolOrnatadi_KolamIchida_204_Tashqarisida_404_Talaba_403()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var own = await Factory.CreateStudentAsync(group: group);
        var foreign = await Factory.CreateStudentAsync();
        var tutorClient = await Factory.LoginAsync(tutor);

        (await tutorClient.GetFromJsonAsync<TutorStudentDetail>($"/api/tutor/students/{own.Id}", JsonDefaults.Options))!
            .HasPassword.Should().BeFalse();

        (await tutorClient.PostJsonAsync($"/api/tutor/students/{own.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await tutorClient.PostJsonAsync($"/api/tutor/students/{foreign.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "ko'lamdan tashqari");

        (await tutorClient.GetFromJsonAsync<TutorStudentDetail>($"/api/tutor/students/{own.Id}", JsonDefaults.Options))!
            .HasPassword.Should().BeTrue();

        var hemisId = await ProfileHemisIdAsync(own.Id);
        var login = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password = TempPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await login.Content.ReadAsync<AuthResultDto>())!.MustChangePassword.Should().BeTrue();

        var studentClient = await Factory.LoginAsStudentAsync(foreign);
        (await studentClient.PostJsonAsync($"/api/tutor/students/{own.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.PostJsonAsync($"/api/admin/students/{own.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutorClient.PostJsonAsync($"/api/admin/students/{own.Id}/password", new { password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "tyutor admin endpoint'iga kira olmaydi");
    }

    // ---------- change-password ----------

    [Fact]
    public async Task ParolAlmashtirish_NotogriJoriy_400_QisqaYangi_400()
    {
        var student = await CreateStudentWithPasswordAsync();
        var (client, _) = await LoginAsync(student.HemisId, TempPassword);

        var wrong = await client.PostJsonAsync("/api/auth/change-password", new { currentPassword = "Notogri-Parol", newPassword = NewPassword });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(wrong)).TryGetProperty("CurrentPassword", out _).Should().BeTrue();

        var shortNew = await client.PostJsonAsync("/api/auth/change-password", new { currentPassword = TempPassword, newPassword = "1234567" });
        shortNew.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(shortNew)).TryGetProperty("NewPassword", out _).Should().BeTrue();

        var same = await client.PostJsonAsync("/api/auth/change-password", new { currentPassword = TempPassword, newPassword = TempPassword });
        same.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(same)).TryGetProperty("NewPassword", out _).Should().BeTrue();

        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.Id == student.Id));
        user.MustChangePassword.Should().BeTrue("hech narsa o'zgarmadi");
    }

    [Fact]
    public async Task ParolAlmashtirish_204_FlagFalse_YangiParolIshlaydi_BoshqaSessiyalarBekor()
    {
        var student = await CreateStudentWithPasswordAsync();
        var (_, other) = await LoginAsync(student.HemisId, TempPassword);
        var (client, current) = await LoginAsync(student.HemisId, TempPassword);

        var change = await client.PostJsonAsync("/api/auth/change-password",
            new { currentPassword = TempPassword, newPassword = NewPassword, refreshToken = current.RefreshToken });
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new { student.HemisId, password = TempPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "eski parol endi ishlamaydi");

        var (_, fresh) = await LoginAsync(student.HemisId, NewPassword);
        fresh.MustChangePassword.Should().BeFalse();

        (await anonymous.PostJsonAsync("/api/auth/refresh", new { other.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "boshqa sessiya bekor qilindi");
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { current.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "joriy sessiya saqlanadi");

        var audited = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.PasswordChanged && a.EntityId == student.Id.ToString()));
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task ParolAlmashtirish_Tyutor_204_ParolsizTalaba_400_Tokensiz_401()
    {
        var tutor = await Factory.CreateTutorAsync();
        var tutorClient = await Factory.LoginAsync(tutor);
        (await tutorClient.PostJsonAsync("/api/auth/change-password", new { currentPassword = tutor.Password, newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { tutor.HemisId, password = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var telegramOnly = await Factory.CreateStudentAsync();
        var studentClient = await Factory.LoginAsStudentAsync(telegramOnly);
        var noPassword = await studentClient.PostJsonAsync("/api/auth/change-password", new { currentPassword = "Istalgan-Parol", newPassword = NewPassword });
        noPassword.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(noPassword)).TryGetProperty("CurrentPassword", out _).Should().BeTrue();

        (await Factory.CreateClient().PostJsonAsync("/api/auth/change-password", new { currentPassword = "a", newPassword = NewPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- yordamchilar ----------

    private sealed record PasswordStudent(Guid Id, string HemisId);

    /// <summary>Talaba yaratadi va admin API orqali unga <see cref="TempPassword"/> o'rnatadi.</summary>
    private async Task<PasswordStudent> CreateStudentWithPasswordAsync(TestGroup? group = null, bool active = true)
    {
        var student = await Factory.CreateStudentAsync(group: group);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PostJsonAsync($"/api/admin/students/{student.Id}/password", new { password = TempPassword }))
            .EnsureSuccessStatusCode();

        if (!active)
        {
            await Factory.WithDbAsync(async db =>
            {
                var user = await db.Users.Include(u => u.RefreshTokens).FirstAsync(u => u.Id == student.Id);
                user.Deactivate();
                await db.SaveChangesAsync();
            });
        }

        return new PasswordStudent(student.Id, await ProfileHemisIdAsync(student.Id));
    }

    private async Task<(HttpClient Client, AuthResultDto Auth)> LoginAsync(string hemisId, string password)
    {
        var client = Factory.CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new { hemisId, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var auth = (await response.Content.ReadAsync<AuthResultDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private Task<string> ProfileHemisIdAsync(Guid studentId) =>
        Factory.WithDbAsync(db => db.StudentProfiles.Where(p => p.UserId == studentId).Select(p => p.HemisId).SingleAsync());

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }

    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Clone();
    }
}
