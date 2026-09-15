using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Test foydalanuvchisi: bazaga yozilgan hisob + kirish uchun kerakli sirlar.
/// Talaba uchun <see cref="GroupId"/> — profil guruhi; tyutor uchun — birinchi biriktirilgan guruh.
/// <see cref="HemisId"/> — parol bilan kiradiganlar (admin/tyutor) uchun login identifikatori;
/// talabada bo'sh (u Telegram orqali kiradi, <c>StudentProfile.HemisId</c> alohida).</summary>
public sealed record TestUser(
    Guid Id,
    string FullName,
    UserRole Role,
    string PhoneNumber,
    string Password,
    Guid? FacultyId,
    long? TelegramId,
    Guid? GroupId = null,
    string HemisId = "");

/// <summary>Tashkiliy zanjir: o'quv yili → fakultet → kafedra → yo'nalish → guruh.</summary>
public sealed record TestGroup(
    Guid AcademicYearId, Guid FacultyId, Guid DepartmentId, Guid DirectionId, Guid GroupId, string GroupName, int Course);

/// <summary>Har test o'ziga kerakli foydalanuvchini yaratadi (tasodifiy telefon/HEMIS ID) — testlar bir bazada
/// bir-biriga xalaqit bermaydi. Keyingi agentlar: <c>fixture.Factory.LoginAsTutorAsync()</c> → Bearer bilan tayyor <c>HttpClient</c>.</summary>
public static class TestClients
{
    public const string DefaultPassword = "Parol-12345";

    public static Task<TestUser> CreateAdminAsync(this ApiFactory factory, string? fullName = null) =>
        factory.CreatePasswordUserAsync(UserRole.Admin, fullName ?? "Test Admin", facultyId: null);

    /// <summary>Tyutor: fakultetga biriktirilgan + kamida bitta guruhga faol <c>TutorAssignment</c>.
    /// Guruh berilmasa — yangi fakultet/yo'nalish/guruh yaratiladi.</summary>
    public static async Task<TestUser> CreateTutorAsync(
        this ApiFactory factory, TestGroup? group = null, string? fullName = null, params Guid[] extraGroupIds)
    {
        group ??= await factory.CreateGroupAsync();
        var tutor = await factory.CreatePasswordUserAsync(UserRole.Tutor, fullName ?? "Test Tyutor", group.FacultyId);

        await factory.WithDbAsync(async db =>
        {
            db.TutorAssignments.Add(TutorAssignment.Create(tutor.Id, group.GroupId, group.AcademicYearId));
            foreach (var extra in extraGroupIds)
                db.TutorAssignments.Add(TutorAssignment.Create(tutor.Id, extra, group.AcademicYearId));
            await db.SaveChangesAsync();
        });

        return tutor with { GroupId = group.GroupId };
    }

    /// <summary>Talaba: parolsiz, Telegram hisobi bog'langan, <c>StudentProfile</c> guruh bilan.
    /// <paramref name="telegramId"/> berilmasa tasodifiy; <paramref name="group"/> berilmasa yangi zanjir yaratiladi.</summary>
    public static async Task<TestUser> CreateStudentAsync(
        this ApiFactory factory, long? telegramId = null, TestGroup? group = null, string? fullName = null, bool active = true)
    {
        group ??= await factory.CreateGroupAsync();
        var phone = RandomPhone();
        var tgId = telegramId ?? Random.Shared.NextInt64(100_000_000, 9_000_000_000);

        var user = User.CreateStudent(fullName ?? "Test Talaba", group.FacultyId, phone);
        user.LinkTelegram(tgId, phone);
        if (!active)
            user.Deactivate();

        var profile = StudentProfile.Create(user.Id, RandomHemisId(), group.GroupId);

        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            db.StudentProfiles.Add(profile);
            await db.SaveChangesAsync();
        });

        return new TestUser(user.Id, user.FullName, user.Role, phone, string.Empty, group.FacultyId, tgId, group.GroupId);
    }

    public static Task<Guid> CreateFacultyAsync(this ApiFactory factory, string? name = null) =>
        factory.WithDbAsync(async db =>
        {
            var suffix = Suffix();
            var faculty = Faculty.Create(name ?? $"Fakultet {suffix}", $"F{suffix}");
            db.Faculties.Add(faculty);
            await db.SaveChangesAsync();
            return faculty.Id;
        });

    /// <summary>O'quv yili (bitta umumiy, "Test 2026-2027") → fakultet (berilgan yoki yangi) → kafedra (berilgan
    /// fakultetda mavjud bo'lsa qayta ishlatiladi, aks holda yangi) → yo'nalish → guruh.</summary>
    public static Task<TestGroup> CreateGroupAsync(this ApiFactory factory, Guid? facultyId = null, int course = 3) =>
        factory.WithDbAsync(async db =>
        {
            var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.Name == TestAcademicYear);
            if (year is null)
            {
                year = AcademicYear.Create(TestAcademicYear, new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
                year.Activate();
                db.AcademicYears.Add(year);
                await db.SaveChangesAsync();
            }

            var suffix = Suffix();
            Faculty faculty;
            if (facultyId is { } id)
            {
                faculty = await db.Faculties.Include(f => f.Departments).ThenInclude(d => d.Directions)
                    .FirstAsync(f => f.Id == id);
            }
            else
            {
                faculty = Faculty.Create($"Fakultet {suffix}", $"F{suffix}");
                db.Faculties.Add(faculty);
            }

            var department = faculty.Departments.FirstOrDefault() ?? faculty.AddDepartment($"Kafedra {suffix}", $"K{suffix}");
            var direction = department.AddDirection($"Yo'nalish {suffix}", $"D{suffix}");
            var group = direction.AddGroup($"G-{suffix}", course, year.Id);
            await db.SaveChangesAsync();

            return new TestGroup(year.Id, faculty.Id, department.Id, direction.Id, group.Id, group.Name, group.Course);
        });

    /// <summary>Berilgan fakultetda mustaqil (yo'nalishsiz) kafedra — kafedra darajasi CRUD testlari uchun.</summary>
    public static Task<Guid> CreateDepartmentAsync(this ApiFactory factory, Guid facultyId, string? name = null) =>
        factory.WithDbAsync(async db =>
        {
            var suffix = Suffix();
            var faculty = await db.Faculties.Include(f => f.Departments).FirstAsync(f => f.Id == facultyId);
            var department = faculty.AddDepartment(name ?? $"Kafedra {suffix}", $"K{suffix}");
            await db.SaveChangesAsync();
            return department.Id;
        });

    /// <summary>Berilgan kafedrada mustaqil (guruhsiz) yo'nalish — yo'nalish darajasi CRUD testlari uchun.</summary>
    public static Task<Guid> CreateDirectionAsync(this ApiFactory factory, Guid departmentId, string? name = null) =>
        factory.WithDbAsync(async db =>
        {
            var suffix = Suffix();
            var department = await db.Departments.Include(d => d.Directions).FirstAsync(d => d.Id == departmentId);
            var direction = department.AddDirection(name ?? $"Yo'nalish {suffix}", $"D{suffix}");
            await db.SaveChangesAsync();
            return direction.Id;
        });

    /// <summary>Toshkent markazidagi korxona (Amir Temur 108), radius 150 m, noyob STIR.</summary>
    public static Task<Company> CreateCompanyAsync(
        this ApiFactory factory, double lat = 41.3111, double lng = 69.2797, int radiusM = 150, string? name = null) =>
        factory.WithDbAsync(async db =>
        {
            var tin = Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
            var company = Company.Create(
                name ?? $"Korxona {tin}", tin, "IT xizmatlari", "Toshkent, Amir Temur 108",
                new GeoPoint(lat, lng), radiusM, "Islomov B.", RandomPhone());
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            return company;
        });

    /// <summary>Faol amaliyot davri: bugundan −14..+30 kun, standart qoidalar, Du–Sha; berilgan guruhlar biriktiriladi.</summary>
    public static Task<PracticePeriod> CreateActivePeriodAsync(
        this ApiFactory factory, TestGroup group, Guid createdByUserId, params Guid[] extraGroupIds) =>
        factory.WithDbAsync(async db =>
        {
            var clock = factory.Services.GetRequiredService<IClock>();
            var today = Amaliyotchi.Domain.Common.PracticeTime.LocalDate(clock.UtcNow);
            var period = PracticePeriod.Create(
                $"Amaliyot {Suffix()}", group.AcademicYearId, today.AddDays(-14), today.AddDays(30),
                createdByUserId, CheckInRules.Default, WorkDays.MondayToSaturday, 36, dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            foreach (var extra in extraGroupIds)
                period.AttachGroup(extra);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    /// <summary>Tasdiqlangan ariza (check-in uchun shart).</summary>
    public static Task<PracticeApplication> CreateApprovedApplicationAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, Company company, Guid tutorUserId) =>
        factory.WithDbAsync(async db =>
        {
            var now = factory.Services.GetRequiredService<IClock>().UtcNow;
            var application = PracticeApplication.Create(student.Id, period.Id, company.Id, company.RadiusM, null, now.AddDays(-10));
            application.Approve(tutorUserId, company.RadiusM, Enumerable.Range(0, PracticeApplication.ChecklistItemCount), "OK", now.AddDays(-9));
            db.PracticeApplications.Add(application);
            await db.SaveChangesAsync();
            return application;
        });

    /// <summary>Faylni saqlovchiga yozib, <c>StoredFile</c> yozuvini yaratadi.</summary>
    public static async Task<StoredFile> CreateStoredFileAsync(
        this ApiFactory factory, Guid? uploadedByUserId, StoredFileKind kind = StoredFileKind.Contract,
        string fileName = "shartnoma.pdf", string contentType = "application/pdf", byte[]? content = null)
    {
        content ??= Encoding.UTF8.GetBytes("%PDF-1.4 test");
        var storage = factory.Services.GetRequiredService<IFileStorage>();
        var key = await storage.SaveAsync(new MemoryStream(content), fileName, contentType);
        var now = factory.Services.GetRequiredService<IClock>().UtcNow;
        var file = StoredFile.Create(kind, fileName, contentType, content.Length, key, now, uploadedByUserId);

        await factory.WithDbAsync(async db =>
        {
            db.StoredFiles.Add(file);
            await db.SaveChangesAsync();
        });

        return file;
    }

    public static async Task<HttpClient> LoginAsAdminAsync(this ApiFactory factory)
    {
        var admin = await factory.CreateAdminAsync();
        return await factory.LoginAsync(admin);
    }

    public static async Task<HttpClient> LoginAsTutorAsync(this ApiFactory factory, TestGroup? group = null)
    {
        var tutor = await factory.CreateTutorAsync(group);
        return await factory.LoginAsync(tutor);
    }

    /// <summary>Talaba sifatida (Telegram orqali) kirgan klient.</summary>
    public static async Task<HttpClient> LoginAsStudentAsync(this ApiFactory factory, TestUser student)
    {
        var (client, _) = await factory.TelegramLoginAsync(student.TelegramId!.Value);
        return client;
    }

    /// <summary>Parol bilan kiradi va Bearer o'rnatilgan klient qaytaradi.</summary>
    public static async Task<HttpClient> LoginAsync(this ApiFactory factory, TestUser user)
    {
        var (client, _) = await factory.LoginWithResultAsync(user);
        return client;
    }

    public static async Task<(HttpClient Client, AuthResultDto Auth)> LoginWithResultAsync(this ApiFactory factory, TestUser user)
    {
        var client = factory.CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new { user.HemisId, user.Password });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Login {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var auth = await response.Content.ReadAsync<AuthResultDto>()
            ?? throw new InvalidOperationException("Login javobi bo'sh.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    /// <summary>Talaba sifatida Telegram orqali kiradi: test bot tokeni bilan haqiqiy initData yasaladi.</summary>
    public static async Task<(HttpClient Client, AuthResultDto Auth)> TelegramLoginAsync(this ApiFactory factory, long telegramId)
    {
        var client = factory.CreateClient();
        var initData = TelegramInitDataFactory.Create(telegramId, ApiFactory.TelegramBotToken, DateTimeOffset.UtcNow);
        var response = await client.PostJsonAsync("/api/auth/telegram", new { initData });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Telegram login {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var auth = await response.Content.ReadAsync<AuthResultDto>()
            ?? throw new InvalidOperationException("Telegram login javobi bo'sh.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    public static string RandomPhone() =>
        "+9989" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    /// <summary>12 xonali noyob HEMIS ID (3410 + 8 tasodifiy raqam).</summary>
    public static string RandomHemisId() =>
        "3410" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);

    private const string TestAcademicYear = "2026-2027";

    private static string Suffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<TestUser> CreatePasswordUserAsync(this ApiFactory factory, UserRole role, string fullName, Guid? facultyId)
    {
        var phone = RandomPhone();
        var hemisId = RandomHemisId();

        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.CreateWithPassword(fullName, hemisId, phone, hasher.Hash(DefaultPassword), role, facultyId);

        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

        return new TestUser(user.Id, user.FullName, role, phone, DefaultPassword, facultyId, null, GroupId: null, HemisId: hemisId);
    }
}
