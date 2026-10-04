using Amaliyotchi.Domain.Enums;
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

    /// <summary><c>RemoveTutors</c>: tarixiy rol 2 (tyutor) — refresh tokenlari bekor qilinadi, hisob soft-delete;
    /// SQL qayta ishga tushirilsa ham natija o'zgarmaydi; admin/talabaga tegilmaydi. Enum'da yo'q rolni o'qish xato bermaydi.</summary>
    [Fact]
    public async Task RemoveTutors_Rol2_SoftDelete_TokenlarBekor_Idempotent()
    {
        var bystander = await Factory.CreateAdminAsync();
        var legacyId = Guid.CreateVersion7();
        var tokenId = Guid.CreateVersion7();
        await Factory.WithDbAsync(async db =>
        {
            var hemis = TestClients.RandomHemisId();
            var phone = TestClients.RandomPhone();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (id, full_name, phone_number, hemis_id, password_hash, role, is_active, must_change_password,
                                   is_super_admin, created_at, is_deleted)
                VALUES ({legacyId}, 'Eski Tyutor', {phone}, {hemis}, 'hash', 2, true, false, false, now(), false)
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO refresh_tokens (id, user_id, token, expires_at, created_at)
                VALUES ({tokenId}, {legacyId}, {"legacy-" + tokenId}, now() + interval '7 days', now())
                """);

            // Enum'da yo'q rol (2) — o'qish xato bermaydi (EF int → enum cast).
            var legacy = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == legacyId);
            ((int)legacy.Role).Should().Be(2);
        });

        await Factory.WithDbAsync(async db =>
        {
            for (var i = 0; i < 2; i++)
            {
                await db.Database.ExecuteSqlRawAsync(RemoveTutors.RevokeTutorRefreshTokensSql);
                await db.Database.ExecuteSqlRawAsync(RemoveTutors.SoftDeleteTutorsSql);
            }

            var legacy = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == legacyId);
            legacy.IsDeleted.Should().BeTrue();
            legacy.DeletedAt.Should().NotBeNull();
            legacy.IsActive.Should().BeFalse();
            (await db.Users.AnyAsync(u => u.Id == legacyId)).Should().BeFalse("soft-delete filtri — login/ro'yxatlarda ko'rinmaydi");

            var token = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == tokenId);
            token.RevokedAt.Should().NotBeNull();

            var other = await db.Users.AsNoTracking().SingleAsync(u => u.Id == bystander.Id);
            other.IsDeleted.Should().BeFalse();
            other.IsActive.Should().BeTrue();
        });
    }

    /// <summary><c>RemoveTutors</c> va DbSeeder bir xil qoida: faol bosh admin yo'q bo'lsa — eng eski faol admin bosh admin;
    /// bor bo'lsa — hech narsa o'zgarmaydi (idempotent). Umumiy baza — tranzaksiya ichida, oxirida qaytariladi.</summary>
    [Fact]
    public async Task RemoveTutors_EngEskiFaolAdmin_BoshAdmin_Idempotent()
    {
        await Factory.CreateAdminAsync();
        await Factory.WithDbAsync(db => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            var before = await db.Users.Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync();
            before.Should().NotBeEmpty("DbSeeder birinchi adminni bosh admin qiladi");
            await db.Database.ExecuteSqlRawAsync(RemoveTutors.PromoteOldestAdminSql);
            (await db.Users.Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync())
                .Should().BeEquivalentTo(before, "bosh admin bor — o'zgarmaydi");

            await db.Database.ExecuteSqlRawAsync("UPDATE users SET is_super_admin = false");
            var oldest = await db.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin && u.IsActive)
                .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
                .Select(u => u.Id).FirstAsync();
            await db.Database.ExecuteSqlRawAsync(RemoveTutors.PromoteOldestAdminSql);
            await db.Database.ExecuteSqlRawAsync(RemoveTutors.PromoteOldestAdminSql);
            (await db.Users.AsNoTracking().Where(u => u.IsSuperAdmin).Select(u => u.Id).ToListAsync())
                .Should().Equal(oldest);

            await transaction.RollbackAsync();
        }));
    }

    [Fact]
    public async Task Nomlar_SnakeCase_VaPrefikslarButun()
    {
        await Factory.WithDbAsync(async db =>
        {
            var tables = await db.Database
                .SqlQueryRaw<string>("SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public'")
                .ToListAsync();

            tables.Should().Contain(["users", "student_profiles", "companies", "practice_periods",
                "practice_period_groups", "practice_applications", "daily_attendances", "attendance_events",
                "diary_entries", "diary_attachments", "leave_requests", "practice_grades", "app_settings",
                "holidays", "document_templates", "stored_files", "broadcast_messages", "broadcast_deliveries", "__migrations"]);
            tables.Should().NotContain(["tutor_assignments", "tutor_scopes", "tutor_faculties"], "RemoveTutors migratsiyasi");

            var badNames = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT conname AS \"Value\" FROM pg_constraint WHERE conname LIKE 'p\\_k\\_%' OR conname LIKE 'f\\_k\\_%'")
                .ToListAsync();
            badNames.Should().BeEmpty("PK_/FK_ prefikslari pk_/fk_ bo'lishi kerak, p_k_ emas");
        });
    }
}
