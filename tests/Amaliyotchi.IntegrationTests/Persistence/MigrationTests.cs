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
    public async Task Nomlar_SnakeCase_VaPrefikslarButun()
    {
        await Factory.WithDbAsync(async db =>
        {
            var tables = await db.Database
                .SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
                .ToListAsync();

            tables.Should().Contain(["users", "student_profiles", "tutor_assignments", "companies", "practice_periods",
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
