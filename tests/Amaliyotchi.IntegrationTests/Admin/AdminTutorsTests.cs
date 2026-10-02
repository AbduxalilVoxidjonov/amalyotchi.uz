using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Groups;
using Amaliyotchi.Application.Features.Admin.Tutors;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Students;
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
        var rowFaculty = row.Faculties.Should().ContainSingle().Subject;
        rowFaculty.Id.Should().Be(groupA.FacultyId);
        rowFaculty.Code.Should().NotBeNullOrEmpty();
        rowFaculty.Name.Should().NotBeNullOrEmpty();
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
        (await client.PostJsonAsync("/api/admin/tutors", new { fullName = "X Y", hemisId = "123456789012", password = "Parol-12345", facultyIds = new[] { Guid.CreateVersion7() } }))
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
            json.RootElement.GetProperty("scopes")[0].GetProperty("level").GetString().Should().Be("group", "enum camelCase string");
        }

        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        detail.Id.Should().Be(tutor.Id);
        detail.FullName.Should().Be("Karta Tyutor");
        detail.Phone.Should().Be(tutor.PhoneNumber);
        var faculty = detail.Faculties.Should().ContainSingle().Subject;
        faculty.Id.Should().Be(group.FacultyId);
        faculty.Code.Should().NotBeNullOrEmpty();
        faculty.Name.Should().NotBeNullOrEmpty();
        detail.IsActive.Should().BeTrue();
        detail.LastLoginAt.Should().BeNull();
        detail.CreatedAt.Should().BeAfter(DateTimeOffset.MinValue);
        var scope = detail.Scopes.Should().ContainSingle().Subject;
        scope.Level.Should().Be(TutorScopeLevel.Group);
        scope.FacultyId.Should().Be(group.FacultyId);
        scope.DepartmentId.Should().Be(group.DepartmentId);
        scope.DirectionId.Should().Be(group.DirectionId);
        scope.GroupId.Should().Be(group.GroupId);
        scope.Name.Should().Be(group.GroupName);
        scope.Path.Split(" › ").Should().HaveCount(3, "fakultet › kafedra › yo'nalish");
        scope.Groups.Should().Be(1);
        scope.Students.Should().Be(2);
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
            facultyIds = new[] { facultyId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        response.Headers.Location!.ToString().Should().EndWith($"/api/admin/tutors/{detail.Id}");
        detail.FullName.Should().Be("Yangi Tyutor");
        detail.HemisId.Should().Be(hemisId);
        detail.Phone.Should().Be(phone);
        detail.Faculties.Select(f => f.Id).Should().Equal(facultyId);
        detail.IsActive.Should().BeTrue();
        detail.Scopes.Should().BeEmpty();
        detail.Groups.Should().BeEmpty();

        var login = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { hemisId, password = "Yangi-Parol-1" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await client.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={phone}");
        page.Items.Should().ContainSingle(t => t.Id == detail.Id);

        var audit = await Factory.WithDbAsync(db =>
            db.AuditLogs.Where(a => a.Action == AuditAction.TutorCreated && a.EntityId == detail.Id.ToString())
                .Select(a => a.Changes).SingleAsync());
        audit.Should().Contain(facultyId.ToString(), "tafsilotda facultyIds");
    }

    [Fact]
    public async Task Yaratish_TelefonsizHam_201()
    {
        var facultyId = await Factory.CreateFacultyAsync();
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Telefonsiz Tyutor", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = new[] { facultyId }
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
            fullName = "Takror Tyutor", hemisId = existing.HemisId, password = "Parol-12345", facultyIds = new[] { facultyId }
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
            fullName = "Takror Tyutor", hemisId = deleted.HemisId, password = "Parol-12345", facultyIds = new[] { facultyId }
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
            password = "Parol-12345", facultyIds = new[] { facultyId }
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
            fullName = "A", hemisId = "12ab", phone = "12345", password = "qisqa", facultyIds = new[] { facultyId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = json.RootElement.GetProperty("errors");
        errors.TryGetProperty("FullName", out _).Should().BeTrue();
        errors.TryGetProperty("HemisId", out _).Should().BeTrue();
        errors.TryGetProperty("Phone", out _).Should().BeTrue();
        errors.TryGetProperty("Password", out _).Should().BeTrue();

        var emptyId = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "To'g'ri Ism", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = new[] { Guid.Empty }
        });
        emptyId.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var noFaculty = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "To'g'ri Ism", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = Array.Empty<Guid>()
        });
        noFaculty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(noFaculty)).TryGetProperty("FacultyIds", out _).Should().BeTrue();

        var missingList = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "To'g'ri Ism", hemisId = TestClients.RandomHemisId(), password = "Parol-12345"
        });
        missingList.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var duplicate = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "To'g'ri Ism", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = new[] { facultyId, facultyId }
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(duplicate)).TryGetProperty("FacultyIds", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Yaratish_FakultetYoq_404_FaolEmas_409()
    {
        var client = await Factory.LoginAsAdminAsync();

        var missing = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Fakultetsiz", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = new[] { Guid.CreateVersion7() }
        });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Detail(missing)).Should().Be("Fakultet topilmadi.");

        var facultyId = await Factory.CreateFacultyAsync("Faol Emas Fakultet");
        (await client.PatchAsJsonAsync($"/api/admin/faculties/{facultyId}/status", new { isActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var activeFacultyId = await Factory.CreateFacultyAsync();
        var inactive = await client.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "Faol Emas", hemisId = TestClients.RandomHemisId(), password = "Parol-12345", facultyIds = new[] { activeFacultyId, facultyId }
        });
        inactive.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(inactive)).Should().Be("Fakultet faol emas: Faol Emas Fakultet");
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
            fullName = "Yangi Ism", phone, facultyIds = new[] { group.FacultyId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        detail.FullName.Should().Be("Yangi Ism");
        detail.Phone.Should().Be(phone);
        detail.Faculties.Select(f => f.Id).Should().Equal(group.FacultyId);
        detail.Groups.Should().ContainSingle(g => g.GroupId == group.GroupId, "fakultet o'zgarmadi — biriktiruv saqlanadi");

        var cleared = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = "Yangi Ism", phone = (string?)null, facultyIds = new[] { group.FacultyId }
        });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cleared.Content.ReadAsync<TutorDetail>())!.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Yangilash_KolamiBorFakultetniOlibTashlash_409_QoshishErkin_AjratgachOlibTashlanadi()
    {
        var tutor = await Factory.CreateTutorAsync(); // guruh darajasidagi ko'lam — o'z fakultetida
        var otherFacultyId = await Factory.CreateFacultyAsync("Qo'shimcha Fakultet");
        var client = await Factory.LoginAsAdminAsync();
        var facultyName = (await client.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Faculties.Single().Name;

        // Ko'lami bor fakultetni olib tashlash (almashtirish) — 409, aniq xabar
        var response = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.PhoneNumber, facultyIds = new[] { otherFacultyId }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Be($"{facultyName} fakultetida tyutorga ko'lam biriktirilgan — avval uni ajrating.");
        (await client.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Faculties.Select(f => f.Id)
            .Should().Equal([tutor.FacultyId!.Value], "409 hech narsani o'zgartirmaydi");

        // Fakultet qo'shish — erkin; ko'lam/biriktiruvlar saqlanadi
        var added = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.PhoneNumber, facultyIds = new[] { tutor.FacultyId, otherFacultyId }
        });
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        var addedDetail = (await added.Content.ReadAsync<TutorDetail>())!;
        addedDetail.Faculties.Select(f => f.Id).Should().BeEquivalentTo([tutor.FacultyId!.Value, otherFacultyId]);
        addedDetail.Faculties.Should().BeInAscendingOrder(f => f.Name, StringComparer.Ordinal);
        addedDetail.Groups.Should().ContainSingle(g => g.GroupId == tutor.GroupId);
        addedDetail.Scopes.Should().ContainSingle();

        // Ko'lam ajratilgach — eski fakultet olib tashlanadi, faqat yangisi qoladi
        (await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes", new { scopes = Array.Empty<object>() })).EnsureSuccessStatusCode();
        var moved = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.PhoneNumber, facultyIds = new[] { otherFacultyId }
        });
        moved.StatusCode.Should().Be(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());
        (await moved.Content.ReadAsync<TutorDetail>())!.Faculties.Select(f => f.Id).Should().Equal(otherFacultyId);

        var primary = await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == tutor.Id).Select(u => u.FacultyId).SingleAsync());
        primary.Should().Be(otherFacultyId, "asosiy fakultet — ro'yxatning birinchisi");
        (await Factory.WithDbAsync(db => db.TutorFaculties.CountAsync(tf => tf.TutorUserId == tutor.Id))).Should().Be(1);

        var audit = await Factory.WithDbAsync(db =>
            db.AuditLogs.Where(a => a.Action == AuditAction.TutorUpdated && a.EntityId == tutor.Id.ToString())
                .Select(a => a.Changes).ToListAsync());
        audit.Should().HaveCount(2).And.OnlyContain(c => c!.Contains(otherFacultyId.ToString()), "tafsilotda facultyIds");
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
            fullName = tutor.FullName, phone = tutor.Phone, facultyIds = new[] { otherFacultyId }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<TutorDetail>())!.Faculties.Select(f => f.Id).Should().Equal(otherFacultyId);
    }

    [Fact]
    public async Task Yangilash_BoshFakultetlar_400_FaolEmasFakultet_409()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync();
        var tutor = await CreateTutorViaApiAsync(client, facultyId);

        var empty = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.Phone, facultyIds = Array.Empty<Guid>()
        });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Errors(empty)).TryGetProperty("FacultyIds", out _).Should().BeTrue();

        var inactiveId = await Factory.CreateFacultyAsync("Yopiq Fakultet");
        (await client.PatchAsJsonAsync($"/api/admin/faculties/{inactiveId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var inactive = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.Phone, facultyIds = new[] { facultyId, inactiveId }
        });
        inactive.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(inactive)).Should().Be("Fakultet faol emas: Yopiq Fakultet");
    }

    [Fact]
    public async Task Yangilash_Topilmasa_404_FakultetYoq_404()
    {
        var client = await Factory.LoginAsAdminAsync();
        var facultyId = await Factory.CreateFacultyAsync();

        var missing = await client.PutAsJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}", new
        {
            fullName = "Yoq", phone = (string?)null, facultyIds = new[] { facultyId }
        });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var tutor = await CreateTutorViaApiAsync(client, facultyId);
        var badFaculty = await client.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = (string?)null, facultyIds = new[] { facultyId, Guid.CreateVersion7() }
        });
        badFaculty.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Detail(badFaculty)).Should().Be("Fakultet topilmadi.");
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

        var tokenHash = Amaliyotchi.Application.Common.Security.RefreshTokenHash.Of(auth.RefreshToken);
        var revoked = await Factory.WithDbAsync(db =>
            db.RefreshTokens.Where(t => t.UserId == tutor.Id && t.Token == tokenHash).Select(t => t.RevokedReason).SingleAsync());
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

    // ---------- PUT {id}/scopes ----------

    [Fact]
    public async Task Kolamlar_FakultetDarajasi_FakultetdagiBarchaFaolGuruhlar()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var inactive = await Factory.CreateGroupAsync(groupA.FacultyId);
        var foreign = await Factory.CreateGroupAsync();
        await Factory.CreateStudentAsync(group: groupA);
        await Factory.CreateStudentAsync(group: groupB);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/groups/{inactive.GroupId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var tutor = await CreateTutorViaApiAsync(admin, groupA.FacultyId);

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "faculty", id = groupA.FacultyId } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<TutorDetail>())!;
        var scope = detail.Scopes.Should().ContainSingle().Subject;
        scope.Level.Should().Be(TutorScopeLevel.Faculty);
        scope.FacultyId.Should().Be(groupA.FacultyId);
        scope.DepartmentId.Should().BeNull();
        scope.DirectionId.Should().BeNull();
        scope.GroupId.Should().BeNull();
        scope.Name.Should().Be(detail.Faculties.Single().Name);
        scope.Path.Should().BeEmpty();
        scope.Groups.Should().Be(2, "faol emas guruh qamralmaydi");
        scope.Students.Should().Be(2);
        detail.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([groupA.GroupId, groupB.GroupId]);
        detail.Groups.Select(g => g.GroupId).Should().NotContain([inactive.GroupId, foreign.GroupId]);

        var page = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.Phone}");
        page.Items.Single().Groups.Should().BeEquivalentTo([groupA.GroupName, groupB.GroupName], "ro'yxat materializatsiyaga tayanadi");

        // Faol emas guruh qayta faollashtirilsa — ko'lam ichida bo'lgani uchun avtomatik qamraladi
        (await admin.PatchAsJsonAsync($"/api/admin/groups/{inactive.GroupId}/status", new { isActive = true })).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Groups.Select(g => g.GroupId)
            .Should().BeEquivalentTo([groupA.GroupId, groupB.GroupId, inactive.GroupId]);
    }

    [Fact]
    public async Task Kolamlar_KafedraYonalishGuruh_Darajalari()
    {
        // Fakultet: kafedra1 { yo'nalish11 { g111, g112 }, yo'nalish12 { g121 } }, kafedra2 { yo'nalish21 { g211 } }
        var g111 = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var g112 = await CreateGroupViaApiAsync(admin, g111.DirectionId);
        var g121 = await Factory.CreateGroupAsync(g111.FacultyId);
        var dept2 = await Factory.CreateDepartmentAsync(g111.FacultyId, "Ikkinchi kafedra");
        var dir21 = await Factory.CreateDirectionAsync(dept2, "Ikkinchi yo'nalish");
        var g211 = await CreateGroupViaApiAsync(admin, dir21);
        var tutor = await CreateTutorViaApiAsync(admin, g111.FacultyId);
        var facultyName = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Faculties.Single().Name;

        // Kafedra darajasi → kafedra1 ning barcha guruhlari
        var dept = await Put(admin, tutor.Id, new { level = "department", id = g111.DepartmentId });
        dept.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([g111.GroupId, g112.Id, g121.GroupId]);
        var deptScope = dept.Scopes.Should().ContainSingle().Subject;
        deptScope.Level.Should().Be(TutorScopeLevel.Department);
        deptScope.DepartmentId.Should().Be(g111.DepartmentId);
        deptScope.DirectionId.Should().BeNull();
        deptScope.Path.Should().Be(facultyName);
        deptScope.Groups.Should().Be(3);

        // Yo'nalish darajasi → faqat yo'nalish11 guruhlari
        var dir = await Put(admin, tutor.Id, new { level = "direction", id = g111.DirectionId });
        dir.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([g111.GroupId, g112.Id]);
        var dirScope = dir.Scopes.Should().ContainSingle().Subject;
        dirScope.Level.Should().Be(TutorScopeLevel.Direction);
        dirScope.DirectionId.Should().Be(g111.DirectionId);
        dirScope.GroupId.Should().BeNull();
        dirScope.Path.Should().Be($"{facultyName} › {dept.Scopes[0].Name}");
        dirScope.Name.Should().Be(dir.Groups[0].DirectionName);

        // Guruh darajasi → faqat shu guruh
        var single = await Put(admin, tutor.Id, new { level = "group", id = g111.GroupId });
        single.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([g111.GroupId]);
        single.Scopes.Should().ContainSingle().Which.Path.Should().Be($"{facultyName} › {deptScope.Name} › {dirScope.Name}");

        // Bir nechta ko'lam: 2-kafedra + 1-yo'nalishdagi bitta guruh — daraja → nom tartibida
        var multi = await Put(admin, tutor.Id,
            new { level = "group", id = g112.Id }, new { level = "department", id = dept2 });
        multi.Scopes.Select(x => x.Level).Should().Equal(TutorScopeLevel.Department, TutorScopeLevel.Group);
        multi.Scopes[0].Name.Should().Be("Ikkinchi kafedra");
        multi.Scopes[0].Groups.Should().Be(1);
        multi.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([g112.Id, g211.Id]);
    }

    [Fact]
    public async Task Kolamlar_Almashtirish_TarixSaqlanadi_QaytaFaollashadi()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateTutorAsync(groupA);
        var admin = await Factory.LoginAsAdminAsync();

        var original = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!;
        var originalAssignmentId = original.Groups.Single().AssignmentId;
        var originalScopeId = original.Scopes.Single().Id;

        // A → B: A ko'lami va biriktiruvi faolsizlantiriladi (o'chirilmaydi), B yaratiladi
        var afterSwap = await Put(admin, tutor.Id, new { level = "group", id = groupB.GroupId });
        afterSwap.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([groupB.GroupId]);
        afterSwap.Scopes.Single().GroupId.Should().Be(groupB.GroupId);

        var assignments = await Factory.WithDbAsync(db =>
            db.TutorAssignments.Where(a => a.TutorUserId == tutor.Id).Select(a => new { a.Id, a.StudentGroupId, a.IsActive }).ToListAsync());
        assignments.Should().HaveCount(2);
        assignments.Single(r => r.StudentGroupId == groupA.GroupId).IsActive.Should().BeFalse();
        assignments.Single(r => r.StudentGroupId == groupB.GroupId).IsActive.Should().BeTrue();
        var scopes = await Factory.WithDbAsync(db =>
            db.TutorScopes.Where(x => x.TutorUserId == tutor.Id).Select(x => new { x.Id, x.StudentGroupId, x.IsActive }).ToListAsync());
        scopes.Should().HaveCount(2);
        scopes.Single(x => x.StudentGroupId == groupA.GroupId).IsActive.Should().BeFalse();

        // A qayta keladi (B bilan birga, takror bilan) — mavjud yozuvlar faollashadi, yangi yozuv yaratilmaydi
        var afterBoth = await Put(admin, tutor.Id,
            new { level = "group", id = groupA.GroupId }, new { level = "group", id = groupB.GroupId }, new { level = "group", id = groupA.GroupId });
        afterBoth.Groups.Should().HaveCount(2);
        afterBoth.Groups.Single(g => g.GroupId == groupA.GroupId).AssignmentId.Should().Be(originalAssignmentId);
        afterBoth.Scopes.Should().HaveCount(2);
        afterBoth.Scopes.Single(x => x.GroupId == groupA.GroupId).Id.Should().Be(originalScopeId);

        (await Factory.WithDbAsync(db => db.TutorAssignments.CountAsync(a => a.TutorUserId == tutor.Id))).Should().Be(2);
        (await Factory.WithDbAsync(db => db.TutorScopes.CountAsync(x => x.TutorUserId == tutor.Id))).Should().Be(2);

        // Bo'sh ro'yxat — hammasi ajratiladi
        var clear = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes", new { scopes = Array.Empty<object>() });
        clear.StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = (await clear.Content.ReadAsync<TutorDetail>())!;
        cleared.Scopes.Should().BeEmpty();
        cleared.Groups.Should().BeEmpty();

        var page = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.PhoneNumber}");
        page.Items.Single().Groups.Should().BeEmpty("ro'yxat ham faol biriktiruvlarga tayanadi");

        var audit = await Factory.WithDbAsync(db =>
            db.AuditLogs.Where(a => a.Action == AuditAction.TutorScopesChanged && a.EntityId == tutor.Id.ToString())
                .Select(a => a.Changes).ToListAsync());
        audit.Should().HaveCount(3);
        audit.Should().Contain(c => c!.Contains("\"group\""), "tafsilotda daraja (API nomi bilan)");
    }

    [Fact]
    public async Task Kolamlar_YonalishKolami_KeyinYaratilganGuruh_AvtomatikQamraladi()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await CreateTutorViaApiAsync(admin, group.FacultyId);
        var tutorUser = new TestUser(tutor.Id, tutor.FullName, UserRole.Tutor, tutor.Phone!, TestClients.DefaultPassword,
            group.FacultyId, null, HemisId: tutor.HemisId);
        (await Put(admin, tutor.Id, new { level = "direction", id = group.DirectionId })).Groups.Should().HaveCount(1);

        // Shu yo'nalishda yangi guruh — tyutorga avtomatik biriktiriladi
        var created = await CreateGroupViaApiAsync(admin, group.DirectionId);
        var newcomer = await Factory.CreateStudentAsync(group: group with { GroupId = created.Id, GroupName = created.Name });

        var detail = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!;
        detail.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([group.GroupId, created.Id]);
        detail.Scopes.Single().Groups.Should().Be(2);

        // ScopeResolver — tyutor kirganda yangi guruh talabasi ko'rinadi
        var tutorClient = await Factory.LoginAsync(tutorUser);
        var students = (await tutorClient.GetFromJsonAsync<List<TutorStudent>>("/api/tutor/students"))!;
        students.Select(x => x.Id).Should().Contain(newcomer.Id);

        // Boshqa yo'nalishdagi yangi guruh — qamralmaydi
        var other = await Factory.CreateGroupAsync(group.FacultyId);
        var otherStudent = await Factory.CreateStudentAsync(group: other);
        students = (await tutorClient.GetFromJsonAsync<List<TutorStudent>>("/api/tutor/students"))!;
        students.Select(x => x.Id).Should().NotContain(otherStudent.Id);
    }

    [Fact]
    public async Task Kolamlar_Kesishuv_409_AniqXabar()
    {
        var group = await Factory.CreateGroupAsync();
        var owner = await Factory.CreateTutorAsync(group, "Egasi Tyutor"); // guruh darajasi
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await CreateTutorViaApiAsync(admin, group.FacultyId);
        var facultyName = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Faculties.Single().Name;

        // Fakultet vs guruh
        var faculty = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "faculty", id = group.FacultyId } } });
        faculty.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(faculty)).Should().Be($"{group.GroupName} (guruh) Egasi Tyutor tyutoriga biriktirilgan.");

        // Teng: guruh vs guruh
        var same = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = group.GroupId } } });
        same.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(same)).Should().Be($"{group.GroupName} (guruh) Egasi Tyutor tyutoriga biriktirilgan.");

        // Egasi kafedraga ko'tariladi → yo'nalish vs kafedra
        var deptName = (await Put(admin, owner.Id, new { level = "department", id = group.DepartmentId })).Scopes.Single().Name;
        var direction = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "direction", id = group.DirectionId } } });
        direction.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(direction)).Should().Be($"{deptName} (kafedra) Egasi Tyutor tyutoriga biriktirilgan.");

        // Egasi fakultetga ko'tariladi → boshqa kafedra ham band
        var dept2 = await Factory.CreateDepartmentAsync(group.FacultyId);
        await Put(admin, owner.Id, new { level = "faculty", id = group.FacultyId });
        var other = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "department", id = dept2 } } });
        other.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(other)).Should().Be($"{facultyName} (fakultet) Egasi Tyutor tyutoriga biriktirilgan.");

        // Egasi ajratgach — bo'shaydi; hech narsa yozilmagan (409 lar iz qoldirmagan)
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{owner.Id}/scopes", new { scopes = Array.Empty<object>() })).EnsureSuccessStatusCode();
        (await Put(admin, tutor.Id, new { level = "faculty", id = group.FacultyId })).Groups.Should().Contain(g => g.GroupId == group.GroupId);
        (await Factory.WithDbAsync(db => db.TutorScopes.CountAsync(x => x.TutorUserId == tutor.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Kolamlar_OzIchidaOtaVaBola_BolaJimginaTashlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await CreateTutorViaApiAsync(admin, group.FacultyId);

        var detail = await Put(admin, tutor.Id,
            new { level = "group", id = group.GroupId },
            new { level = "direction", id = group.DirectionId },
            new { level = "department", id = group.DepartmentId });

        var scope = detail.Scopes.Should().ContainSingle().Subject;
        scope.Level.Should().Be(TutorScopeLevel.Department);
        detail.Groups.Should().ContainSingle(g => g.GroupId == group.GroupId);
        (await Factory.WithDbAsync(db => db.TutorScopes.CountAsync(x => x.TutorUserId == tutor.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Kolamlar_FaolEmasTyutorgaHam_Ruxsat()
    {
        var group = await Factory.CreateGroupAsync();
        var other = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/tutors/{tutor.Id}/status", new { isActive = false })).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = other.GroupId } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Kolamlar_BegonaFakultet_400()
    {
        var tutor = await Factory.CreateTutorAsync();
        var foreign = await Factory.CreateGroupAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var foreignFacultyName = (await admin.GetFromJsonAsync<TutorDetail>(
            $"/api/admin/tutors/{(await Factory.CreateTutorAsync(foreign)).Id}"))!.Faculties.Single().Name;

        var group = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = foreign.GroupId } } });
        group.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(group)).Should().Be($"Guruh tyutor fakultetiga tegishli emas: {foreign.GroupName}");

        var direction = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "direction", id = foreign.DirectionId } } });
        direction.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(direction)).Should().StartWith("Yo'nalish tyutor fakultetiga tegishli emas: ");

        var faculty = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "faculty", id = foreign.FacultyId } } });
        faculty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(faculty)).Should().Be($"Fakultet tyutor fakultetiga tegishli emas: {foreignFacultyName}");

        (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!.Groups
            .Should().ContainSingle(g => g.GroupId == tutor.GroupId, "xato so'rov hech narsani o'zgartirmaydi");
    }

    [Fact]
    public async Task Kolamlar_FaolEmasTugun_400_Topilmasa_400_Validatsiya()
    {
        var group = await Factory.CreateGroupAsync();
        var inactiveDir = await Factory.CreateGroupAsync(group.FacultyId);
        var tutor = await Factory.CreateTutorAsync(group);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/directions/{inactiveDir.DirectionId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var directionName = (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/directions/{inactiveDir.DirectionId}")).GetProperty("name").GetString();

        var inactive = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "direction", id = inactiveDir.DirectionId } } });
        inactive.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(inactive)).Should().Be($"Yo'nalish faol emas: {directionName}");

        var missing = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = Guid.CreateVersion7() } } });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Detail(missing)).Should().Be("Guruh topilmadi.");

        var missingDept = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "department", id = Guid.CreateVersion7() } } });
        (await Detail(missingDept)).Should().Be("Kafedra topilmadi.");

        var emptyId = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = Guid.Empty } } });
        emptyId.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badLevel = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
            new { scopes = new[] { new { level = "university", id = group.GroupId } } });
        badLevel.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/scopes", new { scopes = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Kolamlar_FaolOquvYiliYoq_409()
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
            var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes",
                new { scopes = new[] { new { level = "group", id = other.GroupId } } });
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await Detail(response)).Should().Be("Faol o'quv yili yo'q.");

            // Yangi biriktiruv kerak bo'lmasa (faqat ajratish) — o'quv yili talab qilinmaydi
            (await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}/scopes", new { scopes = Array.Empty<object>() }))
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

    // ---------- GET {id}/scope-tree ----------

    [Fact]
    public async Task ScopeTree_FaolTugunlar_TutorIdTutorNameTogri()
    {
        var groupA = await Factory.CreateGroupAsync(course: 2);
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId, course: 1);
        var groupC = await Factory.CreateGroupAsync(groupA.FacultyId, course: 3);
        var inactive = await Factory.CreateGroupAsync(groupA.FacultyId);
        var foreign = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(groupA, "Ozi Tyutor");
        var other = await Factory.CreateTutorAsync(groupB, "Boshqa Tyutor");
        await Factory.CreateStudentAsync(group: groupC);
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PatchAsJsonAsync($"/api/admin/groups/{inactive.GroupId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var dept2 = await Factory.CreateDepartmentAsync(groupA.FacultyId, "Band kafedra");
        var inactiveDept = await Factory.CreateDepartmentAsync(groupA.FacultyId, "Faol emas kafedra");
        (await admin.PatchAsJsonAsync($"/api/admin/departments/{inactiveDept}/status", new { isActive = false })).EnsureSuccessStatusCode();
        var deptOwner = await CreateTutorViaApiAsync(admin, groupA.FacultyId);
        await Put(admin, deptOwner.Id, new { level = "department", id = dept2 });

        var tree = (await admin.GetFromJsonAsync<List<TutorScopeTree>>($"/api/admin/tutors/{tutor.Id}/scope-tree"))!
            .Should().ContainSingle("bitta fakultet — bitta daraxt").Subject;

        tree.Id.Should().Be(groupA.FacultyId);
        tree.Name.Should().NotBeNullOrEmpty();
        tree.Code.Should().NotBeNullOrEmpty();
        tree.TutorId.Should().BeNull("fakultet darajasida hech kim yo'q");
        tree.TutorName.Should().BeNull();
        tree.Departments.Select(d => d.Id).Should().Contain([groupA.DepartmentId, dept2]).And.NotContain(inactiveDept);
        tree.Departments.Should().BeInAscendingOrder(d => d.Name, StringComparer.Ordinal);

        var dept = tree.Departments.Single(d => d.Id == groupA.DepartmentId);
        dept.TutorId.Should().BeNull();
        var bandDept = tree.Departments.Single(d => d.Id == dept2);
        bandDept.TutorId.Should().Be(deptOwner.Id);
        bandDept.TutorName.Should().Be(deptOwner.FullName);
        bandDept.Directions.Should().BeEmpty();

        var groups = dept.Directions.SelectMany(d => d.Groups).ToList();
        groups.Select(g => g.Id).Should().BeEquivalentTo([groupA.GroupId, groupB.GroupId, groupC.GroupId]);
        groups.Select(g => g.Id).Should().NotContain([inactive.GroupId, foreign.GroupId]);
        dept.Directions.Should().BeInAscendingOrder(d => d.Name, StringComparer.Ordinal);
        dept.Directions.Should().OnlyContain(d => d.TutorId == null, "yo'nalish darajasida ko'lam yo'q");

        var a = groups.Single(g => g.Id == groupA.GroupId);
        a.TutorId.Should().Be(tutor.Id);
        a.TutorName.Should().Be("Ozi Tyutor", "shu tyutorning o'zi bo'lsa ham to'ldiriladi");
        a.Name.Should().Be(groupA.GroupName);
        a.Course.Should().Be(2);

        var b = groups.Single(g => g.Id == groupB.GroupId);
        b.TutorId.Should().Be(other.Id);
        b.TutorName.Should().Be("Boshqa Tyutor");

        var c = groups.Single(g => g.Id == groupC.GroupId);
        c.TutorId.Should().BeNull();
        c.TutorName.Should().BeNull();
        c.Students.Should().Be(1);

        // Fakultet darajasiga ko'tarilsa — ildizda ko'rinadi, guruhda emas
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{other.Id}/scopes", new { scopes = Array.Empty<object>() })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{deptOwner.Id}/scopes", new { scopes = Array.Empty<object>() })).EnsureSuccessStatusCode();
        await Put(admin, tutor.Id, new { level = "faculty", id = groupA.FacultyId });
        tree = (await admin.GetFromJsonAsync<List<TutorScopeTree>>($"/api/admin/tutors/{tutor.Id}/scope-tree"))!.Single();
        tree.TutorId.Should().Be(tutor.Id);
        tree.TutorName.Should().Be("Ozi Tyutor");
        tree.Departments.SelectMany(d => d.Directions).SelectMany(d => d.Groups).Should().OnlyContain(g => g.TutorId == null);

        (await admin.GetAsync($"/api/admin/tutors/{Guid.CreateVersion7()}/scope-tree")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- ko'p fakultet ----------

    [Fact]
    public async Task KopFakultet_Yaratish_KartaNomTartibida_FiltrHarIkkalasidaTopadi_AsosiyBirinchisi()
    {
        var facultyB = await Factory.CreateFacultyAsync("B Fakultet " + Guid.NewGuid().ToString("N")[..4]);
        var facultyA = await Factory.CreateFacultyAsync("A Fakultet " + Guid.NewGuid().ToString("N")[..4]);
        var admin = await Factory.LoginAsAdminAsync();

        var tutor = await CreateTutorViaApiAsync(admin, facultyB, facultyA); // so'rovda B birinchi

        tutor.Faculties.Select(f => f.Id).Should().Equal(facultyA, facultyB);
        tutor.Faculties.Should().BeInAscendingOrder(f => f.Name, StringComparer.Ordinal);
        tutor.Faculties.Should().OnlyContain(f => f.Code.Length > 0 && f.Name.Length > 0);

        var detail = (await admin.GetFromJsonAsync<TutorDetail>($"/api/admin/tutors/{tutor.Id}"))!;
        detail.Faculties.Select(f => f.Id).Should().Equal(facultyA, facultyB);

        var primary = await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == tutor.Id).Select(u => u.FacultyId).SingleAsync());
        primary.Should().Be(facultyB, "asosiy fakultet — so'rov ro'yxatining birinchisi");
        (await Factory.WithDbAsync(db => db.TutorFaculties.CountAsync(tf => tf.TutorUserId == tutor.Id))).Should().Be(2);

        foreach (var facultyId in new[] { facultyA, facultyB })
        {
            var page = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?facultyId={facultyId}&q={tutor.Phone}");
            var row = page.Items.Should().ContainSingle(t => t.Id == tutor.Id, $"{facultyId} filtri topishi kerak").Subject;
            row.Faculties.Select(f => f.Id).Should().Equal(facultyA, facultyB);
        }

        var unrelated = await Factory.CreateFacultyAsync();
        (await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?facultyId={unrelated}&q={tutor.Phone}")).Items.Should().BeEmpty();

        // Qidiruv — ikkinchi fakultet nomi bo'yicha ham topadi
        var byName = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={Uri.EscapeDataString(tutor.Faculties[1].Name)}");
        byName.Items.Should().Contain(t => t.Id == tutor.Id);

        // Kirganda JWT'dagi fakultet — asosiy
        var login = await Factory.CreateClient().PostJsonAsync("/api/auth/login", new { tutor.HemisId, password = TestClients.DefaultPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("user").GetProperty("facultyId").GetGuid().Should().Be(facultyB);
    }

    [Fact]
    public async Task KopFakultet_ScopeTreeIkkiDaraxt_IkkinchiFakultetGuruhigaKolam_Materializatsiya()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync();
        var groupB2 = await Factory.CreateGroupAsync(groupB.FacultyId);
        await Factory.CreateStudentAsync(group: groupB);
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await CreateTutorViaApiAsync(admin, groupA.FacultyId, groupB.FacultyId);

        var trees = (await admin.GetFromJsonAsync<List<TutorScopeTree>>($"/api/admin/tutors/{tutor.Id}/scope-tree"))!;
        trees.Should().HaveCount(2);
        trees.Select(t => t.Id).Should().BeEquivalentTo([groupA.FacultyId, groupB.FacultyId]);
        trees.Should().BeInAscendingOrder(t => t.Name, StringComparer.Ordinal);
        trees.Single(t => t.Id == groupA.FacultyId).Departments.SelectMany(d => d.Directions).SelectMany(d => d.Groups)
            .Select(g => g.Id).Should().Equal(groupA.GroupId);
        trees.Single(t => t.Id == groupB.FacultyId).Departments.SelectMany(d => d.Directions).SelectMany(d => d.Groups)
            .Select(g => g.Id).Should().BeEquivalentTo([groupB.GroupId, groupB2.GroupId]);

        // Har ikkala fakultetdan ko'lam: A — guruh, B — fakultet darajasi
        var detail = await Put(admin, tutor.Id,
            new { level = "group", id = groupA.GroupId }, new { level = "faculty", id = groupB.FacultyId });
        detail.Scopes.Should().HaveCount(2);
        var facultyScope = detail.Scopes.Single(x => x.Level == TutorScopeLevel.Faculty);
        facultyScope.FacultyId.Should().Be(groupB.FacultyId);
        facultyScope.Groups.Should().Be(2, "faqat B fakultetining guruhlari — A guruhi qamralmaydi");
        facultyScope.Students.Should().Be(1);
        detail.Scopes.Single(x => x.Level == TutorScopeLevel.Group).Groups.Should().Be(1);
        detail.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([groupA.GroupId, groupB.GroupId, groupB2.GroupId]);

        trees = (await admin.GetFromJsonAsync<List<TutorScopeTree>>($"/api/admin/tutors/{tutor.Id}/scope-tree"))!;
        trees.Single(t => t.Id == groupB.FacultyId).TutorId.Should().Be(tutor.Id);
        trees.Single(t => t.Id == groupA.FacultyId).TutorId.Should().BeNull();

        // Boshqa tyutor B fakultetida — kesishuv 409 (tekshiruv barcha tyutor fakultetlari bo'yicha)
        var other = await CreateTutorViaApiAsync(admin, groupB.FacultyId);
        var clash = await admin.PutAsJsonAsync($"/api/admin/tutors/{other.Id}/scopes",
            new { scopes = new[] { new { level = "group", id = groupB2.GroupId } } });
        clash.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Ro'yxat guruhlari — ikkala fakultetdan
        var page = await admin.GetPagedAsync<TutorRow>($"/api/admin/tutors?q={tutor.Phone}");
        page.Items.Single().Groups.Should().BeEquivalentTo([groupA.GroupName, groupB.GroupName, groupB2.GroupName]);

        // Dashboard — ikkala fakultet kodi
        var dashboard = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        var row = dashboard.GetProperty("tutors").EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == tutor.Id);
        var codes = row.GetProperty("facultyCode").GetString()!.Split(", ");
        codes.Should().BeEquivalentTo(detail.Faculties.Select(f => f.Code));
    }

    [Fact]
    public async Task KopFakultet_FakultetniOchirish_TyutorBoglanganBolsa_409()
    {
        var facultyA = await Factory.CreateFacultyAsync();
        var facultyB = await Factory.CreateFacultyAsync();
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await CreateTutorViaApiAsync(admin, facultyA, facultyB); // asosiy — A; B faqat tutor_faculties orqali

        var response = await admin.DeleteAsync($"/api/admin/faculties/{facultyB}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("tyutorlar");

        // B olib tashlangach — o'chiriladi
        (await admin.PutAsJsonAsync($"/api/admin/tutors/{tutor.Id}", new
        {
            fullName = tutor.FullName, phone = tutor.Phone, facultyIds = new[] { facultyA }
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/admin/faculties/{facultyB}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---------- yordamchilar ----------

    private static async Task<string?> Detail(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("detail").GetString();
    }

    /// <summary>400 javobidagi <c>errors</c> obyekti (ProblemDetails).</summary>
    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Clone();
    }

    /// <summary><c>PUT .../scopes</c> → 200 <see cref="TutorDetail"/>; muvaffaqiyatsiz bo'lsa tanadagi xabar bilan yiqiladi.</summary>
    private static async Task<TutorDetail> Put(HttpClient admin, Guid tutorId, params object[] scopes)
    {
        var response = await admin.PutAsJsonAsync($"/api/admin/tutors/{tutorId}/scopes", new { scopes });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<TutorDetail>())!;
    }

    /// <summary>API orqali yo'nalishda guruh — <c>CreateGroupCommand</c> ning ko'lam bo'yicha avtomatik biriktirish yo'li ham ishlaydi.</summary>
    private static async Task<GroupDto> CreateGroupViaApiAsync(HttpClient admin, Guid directionId)
    {
        var response = await admin.PostJsonAsync($"/api/admin/directions/{directionId}/groups",
            new { name = $"G-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", course = 2 });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<GroupDto>())!;
    }

    /// <summary>API orqali ko'lamsiz tyutor (fakultet o'zgartirish / ko'lam ssenariylari uchun) — bir yoki bir nechta fakultet.</summary>
    private static async Task<TutorDetail> CreateTutorViaApiAsync(HttpClient admin, params Guid[] facultyIds)
    {
        var response = await admin.PostJsonAsync("/api/admin/tutors", new
        {
            fullName = "API Tyutor", hemisId = TestClients.RandomHemisId(), phone = TestClients.RandomPhone(),
            password = TestClients.DefaultPassword, facultyIds
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<TutorDetail>())!;
    }
}
