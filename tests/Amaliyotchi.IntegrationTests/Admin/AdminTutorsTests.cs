using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminTutorsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Tyutorlar_200_Shakl_KechikkanAriza_Late()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateTutorAsync(groupA, "Kechikkan Tyutor", groupB.GroupId);
        var period = await Factory.CreateActivePeriodAsync(groupA, tutor.Id, groupB.GroupId);
        var company = await Factory.CreateCompanyAsync();
        var s1 = await Factory.CreateStudentAsync(group: groupA);
        var s2 = await Factory.CreateStudentAsync(group: groupB);
        await Factory.CreatePendingApplicationAsync(s1, period, company, TimeSpan.FromHours(50));
        await Factory.CreatePendingApplicationAsync(s2, period, company, TimeSpan.FromHours(1));
        await Factory.LoginAsync(tutor); // LastLoginAt

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/tutors?q={tutor.PhoneNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var item = json.RootElement.GetProperty("items")[0];
            item.GetProperty("status").GetString().Should().Be("late");
            item.GetProperty("phone").GetString().Should().Be(tutor.PhoneNumber, "E.164 xom");
            item.GetProperty("groups").ValueKind.Should().Be(JsonValueKind.Array);
        }

        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.PhoneNumber}");
        page.Total.Should().Be(1);
        var row = page.Items.Single();
        row.Id.Should().Be(tutor.Id);
        row.FullName.Should().Be("Kechikkan Tyutor");
        row.FacultyId.Should().Be(groupA.FacultyId);
        row.FacultyCode.Should().NotBeNullOrEmpty();
        row.Groups.Should().BeEquivalentTo([groupA.GroupName, groupB.GroupName]);
        row.Students.Should().Be(2);
        row.Pending.Should().Be(2);
        row.OldestPendingAt.Should().NotBeNull();
        row.LastActiveAt.Should().NotBeNull();
        row.Status.Should().Be(TutorStatus.Late);
    }

    [Fact]
    public async Task Tyutorlar_YangiAriza_Active_VaIsmBoyichaQidiruv()
    {
        var group = await Factory.CreateGroupAsync();
        var marker = "Faol" + Guid.NewGuid().ToString("N")[..6];
        var tutor = await Factory.CreateTutorAsync(group, marker);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreatePendingApplicationAsync(student, period, company, TimeSpan.FromHours(2));

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={marker.ToUpperInvariant()}");

        page.Total.Should().Be(1);
        page.Items.Single().Pending.Should().Be(1);
        page.Items.Single().Status.Should().Be(TutorStatus.Active);
    }

    [Fact]
    public async Task Tyutorlar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/tutors")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostJsonAsync("/api/admin/tutors", new { fullName = "X Y", hemisId = "123456789012", password = "Parol-12345", facultyId = Guid.CreateVersion7() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tyutorlar_FacultyIdFiltri()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync();
        var tutorA = await Factory.CreateTutorAsync(groupA, "Filtr Tyutor A");
        await Factory.CreateTutorAsync(groupB, "Filtr Tyutor B");

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?facultyId={groupA.FacultyId}&q=Filtr%20Tyutor");

        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(tutorA.Id);
    }

    // ---------- GET {id} ----------

    [Fact]
    public async Task Karta_200_Shakl()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group, "Karta Tyutor");
        await Factory.CreateStudentAsync(group: group);
        await Factory.CreateStudentAsync(group: group);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/tutors/{tutor.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            json.RootElement.GetProperty("hemisId").GetString().Should().Be(tutor.HemisId);
            json.RootElement.GetProperty("groups")[0].GetProperty("assignmentId").ValueKind.Should().Be(JsonValueKind.String);
        }

        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        detail.Id.Should().Be(tutor.Id);
        detail.FullName.Should().Be("Karta Tyutor");
        detail.Phone.Should().Be(tutor.PhoneNumber);
        detail.FacultyId.Should().Be(group.FacultyId);
        detail.FacultyCode.Should().NotBeNullOrEmpty();
        detail.FacultyName.Should().NotBeNullOrEmpty();
        detail.IsActive.Should().BeTrue();
        detail.LastLoginAt.Should().BeNull();
        detail.CreatedAt.Should().BeAfter(DateTimeOffset.MinValue);
        var g = detail.Groups.Should().ContainSingle().Subject;
        g.GroupId.Should().Be(group.GroupId);
        g.GroupName.Should().Be(group.GroupName);
        g.Course.Should().Be(group.Course);
        g.DirectionName.Should().NotBeNullOrEmpty();
        g.Students.Should().Be(2);
        g.AcademicYearName.Should().NotBeNullOrEmpty();
        g.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Karta_AdminYokiYoqId_404()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);

        (await client.GetAsync($"/api/admin/tutors/{admin.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "roli tyutor emas");
        var response = await client.GetAsync($"/api/admin/tutors/{Guid.CreateVersion7()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Detail(response)).Should().Be("Tyutor topilmadi.");
    }

    // ---------- POST ----------

    [Fact]
    public async Task Yaratish_201_Location_YangiTyutorKiraOladi()
    {
        var facultyId = await Factory.CreateFacultyAsync("Yaratish Tyutor Fakulteti");
        var client = await Factory.LoginAsAdminAsync();
        var hemisId = TestClients.RandomHemisId();
        var phone = TestClients.RandomPhone();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "  Yangi Tyutor  ",
            hemisId = $" {hemisId} ",
            phone = phone[4..].Insert(2, " "), // "90 1234567" — normallashadi
            password = "Yangi-Parol-1",
            facultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        response.Headers.Location!.ToString().Should().EndWith($"/api/admin/tutors/{detail.Id}");
        detail.FullName.Should().Be("Yangi Tyutor");
        detail.HemisId.Should().Be(hemisId);
        detail.Phone.Should().Be(phone);
        detail.FacultyId.Should().Be(facultyId);
        detail.IsActive.Should().BeTrue();
        detail.Groups.Should().BeEmpty();

        var login = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password = "Yangi-Parol-1" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={phone}");
        page.Items.Should().ContainSingle(t => t.Id == detail.Id);

        var audit = await Factory.WithDbAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == AuditAction.TutorCreated && a.EntityId == detail.Id.ToString()));
        audit.Should().BeTrue();
    }

    [Fact]
    public async Task Yaratish_TelefonsizHam_201()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Telefonsiz Tyutor", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<TutorDetail>())!.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Yaratish_TakroriyHemisId_409_OchirilganHamHisobga()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var existing = await Factory.CreateTutorAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Takror Tyutor", hemisId = existing.HemisId, password = "Parol-12345", facultyId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be("Bu HEMIS ID bilan foydalanuvchi mavjud.");

        // Soft-delete qilingan hisobning HEMIS ID'si ham band
        var deleted = await Factory.CreateTutorAsync();
        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == deleted.Id);
            user.IsDeleted = true;
            user.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        var again = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Takror Tyutor", hemisId = deleted.HemisId, password = "Parol-12345", facultyId
        });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_TakroriyTelefon_409()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var existing = await Factory.CreateTutorAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Telefon Takror", hemisId = TestClients.RandomHemisId(), phone = existing.PhoneNumber,
            password = "Parol-12345", facultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Yaratish_Validatsiya_400()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "A", hemisId = "12ab", phone = "12345", password = "qisqa", facultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("FullName", out _).Should().BeTrue();
        errors.TryGetProperty("HemisId", out _).Should().BeTrue();
        errors.TryGetProperty("Phone", out _).Should().BeTrue();
        errors.TryGetProperty("Password", out _).Should().BeTrue();

        var noFaculty = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "To'g'ri Ism", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyId = Guid.Empty
        });
        noFaculty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Yaratish_FakultetYoq_404_FaolEmas_409()
    {
        var client = await Factory.LoginAsAdminAsync();

        var missing = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Fakultetsiz", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyId = Guid.CreateVersion7()
        });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var facultyId = await Factory.CreateFacultyAsync("Faol Emas Fakultet");
        (await client.PatchAsJsonAsync($"/api/admin/faculties/{facultyId}/status", new { isActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var inactive = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Faol Emas", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyId
        });
        inactive.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(inactive)).Should().Be("Fakultet faol emas.");
    }

    // ---------- PUT {id} ----------

    [Fact]
    public async Task Yangilash_200_IsmTelefonOzgaradi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group, "Eski Ism");
        var client = await Factory.LoginAsAdminAsync();
        var phone = TestClients.RandomPhone();

        var response = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = "Yangi Ism", phone, facultyId = group.FacultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        detail.FullName.Should().Be("Yangi Ism");
        detail.Phone.Should().Be(phone);
        detail.FacultyId.Should().Be(group.FacultyId);
        detail.Groups.Should().ContainSingle(g => g.GroupId == group.GroupId, "fakultet o'zgarmadi — biriktiruv saqlanadi");

        var cleared = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = "Yangi Ism", phone = (string?)null, facultyId = group.FacultyId
        });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cleared.Content.ReadAsync<TutorDetail>())!.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Yangilash_FakultetOzgarsa_BiriktiruvBorsa_409()
    {
        var tutor = await Factory.CreateTutorAsync();
        var otherFacultyId = await Factory.CreateFacultyAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.PhoneNumber, facultyId = otherFacultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be("Tyutorga guruhlar biriktirilgan — avval ularni ajrating.");
    }

    [Fact]
    public async Task Yangilash_FakultetOzgarsa_BiriktiruvYoq_200()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync();
        var otherFacultyId = await Factory.CreateFacultyAsync();
        var tutor = await CreateTutorViaApiAsync(client, facultyId);

        var response = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.Phone, facultyId = otherFacultyId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<TutorDetail>())!.FacultyId.Should().Be(otherFacultyId);
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404_FakultetYoq_404()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync();

        var missing = await client.PutAsJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}", new
        {
            fullName = "Yoq", phone = (string?)null, facultyId
        });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var tutor = await CreateTutorViaApiAsync(client, facultyId);
        var badFaculty = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = (string?)null, facultyId = Guid.CreateVersion7()
        });
        badFaculty.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- PATCH {id}/status ----------

    [Fact]
    public async Task Holat_Deaktiv_RefreshVaLoginRad_QaytaFaol_LoginIshlaydi()
    {
        var tutor = await Factory.CreateTutorAsync();
        var (_, auth) = await Factory.LoginWithResultAsync(tutor);
        var admin = await Factory.LoginAsAdminAsync();
        var anonymous = Factory.CreateClient();

        var deactivate = await admin.PatchAsJsonAsync($"/api/admin/tutors/{tutor.Id}/status", new { isActive = false });
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "refresh tokenlar bekor qilindi");
        (await anonymous.PostJsonAsync("/api/auth/login", new { tutor.HemisId, tutor.Password })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "hisob faol emas");

        var detail = await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}");
        detail!.IsActive.Should().BeFalse();
        detail.Groups.Should().NotBeEmpty("biriktiruvlarga tegilmaydi");

        var activate = await admin.PatchAsJsonAsync($"/api/admin/tutors/{tutor.Id}/status", new { isActive = true });
        activate.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await anonymous.PostJsonAsync("/api/auth/login", new { tutor.HemisId, tutor.Password })).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await admin.PatchAsJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/status", new { isActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- POST {id}/password ----------

    [Fact]
    public async Task ParolTiklash_EskiParolRad_YangisiIshlaydi_RefreshBekor()
    {
        var tutor = await Factory.CreateTutorAsync();
        var (_, auth) = await Factory.LoginWithResultAsync(tutor);
        var admin = await Factory.LoginAsAdminAsync();
        var anonymous = Factory.CreateClient();

        var reset = await admin.PostJsonAsync($"/api/admin/tutors/{tutor.Id}/password", new { password = "Yangi-Parol-99" });
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await anonymous.PostJsonAsync("/api/auth/login", new { tutor.HemisId, password = tutor.Password })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "eski parol endi ishlamaydi (login rad → 403)");
        (await anonymous.PostJsonAsync("/api/auth/login", new { tutor.HemisId, password = "Yangi-Parol-99" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await anonymous.PostJsonAsync("/api/auth/refresh", new { auth.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "eski sessiya refresh tokeni bekor qilindi");

        var revoked = await Factory.WithDbAsync(db =>
            db.RefreshTokens.Where(t => t.UserId == tutor.Id && t.Token == auth.RefreshToken).Select(t => t.RevokedReason).SingleAsync());
        revoked.Should().Contain("tiklandi");
    }

    [Fact]
    public async Task ParolTiklash_Qisqa_400_Topilmasa_404()
    {
        var tutor = await Factory.CreateTutorAsync();
        var admin = await Factory.LoginAsAdminAsync();

        (await admin.PostJsonAsync($"/api/admin/tutors/{tutor.Id}/password", new { password = "1234567" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/password", new { password = "Parol-12345" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- PUT {id}/groups ----------

    [Fact]
    public async Task Guruhlar_Almashtirish_TarixSaqlanadi_QaytaFaollashadi()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateTutorAsync(groupA);
        var admin = await Factory.LoginAsAdminAsync();

        var originalAssignmentId = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!
            .Groups.Single().AssignmentId;

        // A → B: A faolsizlantiriladi (o'chirilmaydi), B yaratiladi
        var swap = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { groupB.GroupId } });
        swap.StatusCode.Should().Be(HttpStatusCode.OK, await swap.Content.ReadAsStringAsync());
        var afterSwap = (await swap.Content.ReadAsync<TutorDetail>())!;
        afterSwap.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([groupB.GroupId]);

        var records = await Factory.WithDbAsync(db =>
            db.TutorAssignments.Where(a => a.TutorUserId == tutor.Id).Select(a => new { a.Id, a.StudentGroupId, a.IsActive }).ToListAsync());
        records.Should().HaveCount(2);
        records.Single(r => r.StudentGroupId == groupA.GroupId).IsActive.Should().BeFalse();
        records.Single(r => r.StudentGroupId == groupB.GroupId).IsActive.Should().BeTrue();

        // A qayta keladi (B bilan birga) — mavjud yozuv faollashadi, yangi yozuv yaratilmaydi
        var both = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups",
            new { groupIds = new[] { groupA.GroupId, groupB.GroupId, groupA.GroupId } });
        both.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBoth = (await both.Content.ReadAsync<TutorDetail>())!;
        afterBoth.Groups.Should().HaveCount(2);
        afterBoth.Groups.Single(g => g.GroupId == groupA.GroupId).AssignmentId.Should().Be(originalAssignmentId);

        (await Factory.WithDbAsync(db => db.TutorAssignments.CountAsync(a => a.TutorUserId == tutor.Id))).Should().Be(2);

        // Bo'sh ro'yxat — hammasi ajratiladi
        var clear = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = Array.Empty<Guid>() });
        clear.StatusCode.Should().Be(HttpStatusCode.OK);
        (await clear.Content.ReadAsync<TutorDetail>())!.Groups.Should().BeEmpty();

        var page = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.PhoneNumber}");
        page.Items.Single().Groups.Should().BeEmpty("ro'yxat ham faol biriktiruvlarga tayanadi");

        var audit = await Factory.WithDbAsync(db =>
            db.AuditLogs.CountAsync(a => a.Action == AuditAction.TutorGroupsChanged && a.EntityId == tutor.Id.ToString()));
        audit.Should().Be(3);
    }

    [Fact]
    public async Task Guruhlar_FaolEmasTyutorgaHam_Ruxsat()
    {
        var group = await Factory.CreateGroupAsync();
        var other = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/tutors/{tutor.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { other.GroupId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Guruhlar_BegonaFakultetGuruhi_400()
    {
        var tutor = await Factory.CreateTutorAsync();
        var foreign = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { foreign.GroupId } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(response)).Should().Be($"Guruh tyutor fakultetiga tegishli emas: {foreign.GroupName}");
    }

    [Fact]
    public async Task Guruhlar_BoshqaTyutorGuruhi_409()
    {
        var group = await Factory.CreateGroupAsync();
        var owner = await Factory.CreateTutorAsync(group, "Egasi Tyutor");
        var other = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(other);
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { other.GroupId, group.GroupId } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be($"{group.GroupName} guruhi {owner.FullName} tyutoriga biriktirilgan.");

        // Egasi ajratgach — bo'shaydi
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{owner.Id}/groups", new { groupIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { other.GroupId, group.GroupId } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Guruhlar_FaolEmasGuruh_400_Topilmasa_404()
    {
        var group = await Factory.CreateGroupAsync();
        var inactive = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/groups/{inactive.GroupId}/status", new { isActive = false })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { inactive.GroupId } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(response)).Should().Be($"Guruh faol emas: {inactive.GroupName}");

        var missing = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { Guid.CreateVersion7() } });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var emptyId = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { Guid.Empty } });
        emptyId.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/groups", new { groupIds = Array.Empty<Guid>() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Guruhlar_FaolOquvYiliYoq_409()
    {
        var group = await Factory.CreateGroupAsync();
        var other = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var admin = await Factory.LoginAsAdminAsync();

        var activeYears = await Factory.WithDbAsync(async db =>
        {
            var years = await db.AcademicYears.Where(y => y.IsActive).ToListAsync();
            years.ForEach(y => y.Archive());
            await db.SaveChangesAsync();
            return years.Select(y => y.Id).ToList();
        });
        try
        {
            var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = new[] { other.GroupId } });
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Detail(response)).Should().Be("Faol o'quv yili yo'q.");

            // Yangi yozuv kerak bo'lmasa (faqat ajratish) — o'quv yili talab qilinmaydi
            (await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/groups", new { groupIds = Array.Empty<Guid>() }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await Factory.WithDbAsync(async db =>
            {
                var years = await db.AcademicYears.Where(y => activeYears.Contains(y.Id)).ToListAsync();
                years.ForEach(y => y.Activate());
                await db.SaveChangesAsync();
            });
        }
    }

    // ---------- GET {id}/available-groups ----------

    [Fact]
    public async Task AvailableGroups_FakultetdagiFaolGuruhlar_TutorNameTogri()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var groupC = await Factory.CreateGroupAsync(groupA.FacultyId);
        var inactive = await Factory.CreateGroupAsync(groupA.FacultyId);
        var foreign = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(groupA, "Ozi Tyutor");
        var other = await Factory.CreateTutorAsync(groupB, "Boshqa Tyutor");
        await Factory.CreateStudentAsync(group: groupC);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/groups/{inactive.GroupId}/status", new { isActive = false })).EnsureSuccessStatusCode();

        var rows = (await admin.GetFromJsonAsync<List<AvailableGroupRow>>($"/api/admin/tutors/{tutor.Id}/available-groups"))!;

        rows.Select(r => r.Id).Should().BeEquivalentTo([groupA.GroupId, groupB.GroupId, groupC.GroupId]);
        rows.Select(r => r.Id).Should().NotContain([inactive.GroupId, foreign.GroupId]);

        var a = rows.Single(r => r.Id == groupA.GroupId);
        a.TutorId.Should().Be(tutor.Id);
        a.TutorName.Should().Be("Ozi Tyutor", "shu tyutorning o'zi bo'lsa ham to'ldiriladi");
        a.Name.Should().Be(groupA.GroupName);
        a.Course.Should().Be(groupA.Course);
        a.DirectionName.Should().NotBeNullOrEmpty();
        a.DepartmentName.Should().NotBeNullOrEmpty();

        var b = rows.Single(r => r.Id == groupB.GroupId);
        b.TutorId.Should().Be(other.Id);
        b.TutorName.Should().Be("Boshqa Tyutor");

        var c = rows.Single(r => r.Id == groupC.GroupId);
        c.TutorId.Should().BeNull();
        c.TutorName.Should().BeNull();
        c.Students.Should().Be(1);

        rows.Should().BeInAscendingOrder(r => r.DirectionName, StringComparer.Ordinal);

        (await admin.GetAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/available-groups")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- yordamchilar ----------

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }

    /// <summary>API orqali guruhsiz tyutor (fakultet o'zgartirish ssenariylari uchun).</summary>
    private static async Task<TutorDetail> CreateTutorViaApiAsync(HttpClient admin, Guid facultyId)
    {
        var response = await admin.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "API Tyutor", hemisId = TestClients.RandomHemisId(), phone = TestClients.RandomPhone(),
            password = TestClients.DefaultPassword, facultyId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<TutorDetail>())!;
    }
}
