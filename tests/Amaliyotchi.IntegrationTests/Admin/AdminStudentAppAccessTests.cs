using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>v3.34 — "admin talabani kiritdi → talaba ilovadan darhol kiradi": yaratishda vaqtinchalik parol,
/// importda parollar yaratish, profilda <c>mustChangePassword</c>/<c>lastLoginAt</c>.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentAppAccessTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/students";
    private const string TempPassword = "Vaqtinchalik-1";

    private ApiFactory Factory => fixture.Factory;

    private Task<HttpResponseMessage> LoginAsync(string hemisId, string password)
        => Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password });

    private static async Task<JsonElement> DetailAsync(HttpClient admin, Guid id)
    {
        var response = await admin.GetAsync($"{Url}/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task Yaratish_ParolBilan_TalabaDarholKiradi_ProfildaHolat()
    {
        var group = await Factory.CreateGroupAsync();
        var hemisId = TestClients.RandomHemisId();
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.PostJsonAsync(Url, new
        {
            fullName = "Ilova Talaba",
            hemisId,
            groupId = group.GroupId,
            password = TempPassword
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var row = (await response.Content.ReadAsync<StudentRow>())!;

        var before = await DetailAsync(admin, row.Id);
        before.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
        before.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();
        before.GetProperty("lastLoginAt").ValueKind.Should().Be(JsonValueKind.Null);

        var login = await LoginAsync(hemisId, TempPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var auth = (await login.Content.ReadAsync<AuthResultDto>())!;
        auth.User.Id.Should().Be(row.Id);
        auth.MustChangePassword.Should().BeTrue();

        var after = await DetailAsync(admin, row.Id);
        after.GetProperty("lastLoginAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Talaba parolini almashtirgach — vaqtinchalik emas.
        var student = Factory.CreateClient();
        student.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        (await student.PostJsonAsync("/api/auth/change-password",
                new { currentPassword = TempPassword, newPassword = "Yangi-Parol-77" }))
            .IsSuccessStatusCode.Should().BeTrue();
        (await DetailAsync(admin, row.Id)).GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();

        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.StudentPasswordSet && a.EntityId == row.Id.ToString()))).Should().BeTrue();
    }

    [Fact]
    public async Task Yaratish_ParolsizEskiXulq_QisqaParol400()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var shortPassword = await admin.PostJsonAsync(Url, new
        {
            fullName = "Qisqa Parol",
            hemisId = TestClients.RandomHemisId(),
            groupId = group.GroupId,
            password = "abc"
        });
        shortPassword.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await shortPassword.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("errors").GetProperty("password")[0].GetString().Should().Contain("8");

        var hemisId = TestClients.RandomHemisId();
        var created = await admin.PostJsonAsync(Url, new { fullName = "Parolsiz", hemisId, groupId = group.GroupId });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var row = (await created.Content.ReadAsync<StudentRow>())!;
        var detail = await DetailAsync(admin, row.Id);
        detail.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        detail.GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Import_ParollarYaratiladi_TalabaKiradi_SuzsizEskiXulq()
    {
        var group = await Factory.CreateGroupAsync();
        var first = TestClients.RandomHemisId();
        var second = TestClients.RandomHemisId();
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.PostAsync($"{Url}/import", ImportForm(Workbook(group.GroupName,
            ("Import Parol Bir", first), ("Import Parol Ikki", second), ("Xato", "abc")), generatePasswords: "true"));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var result = (await response.Content.ReadAsync<StudentImportResult>())!;
        result.Created.Should().Be(2);
        result.Failed.Should().Be(1);
        result.Credentials.Should().HaveCount(2);
        var cred = result.Credentials.Single(c => c.HemisId == first);
        cred.Row.Should().Be(2);
        cred.FullName.Should().Be("Import Parol Bir");
        cred.Password.Should().HaveLength(TemporaryPasswords.Length);
        result.Credentials.Select(c => c.Password).Should().OnlyHaveUniqueItems();

        var login = await LoginAsync(first, cred.Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        (await login.Content.ReadAsync<AuthResultDto>())!.MustChangePassword.Should().BeTrue();

        // Bayroqsiz — eski xulq: parol yo'q, credentials bo'sh.
        var plain = TestClients.RandomHemisId();
        var noPasswords = await admin.PostAsync($"{Url}/import", ImportForm(Workbook(group.GroupName, ("Parolsiz Import", plain))));
        noPasswords.StatusCode.Should().Be(HttpStatusCode.OK);
        var plainResult = (await noPasswords.Content.ReadAsync<StudentImportResult>())!;
        plainResult.Created.Should().Be(1);
        plainResult.Credentials.Should().BeEmpty();
        (await Factory.WithDbAsync(db => db.StudentProfiles.Where(p => p.HemisId == plain)
            .Select(p => p.User.PasswordHash).SingleAsync())).Should().BeNull();
    }

    [Fact]
    public async Task HemisId_Ozgartirish_204_Band409_Format400_KeyinYangiLoginBilanKiradi()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var oldHemis = TestClients.RandomHemisId();
        var created = await admin.PostJsonAsync(Url, new
        {
            fullName = "Hemis Almashtir", hemisId = oldHemis, groupId = group.GroupId, password = TempPassword
        });
        var row = (await created.Content.ReadAsync<StudentRow>())!;
        var other = await Factory.CreateStudentAsync(group: group);
        var otherHemis = await Factory.WithDbAsync(db => db.StudentProfiles
            .Where(p => p.UserId == other.Id).Select(p => p.HemisId).SingleAsync());

        var bad = await admin.PutAsJsonAsync($"{Url}/{row.Id}/hemis-id", new { hemisId = "12ab" });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("errors").GetProperty("hemisId")[0].GetString()
                .Should().Be(StudentImportMessages.HemisFormatMessage);

        (await admin.PutAsJsonAsync($"{Url}/{row.Id}/hemis-id", new { hemisId = otherHemis }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync($"{Url}/{Guid.NewGuid()}/hemis-id", new { hemisId = TestClients.RandomHemisId() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var newHemis = TestClients.RandomHemisId();
        (await admin.PutAsJsonAsync($"{Url}/{row.Id}/hemis-id", new { hemisId = $" {newHemis} " }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailAsync(admin, row.Id)).GetProperty("hemisId").GetString().Should().Be(newHemis);

        (await LoginAsync(oldHemis, TempPassword)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LoginAsync(newHemis, TempPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.StudentHemisIdChanged && a.EntityId == row.Id.ToString()))).Should().BeTrue();
    }

    [Fact]
    public async Task Royxat_HasPassword_Maydon_VaFiltr()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var withHemis = TestClients.RandomHemisId();
        var withoutHemis = TestClients.RandomHemisId();
        (await admin.PostJsonAsync(Url, new { fullName = "Parolli", hemisId = withHemis, groupId = group.GroupId, password = TempPassword }))
            .EnsureSuccessStatusCode();
        (await admin.PostJsonAsync(Url, new { fullName = "Parolsiz", hemisId = withoutHemis, groupId = group.GroupId }))
            .EnsureSuccessStatusCode();

        (await admin.GetPagedAsync<StudentRow>($"{Url}?q={withHemis}")).Items.Single().HasPassword.Should().BeTrue();
        (await admin.GetPagedAsync<StudentRow>($"{Url}?q={withoutHemis}")).Items.Single().HasPassword.Should().BeFalse();

        var noPassword = await admin.GetPagedAsync<StudentRow>($"{Url}?hasPassword=false&pageSize=500");
        noPassword.Items.Should().OnlyContain(r => !r.HasPassword).And.Contain(r => r.HemisId == withoutHemis);
        (await admin.GetPagedAsync<StudentRow>($"{Url}?hasPassword=true&q={withoutHemis}")).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Yaratish_AdminLoginiBilanBirXilHemis_409()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var adminLogin = await Factory.WithDbAsync(db => db.Users
            .Where(u => u.HemisId != null).Select(u => u.HemisId!).FirstAsync());

        (await admin.PostJsonAsync(Url, new { fullName = "Admin Logini", hemisId = adminLogin, groupId = group.GroupId }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static byte[] Workbook(string group, params (string FullName, string HemisId)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Talabalar");
        sheet.Cell(1, 1).Value = $"{StudentImportColumns.FullName} *";
        sheet.Cell(1, 2).Value = $"{StudentImportColumns.HemisId} *";
        sheet.Cell(1, 3).Value = $"{StudentImportColumns.Group} *";
        sheet.Cell(1, 4).Value = StudentImportColumns.Phone;
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].FullName;
            sheet.Cell(i + 2, 2).Value = rows[i].HemisId;
            sheet.Cell(i + 2, 3).Value = group;
        }

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    private static MultipartFormDataContent ImportForm(byte[] content, string? generatePasswords = null)
    {
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue(ExcelImport.ContentType);
        var form = new MultipartFormDataContent { { part, "file", "talabalar.xlsx" } };
        if (generatePasswords is not null)
            form.Add(new StringContent(generatePasswords), "generatePasswords");
        return form;
    }
}
