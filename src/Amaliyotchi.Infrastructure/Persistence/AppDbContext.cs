using System.Linq.Expressions;
using System.Reflection;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Auditing;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Amaliyotchi.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Faculty> Faculties => Set<Faculty>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Direction> Directions => Set<Direction>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TutorAssignment> TutorAssignments => Set<TutorAssignment>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<PracticePeriod> PracticePeriods => Set<PracticePeriod>();
    public DbSet<PracticePeriodGroup> PracticePeriodGroups => Set<PracticePeriodGroup>();
    public DbSet<PracticeApplication> PracticeApplications => Set<PracticeApplication>();
    public DbSet<DailyAttendance> DailyAttendances => Set<DailyAttendance>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<DiaryAttachment> DiaryAttachments => Set<DiaryAttachment>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PracticeGrade> PracticeGrades => Set<PracticeGrade>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    /// <summary>Npgsql <c>timestamptz</c> ga faqat UTC (offset 0) <see cref="DateTimeOffset"/> yozadi — Toshkent
    /// (+05:00) qiymati (<c>PracticeTime.At</c>) to'g'ridan-to'g'ri saqlansa <c>ArgumentException</c>. Shuning uchun
    /// barcha DateTimeOffset'lar yozishda UTC'ga o'tkaziladi (moment o'zgarmaydi); o'qishda UTC qaytadi.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Korxona koordinatalari: geography(Point,4326) + GIST, ST_DWithin/ST_Distance.
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyClientGeneratedIds(modelBuilder);
        ApplySoftDeleteFilter(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary><see cref="BaseEntity.Id"/> UUIDv7 sifatida ilovada beriladi. Buni EF'ga aniq aytmasak,
    /// u kalitni "bazada generatsiya qilinadi" deb hisoblaydi va navigatsiya orqali (masalan
    /// <c>faculty.AddDirection</c>, <c>user.IssueRefreshToken</c>) topilgan, Id'si to'ldirilgan yangi
    /// entity'ni <c>Modified</c> deb belgilaydi → <c>UPDATE</c> 0 qator → 409. <c>ValueGeneratedNever</c>
    /// bilan bunday entity to'g'ri <c>Added</c> bo'ladi.</summary>
    private static void ApplyClientGeneratedIds(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var id = entityType.FindProperty(nameof(BaseEntity.Id));
            if (id is not null)
                id.ValueGenerated = ValueGenerated.Never;
        }
    }

    /// <summary>O'chirilgan yozuv hech bir so'rovda ko'rinmaydi — buni har bir handler'da
    /// eslab qolish shart emas, filtr model darajasida o'rnatiladi.</summary>
    private static void ApplySoftDeleteFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(property), parameter);

            entityType.SetQueryFilter(filter);
        }
    }

    /// <summary>PostgreSQL'da nomlar snake_case bo'ladi: users, created_at, faculty_id.
    /// Qo'shimcha paketsiz, model yaratilayotganda bir marta qo'llaniladi.
    /// Complex type (GeoPoint) ustunlari konfiguratsiyada aniq nomlangan (<c>latitude</c>/<c>longitude</c>).</summary>
    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is not null)
                entity.SetTableName(ToSnakeCase(tableName));

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));

            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
        }
    }

    /// <summary>PascalCase → snake_case. Qisqartmalar bo'linmaydi: <c>PK_Users</c> → <c>pk_users</c>,
    /// <c>IX_Companies_LocationGeog</c> → <c>ix_companies_location_geog</c>, <c>HemisId</c> → <c>hemis_id</c>.</summary>
    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_' && NeedsSeparator(name, i))
                    builder.Append('_');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Katta harf oldiga <c>_</c> qo'yiladi, agar oldingi belgi kichik harf/raqam bo'lsa
    /// (<c>userId</c>) yoki katta harflar ketma-ketligi tugab kichik harf boshlansa (<c>IXCompanies</c> → <c>ix_companies</c>).</summary>
    private static bool NeedsSeparator(string name, int index)
    {
        var previous = name[index - 1];
        if (char.IsLower(previous) || char.IsDigit(previous))
            return true;

        return char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}

/// <summary>Yozishda UTC'ga normalizatsiya; o'qishda o'zgarishsiz (Npgsql doimo offset 0 qaytaradi).</summary>
internal sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    v => v.ToUniversalTime(),
    v => v);
