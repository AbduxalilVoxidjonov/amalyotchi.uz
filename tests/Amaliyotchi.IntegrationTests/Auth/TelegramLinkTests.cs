using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Auth;

/// <summary><c>POST /api/auth/telegram/link</c> — talaba Mini App'da initData + HEMIS ID + parol bilan Telegram
/// hisobini bog'laydi va kiradi.</summary>
[Collection(ApiCollection.Name)]
public sealed class TelegramLinkTests(ApiFixture fixture)
{
    private const string LinkUrl = "/api/auth/telegram/link";
    private const string TempPassword = "Vaqtinchalik-1";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Bogladi_200_MustChangePassword_KeyinTelegramLogin200()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false);
        var tgId = NewTelegramId();

        var response = await PostLinkAsync(InitData(tgId), student.HemisId, TempPassword);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var auth = (await response.Content.ReadAsync<AuthResultDto>())!;
        auth.User.Id.Should().Be(student.Id);
        auth.User.Role.Should().Be(UserRole.Student);
        auth.User.HemisId.Should().Be(student.HemisId);
        auth.User.PhoneNumber.Should().Be(student.Phone, "initData'da telefon yo'q — mavjud telefon saqlanadi");
        auth.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.MustChangePassword.Should().BeTrue("parolni xodim o'rnatgan");
        using (var json = JsonDocument.Parse(body))
            json.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();

        var linked = await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == student.Id)
            .Select(u => u.TelegramUserId).SingleAsync());
        linked.Should().Be(tgId);

        var audited = await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.TelegramLinked && a.EntityId == student.Id.ToString()));
        audited.Should().BeTrue();

        // Endi oddiy Telegram login shu initData bilan ishlaydi.
        var (_, tgAuth) = await Factory.TelegramLoginAsync(tgId);
        tgAuth.User.Id.Should().Be(student.Id);
        tgAuth.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task QaytaBoglash_ShuId_Bilan_200_Idempotent()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false);
        var tgId = NewTelegramId();

        (await PostLinkAsync(InitData(tgId), student.HemisId, TempPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await PostLinkAsync(InitData(tgId), student.HemisId, TempPassword);

        again.StatusCode.Should().Be(HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
        (await again.Content.ReadAsync<AuthResultDto>())!.User.Id.Should().Be(student.Id);

        var linkAudits = await Factory.WithDbAsync(db => db.AuditLogs.CountAsync(a =>
            a.Action == AuditAction.TelegramLinked && a.EntityId == student.Id.ToString()));
        linkAudits.Should().Be(1, "ikkinchi so'rov — oddiy kirish");
    }

    [Fact]
    public async Task NotogriParol_403_VaBoglanmaydi_VaAudit()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false);
        var tgId = NewTelegramId();

        var response = await PostLinkAsync(InitData(tgId), student.HemisId, "Notogri-Parol");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(response)).Should().Be("HEMIS ID yoki parol noto'g'ri.");

        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id));
        user.TelegramUserId.Should().BeNull();
        var failed = await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.LoginFailed && a.EntityId == student.Id.ToString()));
        failed.Should().BeTrue();
    }

    [Fact]
    public async Task NomalumHemisId_403_UmumiyXabar()
    {
        var response = await PostLinkAsync(InitData(NewTelegramId()), TestClients.RandomHemisId(), TempPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(response)).Should().Be("HEMIS ID yoki parol noto'g'ri.");
    }

    [Fact]
    public async Task YaroqsizImzo_403()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false);
        var tgId = NewTelegramId();
        var initData = TelegramInitDataFactory.Create(
            tgId, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow, hashOverride: new string('0', 64));

        var response = await PostLinkAsync(initData, student.HemisId, TempPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(response)).Should().Be("Telegram imzosi tasdiqlanmadi. Ilovani qaytadan oching.");
        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id));
        user.TelegramUserId.Should().BeNull();
    }

    [Fact]
    public async Task FaolBolmaganTalaba_403()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false, active: false);

        var response = await PostLinkAsync(InitData(NewTelegramId()), student.HemisId, TempPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(response)).Should().Be("Hisobingiz faol emas. Tyutoringizga murojaat qiling.");
    }

    [Fact]
    public async Task XodimHisobi_403()
    {
        var tutor = await Factory.CreateTutorAsync();

        var response = await PostLinkAsync(InitData(NewTelegramId()), tutor.HemisId, tutor.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Detail(response)).Should().Be("Telegram faqat talaba hisobiga bog'lanadi.");
        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == tutor.Id));
        user.TelegramUserId.Should().BeNull();
    }

    [Fact]
    public async Task TelegramIdBoshqaHisobgaBoglangan_409()
    {
        var other = await Factory.CreateStudentAsync();
        var student = await CreateStudentWithPasswordAsync(linkTelegram: false);

        var response = await PostLinkAsync(InitData(other.TelegramId!.Value), student.HemisId, TempPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be("Bu Telegram akkaunti boshqa hisobga bog'langan.");
        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id));
        user.TelegramUserId.Should().BeNull();
    }

    [Fact]
    public async Task TalabaBoshqaTelegramgaBoglangan_409()
    {
        var student = await CreateStudentWithPasswordAsync(linkTelegram: true);

        var response = await PostLinkAsync(InitData(NewTelegramId()), student.HemisId, TempPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be("Bu hisobga boshqa Telegram akkaunti bog'langan.");
        var user = await Factory.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == student.Id));
        user.TelegramUserId.Should().Be(student.TelegramId);
    }

    [Fact]
    public async Task BoshMaydonlar_400_Errors()
    {
        var response = await Factory.CreateClient().PostJsonAsync(LinkUrl, new { initData = "", hemisId = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("InitData", out _).Should().BeTrue();
        errors.TryGetProperty("HemisId", out _).Should().BeTrue();
        errors.TryGetProperty("Password", out _).Should().BeTrue();
    }

    // ---------- yordamchilar ----------

    private sealed record LinkStudent(Guid Id, string HemisId, string Phone, long? TelegramId);

    /// <summary>Talaba yaratadi va admin API orqali unga <see cref="TempPassword"/> o'rnatadi
    /// (<c>mustChangePassword = true</c>).</summary>
    private async Task<LinkStudent> CreateStudentWithPasswordAsync(bool linkTelegram, bool active = true)
    {
        var student = await Factory.CreateStudentAsync(linkTelegram: linkTelegram);
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

        var hemisId = await Factory.WithDbAsync(db =>
            db.StudentProfiles.Where(p => p.UserId == student.Id).Select(p => p.HemisId).SingleAsync());
        return new LinkStudent(student.Id, hemisId, student.PhoneNumber, student.TelegramId);
    }

    private Task<HttpResponseMessage> PostLinkAsync(string initData, string hemisId, string password) =>
        Factory.CreateClient().PostJsonAsync(LinkUrl, new { initData, hemisId, password });

    private static string InitData(long telegramId) =>
        TelegramInitDataFactory.Create(telegramId, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow);

    private static long NewTelegramId() => Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }
}
