using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Admins;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Infrastructure.Identity;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Amaliyotchi.IntegrationTests.Faces;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Bosh admin: <c>/api/admin/admins</c> CRUD, invariantlar (bosh admin va o'zini bloklab bo'lmaydi), oddiy admin → 403,
/// <c>isSuperAdmin</c> auth javoblarida.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminAdminsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private const string Url = "/api/admin/admins";

    private async Task<(TestUser User, HttpClient Client)> SuperAsync()
    {
        var super = await Factory.CreateSuperAdminAsync();
        return (super, await Factory.LoginAsync(super));
    }

    private static async Task<T> OkAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        response.StatusCode.Should().Be(status, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<T>())!;
    }

    [Fact]
    public async Task Royxat_Shakl_BoshAdminlarBirinchi()
    {
        var (super, client) = await SuperAsync();
        var plain = await Factory.CreateAdminAsync("Oddiy Royxat Admin");

        var response = await client.GetAsync(Url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using (var json = JsonDocument.Parse(body))
        {
            var item = json.RootElement.GetProperty("items").EnumerateArray()
                .Single(i => i.GetProperty("id").GetGuid() == plain.Id);
            item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
                ["id", "fullName", "login", "phoneNumber", "isActive", "isSuperAdmin", "lastLoginAt", "createdAt"]);
            item.GetProperty("login").GetString().Should().Be(plain.HemisId);
            item.GetProperty("isSuperAdmin").GetBoolean().Should().BeFalse();
        }

        var list = JsonSerializer.Deserialize<AdminListDto>(body, JsonDefaults.Options)!;
        list.Items.Should().Contain(i => i.Id == super.Id && i.IsSuperAdmin && i.LastLoginAt != null);
        list.Items.Should().NotContain(i => i.Login == string.Empty, "faqat admin hisoblari (talaba yo'q)");
        list.Items.TakeWhile(i => i.IsSuperAdmin).Should().NotBeEmpty("bosh adminlar birinchi");
        list.Items.SkipWhile(i => i.IsSuperAdmin).Should().OnlyContain(i => !i.IsSuperAdmin);
    }

    [Fact]
    public async Task Yaratish_ParolsizVaqtinchalikParol_KirishMustChange_Audit()
    {
        var (_, client) = await SuperAsync();
        var login = TestClients.RandomHemisId();

        var created = await OkAsync<CreatedAdminDto>(
            await client.PostJsonAsync(Url, new { fullName = "  Yangi Admin  ", login, phoneNumber = "90 111 22 33" }),
            HttpStatusCode.Created);

        created.FullName.Should().Be("Yangi Admin");
        created.Login.Should().Be(login);
        created.PhoneNumber.Should().Be("+998901112233");
        created.IsActive.Should().BeTrue();
        created.IsSuperAdmin.Should().BeFalse();
        created.LastLoginAt.Should().BeNull();
        created.TemporaryPassword.Should().NotBeNullOrEmpty().And.HaveLength(12);

        var anonymous = Factory.CreateClient();
        var auth = await OkAsync<AuthResultDto>(
            await anonymous.PostJsonAsync("/api/auth/login", new { hemisId = login, password = created.TemporaryPassword }));
        auth.MustChangePassword.Should().BeTrue("server yaratgan parol — vaqtinchalik");
        auth.User.Role.Should().Be(UserRole.Admin);
        auth.User.IsSuperAdmin.Should().BeFalse();

        var audit = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == AuditAction.AdminCreated && a.EntityId == created.Id.ToString()));
        using (var changes = JsonDocument.Parse(audit.Changes!))
        {
            changes.RootElement.GetProperty("login").GetString().Should().Be(login);
            changes.RootElement.GetProperty("generatedPassword").GetBoolean().Should().BeTrue();
        }
        audit.Changes.Should().NotContain(created.TemporaryPassword!);
    }

    [Fact]
    public async Task Yaratish_ParolBilan_TemporaryNull_MustChangeYoq()
    {
        var (_, client) = await SuperAsync();
        var login = TestClients.RandomHemisId();

        var created = await OkAsync<CreatedAdminDto>(
            await client.PostJsonAsync(Url, new { fullName = "Parolli Admin", login, password = "Juda-Kuchli-1" }),
            HttpStatusCode.Created);
        created.TemporaryPassword.Should().BeNull();
        created.PhoneNumber.Should().BeNull();

        var auth = await OkAsync<AuthResultDto>(
            await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId = login, password = "Juda-Kuchli-1" }));
        auth.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Yaratish_Validatsiya400_PascalCase_LoginBand409()
    {
        var (_, client) = await SuperAsync();

        var invalid = await client.PostJsonAsync(Url, new { fullName = "", login = "12ab", phoneNumber = "123", password = "qisqa" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await invalid.ProblemAsync();
        problem.FirstError("FullName").Should().Be("FISH ni kiriting.");
        problem.FirstError("Login").Should().NotBeNullOrEmpty();
        problem.FirstError("PhoneNumber").Should().NotBeNullOrEmpty();
        problem.FirstError("Password").Should().NotBeNullOrEmpty();

        var existing = await Factory.CreateAdminAsync();
        var taken = await client.PostJsonAsync(Url, new { fullName = "Takror", login = existing.HemisId });
        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await taken.Content.ReadAsStringAsync()).Should().Contain("Bu login allaqachon band.");

        var phoneTaken = await client.PostJsonAsync(
            Url, new { fullName = "Telefon Takror", login = TestClients.RandomHemisId(), phoneNumber = existing.PhoneNumber });
        phoneTaken.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tahrirlash_200_Topilmasa404_TalabaHam404()
    {
        var (_, client) = await SuperAsync();
        var target = await Factory.CreateAdminAsync("Eski Ism");

        var updated = await OkAsync<AdminAccountDto>(
            await client.PutAsJsonAsync($"{Url}/{target.Id}", new { fullName = "Yangi Ism", phoneNumber = (string?)null }, JsonDefaults.Options));
        updated.FullName.Should().Be("Yangi Ism");
        updated.PhoneNumber.Should().BeNull();
        updated.Login.Should().Be(target.HemisId, "login bu yerda o'zgarmaydi");

        (await client.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", new { fullName = "X Y" }, JsonDefaults.Options))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var student = await Factory.CreateStudentAsync();
        (await client.PutAsJsonAsync($"{Url}/{student.Id}", new { fullName = "X Y" }, JsonDefaults.Options))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == AuditAction.AdminUpdated && a.EntityId == target.Id.ToString())))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Bloklash_TokenlarBekor_SessiyaYopiladi_QaytaFaollashtirish()
    {
        var (_, client) = await SuperAsync();
        var target = await Factory.CreateAdminAsync();
        var (targetClient, targetAuth) = await Factory.LoginWithResultAsync(target);

        var blocked = await OkAsync<AdminAccountDto>(
            await client.PatchAsJsonAsync($"{Url}/{target.Id}/status", new { isActive = false }, JsonDefaults.Options));
        blocked.IsActive.Should().BeFalse();

        (await targetClient.GetAsync("/api/admin/nav")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "access token stamp orqali rad etiladi");
        (await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { refreshToken = targetAuth.RefreshToken }))
            .IsSuccessStatusCode.Should().BeFalse("refresh tokenlar bekor qilingan");
        (await Factory.WithDbAsync(db => db.RefreshTokens.AnyAsync(t => t.UserId == target.Id && t.RevokedAt == null)))
            .Should().BeFalse();

        var audit = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == AuditAction.AdminStatusChanged && a.EntityId == target.Id.ToString()));
        using (var changes = JsonDocument.Parse(audit.Changes!))
            changes.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var active = await OkAsync<AdminAccountDto>(
            await client.PatchAsJsonAsync($"{Url}/{target.Id}/status", new { isActive = true }, JsonDefaults.Options));
        active.IsActive.Should().BeTrue();
        (await Factory.LoginAsync(target)).Should().NotBeNull("qayta kira oladi");
    }

    [Fact]
    public async Task Invariant_BoshAdminVaOziniBloklabBolmaydi_409()
    {
        var (super, client) = await SuperAsync();
        var otherSuper = await Factory.CreateSuperAdminAsync();

        var self = await client.PatchAsJsonAsync($"{Url}/{super.Id}/status", new { isActive = false }, JsonDefaults.Options);
        self.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await self.Content.ReadAsStringAsync()).Should().Contain(User.SelfBlockMessage);

        var superBlock = await client.PatchAsJsonAsync($"{Url}/{otherSuper.Id}/status", new { isActive = false }, JsonDefaults.Options);
        superBlock.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await superBlock.Content.ReadAsStringAsync()).Should().Contain(User.SuperAdminBlockMessage);

        (await Factory.WithDbAsync(db => db.Users.CountAsync(u => (u.Id == super.Id || u.Id == otherSuper.Id) && u.IsActive)))
            .Should().Be(2);
        (await Factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive && u.IsSuperAdmin)))
            .Should().BeTrue("kamida bitta faol bosh admin");
    }

    [Fact]
    public async Task ParolTiklash_VaqtinchalikParol_EskiParolIshlamaydi_TokenlarBekor()
    {
        var (_, client) = await SuperAsync();
        var target = await Factory.CreateAdminAsync();
        var (_, oldAuth) = await Factory.LoginWithResultAsync(target);

        var reset = await OkAsync<AdminPasswordResetDto>(await client.PostAsync($"{Url}/{target.Id}/password", null));
        reset.TemporaryPassword.Should().HaveLength(12);

        var anonymous = Factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new { hemisId = target.HemisId, password = target.Password }))
            .IsSuccessStatusCode.Should().BeFalse();
        var auth = await OkAsync<AuthResultDto>(
            await anonymous.PostJsonAsync("/api/auth/login", new { hemisId = target.HemisId, password = reset.TemporaryPassword }));
        auth.MustChangePassword.Should().BeTrue();
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { refreshToken = oldAuth.RefreshToken }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == AuditAction.AdminPasswordReset && a.EntityId == target.Id.ToString())))
            .Should().BeTrue();

        (await client.PostAsync($"{Url}/{Guid.NewGuid()}/password", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OddiyAdmin_403_Talaba_403_Anonim_401()
    {
        var target = await Factory.CreateAdminAsync();
        var plain = await Factory.LoginAsAdminAsync();
        var student = await Factory.LoginAsNewStudentAsync();

        foreach (var client in new[] { plain, student })
        {
            (await client.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostJsonAsync(Url, new { fullName = "X Y", login = TestClients.RandomHemisId() }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PutAsJsonAsync($"{Url}/{target.Id}", new { fullName = "X Y" }, JsonDefaults.Options))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PatchAsJsonAsync($"{Url}/{target.Id}/status", new { isActive = false }, JsonDefaults.Options))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PostAsync($"{Url}/{target.Id}/password", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await Factory.CreateClient().GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Claim eskirgan bo'lsa ham (token bosh admin bo'lgan paytda olingan) — belgi olib tashlansa stamp o'zgaradi va
    /// token rad etiladi; yangi token claim'siz → 403.</summary>
    [Fact]
    public async Task BoshAdminBelgisiOlibTashlansa_EskiToken401_YangiToken403()
    {
        var (super, client) = await SuperAsync();
        (await client.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == super.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsSuperAdmin, false)));
        // ExecuteUpdate interceptor'ni chetlab o'tadi — sessiya keshi qo'lda tozalanadi (prod'da interceptor tozalaydi).
        Factory.Services.GetRequiredService<IMemoryCache>().Remove(UserSessionValidator.CacheKey(super.Id));

        (await client.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var fresh = await Factory.LoginAsync(super);
        (await fresh.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Auth_IsSuperAdmin_Login_Refresh_Me()
    {
        var super = await Factory.CreateSuperAdminAsync();
        var (client, auth) = await Factory.LoginWithResultAsync(super);
        auth.User.IsSuperAdmin.Should().BeTrue();

        var me = await client.GetStringAsync("/api/auth/me");
        me.Should().Contain("\"isSuperAdmin\":true");

        var refreshed = await OkAsync<AuthResultDto>(
            await Factory.CreateClient().PostJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken }));
        refreshed.User.IsSuperAdmin.Should().BeTrue();

        var (_, plainAuth) = await Factory.LoginWithResultAsync(await Factory.CreateAdminAsync());
        plainAuth.User.IsSuperAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAdmin_BoshAdmin()
    {
        var seed = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.HemisId == ApiFactory.SeedAdminHemisId));
        seed.IsSuperAdmin.Should().BeTrue("DbSeeder birinchi adminni bosh admin qilib yaratadi");
    }

    /// <summary>DbSeeder (migratsiyadagi qoida bilan bir xil): faol bosh admin yo'q bo'lsa — eng eski faol admin bosh admin
    /// qilinadi; bor bo'lsa o'zgarmaydi. Umumiy baza — oldingi holat oxirida tiklanadi.</summary>
    [Fact]
    public async Task Seed_BoshAdminYoq_EngEskiFaolAdmin_BoshAdmin_Idempotent()
    {
        await Factory.CreateAdminAsync();
        var before = await Factory.WithDbAsync(db => db.Users.Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync());
        try
        {
            await Factory.WithDbAsync(db => db.Users.ExecuteUpdateAsync(u => u.SetProperty(x => x.IsSuperAdmin, false)));
            var oldest = await Factory.WithDbAsync(db => db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin && u.IsActive)
                .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
                .Select(u => u.Id).FirstAsync());

            for (var i = 0; i < 2; i++)
            {
                using var scope = Factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
            }

            (await Factory.WithDbAsync(db => db.Users.Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync()))
                .Should().Equal(oldest);
        }
        finally
        {
            await Factory.WithDbAsync(async db =>
            {
                await db.Users.ExecuteUpdateAsync(u => u.SetProperty(x => x.IsSuperAdmin, false));
                await db.Users.Where(u => before.Contains(u.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsSuperAdmin, true));
            });
        }
    }
}
