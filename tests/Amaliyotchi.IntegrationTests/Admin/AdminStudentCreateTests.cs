using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>POST /api/admin/students</c> — bitta talabani forma orqali qo'shish (Excel import bilan bir xil qoidalar)
/// va <c>GET /api/admin/students/group-options</c> — formadagi guruh variantlari.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentCreateTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/students";
    private const string OptionsUrl = "/api/admin/students/group-options";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Yaratish_201_Location_RoyxatQatori_Audit_KeyinTelegramBilanBoglanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var hemisId = TestClients.RandomHemisId();
        var phone = TestClients.RandomPhone();
        var client = await Factory.LoginAsAdminAsync();

        // Telefon import'dagidek normallashadi: 9 xonali → +998…
        var response = await client.PostJsonAsync(Url, new
        {
            fullName = "  Forma Talaba  ",
            hemisId = $" {hemisId} ",
            groupId = group.GroupId,
            phoneNumber = phone["+998".Length..]
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var row = (await response.Content.ReadAsync<StudentRow>())!;
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/admin/students/{row.Id}");

        row.FullName.Should().Be("Forma Talaba");
        row.HemisId.Should().Be(hemisId);
        row.GroupId.Should().Be(group.GroupId);
        row.Group.Should().Be(group.GroupName);
        row.Course.Should().Be(group.Course);
        row.Company.Should().BeNull();
        row.TelegramLinked.Should().BeFalse();
        row.Status.Should().Be(AdminStudentStatus.Unlinked);

        // Javob shakli ro'yxat elementi bilan bir xil.
        var listed = await client.GetPagedAsync<StudentRow>($"{Url}?q={hemisId}");
        listed.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(row);

        var saved = await Factory.WithDbAsync(db => db.StudentProfiles.Include(p => p.User)
            .SingleAsync(p => p.UserId == row.Id));
        saved.Status.Should().Be(StudentStatus.Active);
        saved.User.Role.Should().Be(UserRole.Student);
        saved.User.FacultyId.Should().Be(group.FacultyId);
        saved.User.PhoneNumber.Should().Be(phone);
        saved.User.TelegramUserId.Should().BeNull();

        var audited = await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a =>
            a.Action == AuditAction.StudentCreated && a.EntityId == row.Id.ToString()));
        audited.Should().BeTrue();

        // Import qilingan talaba kabi: admin vaqtinchalik parol beradi → talaba Mini App'da HEMIS ID + parol bilan bog'lanadi.
        (await client.PostJsonAsync($"{Url}/{row.Id}/password", new { password = "Vaqtinchalik-1" }))
            .EnsureSuccessStatusCode();
        var telegramId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        var initData = TelegramInitDataFactory.Create(telegramId, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow);
        var link = await Factory.CreateClient().PostJsonAsync("/api/auth/telegram/link",
            new { initData, hemisId, password = "Vaqtinchalik-1" });
        link.StatusCode.Should().Be(HttpStatusCode.OK, await link.Content.ReadAsStringAsync());
        (await link.Content.ReadAsync<AuthResultDto>())!.User.Id.Should().Be(row.Id);
    }

    [Fact]
    public async Task Yaratish_TelefonsizHamBoladi()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, new
        {
            fullName = "Telefonsiz Talaba",
            hemisId = TestClients.RandomHemisId(),
            groupId = group.GroupId,
            phoneNumber = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Validatsiya_400_CamelCaseKalitlar()
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, new
        {
            fullName = "   ",
            hemisId = "abc12",
            groupId = (Guid?)null,
            phoneNumber = "123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await Errors(response);
        errors.GetProperty("fullName")[0].GetString().Should().Be(StudentImportMessages.FullNameRequiredMessage);
        errors.GetProperty("hemisId")[0].GetString().Should().Be(StudentImportMessages.HemisFormatMessage);
        errors.GetProperty("groupId")[0].GetString().Should().Be(StudentImportMessages.GroupRequiredMessage);
        errors.GetProperty("phoneNumber")[0].GetString().Should().Be(StudentImportMessages.PhoneFormatMessage);
    }

    [Fact]
    public async Task Validatsiya_UzunFish_BoshHemis_400()
    {
        var group = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, new
        {
            fullName = new string('A', 201),
            hemisId = "",
            groupId = group.GroupId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await Errors(response);
        errors.GetProperty("fullName")[0].GetString().Should().Be(StudentImportMessages.FullNameLengthMessage);
        errors.GetProperty("hemisId")[0].GetString().Should().Be(StudentImportMessages.HemisRequiredMessage);
    }

    [Fact]
    public async Task GuruhYoqYokiNofaol_400_ErrorsGroupId()
    {
        var group = await Factory.CreateGroupAsync();
        await Factory.WithDbAsync(async db =>
        {
            var entity = await db.StudentGroups.FirstAsync(g => g.Id == group.GroupId);
            entity.Deactivate();
            await db.SaveChangesAsync();
        });
        var client = await Factory.LoginAsAdminAsync();

        foreach (var groupId in new[] { Guid.NewGuid(), group.GroupId })
        {
            var response = await client.PostJsonAsync(Url, new
            {
                fullName = "Guruhsiz Talaba",
                hemisId = TestClients.RandomHemisId(),
                groupId
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errors = await Errors(response);
            errors.GetProperty("groupId")[0].GetString().Should().Be(StudentCreateMessages.GroupNotFoundMessage);
        }
    }

    [Fact]
    public async Task HemisIdBand_409_TelefonBand_409()
    {
        var group = await Factory.CreateGroupAsync();
        var existing = await Factory.CreateStudentAsync(group: group);
        var existingHemis = await Factory.WithDbAsync(db => db.StudentProfiles
            .Where(p => p.UserId == existing.Id).Select(p => p.HemisId).FirstAsync());
        var client = await Factory.LoginAsAdminAsync();

        var hemisTaken = await client.PostJsonAsync(Url, new
        {
            fullName = "Takror Talaba",
            hemisId = existingHemis,
            groupId = group.GroupId
        });
        hemisTaken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(hemisTaken)).Should().Be(StudentCreateMessages.HemisTakenMessage);

        var phoneTaken = await client.PostJsonAsync(Url, new
        {
            fullName = "Takror Telefon",
            hemisId = TestClients.RandomHemisId(),
            groupId = group.GroupId,
            phoneNumber = existing.PhoneNumber
        });
        phoneTaken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(phoneTaken)).Should().Be(StudentImportMessages.PhoneTakenMessage);
    }

    [Fact]
    public async Task OchirilganTalabaningHemisIdsi_QaytaIshlatiladi_ImportdagiDek()
    {
        var group = await Factory.CreateGroupAsync();
        var existing = await Factory.CreateStudentAsync(group: group);
        var hemisId = await Factory.WithDbAsync(async db =>
        {
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == existing.Id);
            profile.IsDeleted = true;
            profile.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return profile.HemisId;
        });
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Url, new
        {
            fullName = "Qayta Talaba",
            hemisId,
            groupId = group.GroupId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.PostJsonAsync(Url, new { fullName = "X", hemisId = "123456", groupId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync(OptionsUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GuruhVariantlari_FaqatFaol_Filtrlar_Tartib()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var g3 = await Factory.CreateGroupAsync(facultyId, course: 3);
        var g2 = await Factory.CreateGroupAsync(facultyId, course: 2);
        var inactive = await Factory.CreateGroupAsync(facultyId, course: 2);
        await Factory.WithDbAsync(async db =>
        {
            var entity = await db.StudentGroups.FirstAsync(g => g.Id == inactive.GroupId);
            entity.Deactivate();
            await db.SaveChangesAsync();
        });
        var other = await Factory.CreateGroupAsync();
        var client = await Factory.LoginAsAdminAsync();

        var byFaculty = await Options(client, $"?facultyId={facultyId}");
        byFaculty.Select(o => o.Id).Should().BeEquivalentTo([g3.GroupId, g2.GroupId]);
        byFaculty.Select(o => o.Id).Should().NotContain([inactive.GroupId, other.GroupId]);
        var expected = byFaculty
            .OrderBy(o => o.FacultyName, StringComparer.Ordinal).ThenBy(o => o.DirectionName, StringComparer.Ordinal)
            .ThenBy(o => o.Course).ThenBy(o => o.Name, StringComparer.Ordinal).ToList();
        byFaculty.Should().Equal(expected);

        var first = byFaculty.Single(o => o.Id == g3.GroupId);
        first.Name.Should().Be(g3.GroupName);
        first.Course.Should().Be(3);
        first.DirectionName.Should().NotBeNullOrEmpty();
        first.FacultyName.Should().NotBeNullOrEmpty();

        (await Options(client, $"?facultyId={facultyId}&course=2")).Select(o => o.Id).Should().Equal(g2.GroupId);
        (await Options(client, $"?directionId={g3.DirectionId}")).Select(o => o.Id).Should().Equal(g3.GroupId);
        (await Options(client, $"?directionId={g3.DirectionId}&course=2")).Should().BeEmpty();
        (await Options(client, "")).Select(o => o.Id).Should().Contain([g3.GroupId, g2.GroupId, other.GroupId])
            .And.NotContain(inactive.GroupId);
    }

    /* ── yordamchilar ────────────────────────────────────────────────────── */

    private static async Task<List<StudentGroupOption>> Options(HttpClient client, string query)
    {
        var response = await client.GetAsync(OptionsUrl + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<List<StudentGroupOption>>())!;
    }

    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Clone();
    }

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }
}
