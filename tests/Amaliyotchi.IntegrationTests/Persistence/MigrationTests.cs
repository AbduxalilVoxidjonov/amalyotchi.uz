using Amaliyotchi.Domain.Students;
using Amaliyotchi.Infrastructure.Persistence.Migrations;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public sealed class MigrationTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Migratsiya_Qollangan_PendingMigratsiyaVaModelOzgarishiYoq()
    {
        await Factory.WithDbAsync(async db =>
        {
            var applied = await db.Database.GetAppliedMigrationsAsync();
            applied.Should().ContainSingle(m => m.EndsWith("_Initial", StringComparison.Ordinal));

            (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();

            // `dotnet ef migrations has-pending-model-changes` bilan bir xil tekshiruv.
            db.Database.HasPendingModelChanges().Should().BeFalse("model va snapshot bir xil bo'lishi kerak");
        });
    }

    [Fact]
    public async Task Postgis_Kengaytmasi_Ornatilgan()
    {
        var count = await Factory.WithDbAsync(db =>
            db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'postgis'").SingleAsync());

        count.Should().Be(1);
    }

    [Fact]
    public async Task Companies_LocationGeog_GeographyUstuni_VaGistIndeks()
    {
        await Factory.WithDbAsync(async db =>
        {
            var columnType = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT format_type(a.atttypid, a.atttypmod) AS \"Value\" FROM pg_attribute a " +
                    "JOIN pg_class c ON c.oid = a.attrelid WHERE c.relname = 'companies' AND a.attname = 'location_geog'")
                .SingleAsync();
            columnType.Should().Be("geography(Point,4326)");

            var generated = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT a.attgenerated::text AS \"Value\" FROM pg_attribute a " +
                    "JOIN pg_class c ON c.oid = a.attrelid WHERE c.relname = 'companies' AND a.attname = 'location_geog'")
                .SingleAsync();
            generated.Should().Be("s", "stored generated column");

            var indexDef = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'companies' AND indexname = 'ix_companies_location_geog'")
                .SingleAsync();
            indexDef.Should().Contain("USING gist").And.Contain("location_geog");
        });
    }

    [Fact]
    public async Task TutorScopes_Backfill_FaolBiriktiruvdanGuruhKolami_Idempotent()
    {
        // Ko'lamsiz eski uslubdagi biriktiruvlar: faol va faolsizlantirilgan.
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateAdminAsync("Backfill Tyutor"); // rol muhim emas — faqat users.id kerak
        await Factory.WithDbAsync(async db =>
        {
            var active = TutorAssignment.Create(tutor.Id, groupA.GroupId, groupA.AcademicYearId);
            var inactive = TutorAssignment.Create(tutor.Id, groupB.GroupId, groupB.AcademicYearId);
            inactive.Deactivate();
            db.TutorAssignments.AddRange(active, inactive);
            await db.SaveChangesAsync();
        });

        await Factory.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(TutorScopes.BackfillGroupScopesSql);
            await db.Database.ExecuteSqlRawAsync(TutorScopes.BackfillGroupScopesSql); // ikkinchi marta — takror yaratmaydi

            var scopes = await db.TutorScopes.Where(s => s.TutorUserId == tutor.Id).ToListAsync();
            var scope = scopes.Should().ContainSingle("faqat faol biriktiruv ko'chiriladi, bir marta").Subject;
            scope.Level.Should().Be(TutorScopeLevel.Group);
            scope.FacultyId.Should().Be(groupA.FacultyId);
            scope.DepartmentId.Should().Be(groupA.DepartmentId);
            scope.DirectionId.Should().Be(groupA.DirectionId);
            scope.StudentGroupId.Should().Be(groupA.GroupId);
            scope.IsActive.Should().BeTrue();
        });
    }

    [Fact]
    public async Task TutorFaculties_Backfill_UsersFacultyIdDanKochadi_Idempotent()
    {
        // Eski uslub: tyutor faqat users.faculty_id bilan, tutor_faculties qatori yo'q (backfill'dan oldingi holat).
        var tutor = await Factory.CreateTutorAsync();
        var admin = await Factory.CreateAdminAsync();
        var deletedTutor = await Factory.CreateTutorAsync();
        await Factory.WithDbAsync(async db =>
        {
            await db.TutorFaculties.Where(tf => tf.TutorUserId == tutor.Id || tf.TutorUserId == deletedTutor.Id).ExecuteDeleteAsync();
            var user = await db.Users.SingleAsync(u => u.Id == deletedTutor.Id);
            user.IsDeleted = true;
            user.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });

        await Factory.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(TutorFaculties.BackfillTutorFacultiesSql);
            await db.Database.ExecuteSqlRawAsync(TutorFaculties.BackfillTutorFacultiesSql); // ikkinchi marta — takror yaratmaydi

            var rows = await db.TutorFaculties.Where(tf => tf.TutorUserId == tutor.Id).ToListAsync();
            rows.Should().ContainSingle().Which.FacultyId.Should().Be(tutor.FacultyId!.Value);
            (await db.TutorFaculties.AnyAsync(tf => tf.TutorUserId == admin.Id)).Should().BeFalse("admin — fakultetsiz, rol tyutor emas");
            (await db.TutorFaculties.AnyAsync(tf => tf.TutorUserId == deletedTutor.Id)).Should().BeFalse("o'chirilgan tyutor ko'chirilmaydi");
        });
    }

    [Fact]
    public async Task Nomlar_SnakeCase_VaPrefikslarButun()
    {
        await Factory.WithDbAsync(async db =>
        {
            var tables = await db.Database
                .SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
                .ToListAsync();

            tables.Should().Contain(["users", "student_profiles", "tutor_assignments", "tutor_scopes", "tutor_faculties", "companies", "practice_periods",
                "practice_period_groups", "practice_applications", "daily_attendances", "attendance_events",
                "diary_entries", "diary_attachments", "leave_requests", "practice_grades", "app_settings",
                "holidays", "document_templates", "stored_files", "__migrations"]);

            var badNames = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT conname AS \"Value\" FROM pg_constraint WHERE conname LIKE 'p\\_k\\_%' OR conname LIKE 'f\\_k\\_%'")
                .ToListAsync();
            badNames.Should().BeEmpty("PK_/FK_ prefikslari pk_/fk_ bo'lishi kerak, p_k_ emas");
        });
    }
}
