using System.Net;
using Amaliyotchi.Api.Infrastructure;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Infrastructure.Persistence.Seeding;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Amaliyotchi.IntegrationTests.Persistence;

/// <summary>Har test o'z bazasida (bitta PostGIS konteyneri, alohida <c>CREATE DATABASE</c>): demo seed + purge umumiy
/// bazani (ApiFixture) buzmasin va testlar bir-biriga ta'sir qilmasin.</summary>
public sealed class DemoPurgeFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private readonly List<ApiFactory> _factories = [];

    public Task InitializeAsync() => _postgres.StartAsync();

    /// <summary>Yangi baza → migratsiya + asosiy seed → demo seed (ikki marta: ikkinchisi kundaliklarga PDF biriktiradi —
    /// diskda haqiqiy fayllar paydo bo'ladi, xuddi qayta ishga tushirilgan stenddagidek).</summary>
    public async Task<ApiFactory> CreateSeededFactoryAsync()
    {
        var database = "purge_" + Guid.NewGuid().ToString("N")[..12];
        await using (var connection = new NpgsqlConnection(_postgres.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = database }.ConnectionString;
        var factory = new ApiFactory(connectionString);
        _factories.Add(factory);
        await factory.InitializeDatabaseAsync();
        await DatabaseInitializer.MigrateAndSeedAsync(factory.Services, includeDemo: true);
        await DatabaseInitializer.MigrateAndSeedAsync(factory.Services, includeDemo: true);
        return factory;
    }

    public async Task DisposeAsync()
    {
        foreach (var factory in _factories)
            await factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public sealed class DemoDataPurgeTests(DemoPurgeFixture fixture) : IClassFixture<DemoPurgeFixture>
{
    [Fact]
    public async Task DryRun_DemoSeed_SanaydiVaHechNarsaOzgartirmaydi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();
        var before = await SnapshotAsync(factory);

        var report = await AnalyzeAsync(factory);

        report.Found.Should().BeTrue();
        report.Applied.Should().BeFalse();
        report.Blockers.Should().BeEmpty();
        report.Count("users").Should().Be(41, "3 tyutor + 38 talaba");
        report.Count("student_profiles").Should().Be(38);
        report.Count("faculties").Should().Be(4);
        report.Count("departments").Should().Be(4);
        report.Count("directions").Should().Be(4);
        report.Count("student_groups").Should().Be(4);
        report.Count("tutor_faculties").Should().Be(3);
        report.Count("tutor_scopes").Should().Be(4);
        report.Count("tutor_assignments").Should().Be(4);
        report.Count("companies").Should().Be(6);
        report.Count("practice_periods").Should().Be(2);
        report.Count("practice_period_groups").Should().Be(4);
        report.Count("practice_applications").Should().Be(38);
        report.Count("practice_grades").Should().Be(5);
        report.Count("leave_requests").Should().Be(before["leave_requests"]);
        report.Count("daily_attendances").Should().Be(before["daily_attendances"]).And.BePositive();
        report.Count("attendance_events").Should().Be(before["attendance_events"]).And.BePositive();
        report.Count("diary_entries").Should().Be(before["diary_entries"]).And.BePositive();
        report.Count("diary_attachments").Should().Be(before["diary_attachments"]).And.BePositive();
        report.Count("stored_files").Should().Be(before["stored_files"]).And.Be(report.Count("diary_attachments"));
        report.Count("audit_logs").Should().BePositive();

        // CLI dry-run (to'liq yo'l: konfiguratsiya, DI, hisobot) — exit 0, baza o'zgarmaydi.
        var (exit, output, _) = await RunCliAsync(factory, PurgeDemoCommand.Name);
        exit.Should().Be(PurgeDemoCommand.ExitOk);
        output.Should().Contain("DRY-RUN").And.Contain("To'siqlar yo'q");

        (await SnapshotAsync(factory)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Apply_DemoOchadi_HaqiqiyMalumotVaAsosiySeedSaqlanadi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();

        // Boshqa fakultetda haqiqiy ma'lumot: tyutor, talaba, korxona, davr, tasdiqlangan ariza, fayl.
        var group = await factory.CreateGroupAsync();
        var tutor = await factory.CreateTutorAsync(group, "Haqiqiy Tyutor");
        var student = await factory.CreateStudentAsync(group: group, fullName: "Haqiqiy Talaba");
        var company = await factory.CreateCompanyAsync(name: "Haqiqiy Korxona MChJ");
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).FirstAsync());
        var period = await factory.CreateActivePeriodAsync(group, adminId);
        var application = await factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id);
        var realFile = await factory.CreateStoredFileAsync(student.Id);

        var before = await SnapshotAsync(factory);
        var (settings, holidays, years) = await factory.WithDbAsync(async db =>
            (await db.AppSettings.CountAsync(), await db.Holidays.CountAsync(), await db.AcademicYears.IgnoreQueryFilters().CountAsync()));

        var report = await PurgeAsync(factory);

        report.Applied.Should().BeTrue();
        report.Blockers.Should().BeEmpty();
        (await AnalyzeAsync(factory)).Found.Should().BeFalse("demo ma'lumot qolmadi");

        var after = await SnapshotAsync(factory);
        foreach (var row in report.Counts.Where(c => c.Table != "audit_logs"))
            after[row.Table].Should().Be(before[row.Table] - row.Count, row.Table);

        await factory.WithDbAsync(async db =>
        {
            var users = db.Users.IgnoreQueryFilters();
            (await users.AnyAsync(u => u.PhoneNumber == DemoDataSeeder.TutorPhone)).Should().BeFalse();
            (await db.StudentProfiles.IgnoreQueryFilters().AnyAsync(p => p.HemisId == DemoDataSeeder.StudentHemisId)).Should().BeFalse();
            (await db.Faculties.IgnoreQueryFilters().AnyAsync(f => f.Code == "AT")).Should().BeFalse();
            (await db.PracticePeriods.IgnoreQueryFilters().AnyAsync(p => p.Name == DemoDataSeeder.PeriodName)).Should().BeFalse();
            (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Tin == "304512889")).Should().BeFalse();

            // Haqiqiy ma'lumot to'liq joyida.
            (await users.CountAsync(u => u.Id == tutor.Id || u.Id == student.Id || u.Id == adminId)).Should().Be(3);
            (await db.Faculties.AnyAsync(f => f.Id == group.FacultyId)).Should().BeTrue();
            (await db.StudentGroups.AnyAsync(g => g.Id == group.GroupId)).Should().BeTrue();
            (await db.TutorAssignments.AnyAsync(a => a.TutorUserId == tutor.Id)).Should().BeTrue();
            (await db.Companies.AnyAsync(c => c.Id == company.Id)).Should().BeTrue();
            (await db.PracticePeriods.AnyAsync(p => p.Id == period.Id)).Should().BeTrue();
            (await db.PracticeApplications.AnyAsync(a => a.Id == application.Id)).Should().BeTrue();
            (await db.StoredFiles.AnyAsync(f => f.Id == realFile.Id)).Should().BeTrue();

            // DbSeeder ma'lumoti tegilmagan.
            (await db.AppSettings.CountAsync()).Should().Be(settings);
            (await db.Holidays.CountAsync()).Should().Be(holidays);
            (await db.AcademicYears.IgnoreQueryFilters().CountAsync()).Should().Be(years);
            (await users.AnyAsync(u => u.HemisId == ApiFactory.SeedAdminHemisId && u.Role == UserRole.Admin)).Should().BeTrue();

            // Purge'ning o'zi audit'da qoladi.
            (await db.AuditLogs.CountAsync(a => a.EntityName == "DemoData" && a.Action == AuditAction.Deleted)).Should().Be(1);
        });

        var storage = factory.Services.GetRequiredService<Amaliyotchi.Application.Common.Interfaces.IFileStorage>();
        (await storage.OpenReadAsync(realFile.StoragePath)).Should().NotBeNull("haqiqiy fayl diskda qoladi");
    }

    [Fact]
    public async Task Apply_DemoGuruhdaHaqiqiyTalaba_RadEtadi_HechNarsaOchmaydi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();
        var demoGroup = await DemoGroupAsync(factory, "412-22");
        await factory.CreateStudentAsync(group: demoGroup, fullName: "Haqiqiy Qo'shilgan Talaba");
        var before = await SnapshotAsync(factory);

        var report = await PurgeAsync(factory);

        report.Applied.Should().BeFalse();
        report.Blockers.Should().ContainSingle(b => b.Contains("412-22") && b.Contains("Haqiqiy Qo'shilgan Talaba"));
        (await SnapshotAsync(factory)).Should().BeEquivalentTo(before);

        var (exit, output, _) = await RunCliAsync(factory, PurgeDemoCommand.Name, "--apply");
        exit.Should().Be(PurgeDemoCommand.ExitBlocked);
        output.Should().Contain("TO'SIQLAR").And.Contain("Haqiqiy Qo'shilgan Talaba");
        (await SnapshotAsync(factory)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Apply_HaqiqiyTyutorDemoFakultetda_YokiHaqiqiyArizaDemoKorxonada_RadEtadi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();
        var demoGroup = await DemoGroupAsync(factory, "413-22");

        // Haqiqiy tyutor demo fakultetga (va boshqa haqiqiy guruhga) biriktirilgan.
        var realGroup = await factory.CreateGroupAsync();
        var realTutor = User.CreateWithPassword(
            "Begona Tyutor", TestClients.RandomHemisId(), TestClients.RandomPhone(), "hash", UserRole.Tutor, demoGroup.FacultyId);
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(realTutor);
            await db.SaveChangesAsync();
        });

        // Haqiqiy talaba demo korxonaga ariza bergan.
        var realStudent = await factory.CreateStudentAsync(group: realGroup, fullName: "Begona Talaba");
        var adminId = await factory.WithDbAsync(db => db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).FirstAsync());
        var period = await factory.CreateActivePeriodAsync(realGroup, adminId);
        var demoCompany = await factory.WithDbAsync(db => db.Companies.FirstAsync(c => c.Tin == "203112340"));
        await factory.CreateApprovedApplicationAsync(realStudent, period, demoCompany, adminId);
        var before = await SnapshotAsync(factory);

        var report = await PurgeAsync(factory);

        report.Applied.Should().BeFalse();
        report.Blockers.Should().Contain(b => b.Contains("Begona Tyutor"));
        report.Blockers.Should().Contain(b => b.Contains("practice_applications") && b.Contains("demo bo'lmagan talaba"));
        (await SnapshotAsync(factory)).Should().BeEquivalentTo(before);
        (await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == realTutor.Id))).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_IkkinchiMarta_TopilmadiExit0()
    {
        var factory = await fixture.CreateSeededFactoryAsync();

        (await PurgeAsync(factory)).Applied.Should().BeTrue();
        var snapshot = await SnapshotAsync(factory);

        var second = await PurgeAsync(factory);
        second.Found.Should().BeFalse();
        second.Applied.Should().BeFalse();
        second.TotalRows.Should().Be(0);

        var (exit, output, _) = await RunCliAsync(factory, PurgeDemoCommand.Name, "--apply");
        exit.Should().Be(PurgeDemoCommand.ExitOk);
        output.Should().Contain("Demo ma'lumot topilmadi");
        (await SnapshotAsync(factory)).Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task Apply_DemoFayllarDiskdanOchadi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();
        var paths = await factory.WithDbAsync(db => db.StoredFiles.Select(f => f.StoragePath).ToListAsync());
        paths.Should().NotBeEmpty();
        paths.Should().OnlyContain(p => File.Exists(Path.Combine(factory.StorageRoot, p)));

        var report = await PurgeAsync(factory);

        report.FilesDeleted.Should().Be(paths.Count);
        report.FileErrors.Should().BeEmpty();
        paths.Should().OnlyContain(p => !File.Exists(Path.Combine(factory.StorageRoot, p)));
        (await factory.WithDbAsync(db => db.StoredFiles.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Apply_KeyinApiNormalIshlaydi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();

        // Demo tyutor purge'dan oldin kira oladi.
        var anonymous = factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login",
            new { HemisId = DemoDataSeeder.TutorHemisId, Password = DemoDataSeeder.TutorPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var (exit, _, error) = await RunCliAsync(factory, PurgeDemoCommand.Name, "--apply");
        exit.Should().Be(PurgeDemoCommand.ExitOk, error);

        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostJsonAsync("/api/auth/login",
            new { HemisId = DemoDataSeeder.TutorHemisId, Password = DemoDataSeeder.TutorPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "demo tyutor o'chirilgan (noto'g'ri login — 403, AuthSessionService)");

        var admin = factory.CreateClient();
        var login = await admin.PostJsonAsync("/api/auth/login",
            new { HemisId = ApiFactory.SeedAdminHemisId, Password = ApiFactory.SeedAdminPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadAsync<Amaliyotchi.Application.Features.Auth.AuthResultDto>();
        admin.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var dashboard = await admin.GetAsync("/api/admin/dashboard");
        dashboard.StatusCode.Should().Be(HttpStatusCode.OK, await dashboard.Content.ReadAsStringAsync());
        (await admin.GetAsync("/api/admin/faculties")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cli_ProductionMuhitida_ProductionConfigValidatorIshlaydi()
    {
        var factory = await fixture.CreateSeededFactoryAsync();
        var before = await SnapshotAsync(factory);

        // Testcontainers paroli ("amaliyotchi") — production'da taqiqlangan default parol.
        var (exit, _, error) = await RunCliInEnvironmentAsync(factory, "Production", PurgeDemoCommand.Name, "--apply");

        exit.Should().Be(PurgeDemoCommand.ExitError);
        error.Should().Contain("Production sozlamalari xavfsiz emas");
        (await SnapshotAsync(factory)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Cli_NomalumArgument_Exit64()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = await PurgeDemoCommand.RunAsync([PurgeDemoCommand.Name, "--force"], output, error);

        exit.Should().Be(PurgeDemoCommand.ExitUsage);
        error.ToString().Should().Contain("--force");
    }

    // ------------------------------------------------------------------ yordamchilar

    private static Task<DemoPurgeReport> AnalyzeAsync(ApiFactory factory) =>
        WithPurgerAsync(factory, p => p.AnalyzeAsync());

    private static Task<DemoPurgeReport> PurgeAsync(ApiFactory factory) =>
        WithPurgerAsync(factory, p => p.PurgeAsync());

    private static async Task<DemoPurgeReport> WithPurgerAsync(ApiFactory factory, Func<DemoDataPurger, Task<DemoPurgeReport>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<DemoDataPurger>());
    }

    private static Task<(int Exit, string Output, string Error)> RunCliAsync(ApiFactory factory, params string[] args) =>
        RunCliInEnvironmentAsync(factory, "Testing", args);

    /// <summary>CLI'ni API bilan bir xil konfiguratsiyada (test bazasi, fayl papkasi) ishga tushiradi.</summary>
    private static async Task<(int Exit, string Output, string Error)> RunCliInEnvironmentAsync(
        ApiFactory factory, string environment, params string[] args)
    {
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = await PurgeDemoCommand.RunAsync(args, output, error, builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Jwt:SigningKey"] = ApiFactory.JwtSigningKey,
                ["Storage:RootPath"] = factory.StorageRoot,
                ["Telegram:BotEnabled"] = "false",
                ["Serilog:MinimumLevel:Default"] = "Warning"
            }), environment);

        return (exit, output.ToString(), error.ToString());
    }

    private static Task<TestGroup> DemoGroupAsync(ApiFactory factory, string name) =>
        factory.WithDbAsync(async db =>
        {
            var chain = await (from g in db.StudentGroups
                               join d in db.Directions on g.DirectionId equals d.Id
                               join dept in db.Departments on d.DepartmentId equals dept.Id
                               where g.Name == name
                               select new TestGroup(g.AcademicYearId, dept.FacultyId, dept.Id, d.Id, g.Id, g.Name, g.Course))
                .SingleAsync();
            return chain;
        });

    private static Task<Dictionary<string, int>> SnapshotAsync(ApiFactory factory) =>
        factory.WithDbAsync(async db => new Dictionary<string, int>
        {
            ["users"] = await db.Users.IgnoreQueryFilters().CountAsync(),
            ["student_profiles"] = await db.StudentProfiles.IgnoreQueryFilters().CountAsync(),
            ["refresh_tokens"] = await db.RefreshTokens.CountAsync(),
            ["audit_logs"] = await db.AuditLogs.CountAsync(),
            ["academic_years"] = await db.AcademicYears.IgnoreQueryFilters().CountAsync(),
            ["faculties"] = await db.Faculties.IgnoreQueryFilters().CountAsync(),
            ["departments"] = await db.Departments.IgnoreQueryFilters().CountAsync(),
            ["directions"] = await db.Directions.IgnoreQueryFilters().CountAsync(),
            ["student_groups"] = await db.StudentGroups.IgnoreQueryFilters().CountAsync(),
            ["tutor_faculties"] = await db.TutorFaculties.CountAsync(),
            ["tutor_scopes"] = await db.TutorScopes.IgnoreQueryFilters().CountAsync(),
            ["tutor_assignments"] = await db.TutorAssignments.IgnoreQueryFilters().CountAsync(),
            ["companies"] = await db.Companies.IgnoreQueryFilters().CountAsync(),
            ["practice_periods"] = await db.PracticePeriods.IgnoreQueryFilters().CountAsync(),
            ["practice_period_groups"] = await db.PracticePeriodGroups.CountAsync(),
            ["practice_applications"] = await db.PracticeApplications.CountAsync(),
            ["daily_attendances"] = await db.DailyAttendances.CountAsync(),
            ["attendance_events"] = await db.AttendanceEvents.CountAsync(),
            ["diary_entries"] = await db.DiaryEntries.CountAsync(),
            ["diary_attachments"] = await db.DiaryAttachments.CountAsync(),
            ["leave_requests"] = await db.LeaveRequests.CountAsync(),
            ["practice_grades"] = await db.PracticeGrades.CountAsync(),
            ["app_settings"] = await db.AppSettings.CountAsync(),
            ["holidays"] = await db.Holidays.IgnoreQueryFilters().CountAsync(),
            ["document_templates"] = await db.DocumentTemplates.IgnoreQueryFilters().CountAsync(),
            ["stored_files"] = await db.StoredFiles.CountAsync()
        });
}
