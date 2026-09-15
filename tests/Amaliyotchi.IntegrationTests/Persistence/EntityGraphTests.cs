using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Organization;
using Amaliyotchi.Domain.ValueObjects;
using Amaliyotchi.Infrastructure.Persistence.Configurations;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Amaliyotchi.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public sealed class EntityGraphTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    /// <summary>1b topgan xato: Id oldindan (UUIDv7) berilgan, navigatsiya orqali qo'shilgan entity
    /// EF'da "Modified" bo'lib qolardi → UPDATE 0 qator → DbUpdateConcurrencyException (409).
    /// <c>ValueGeneratedNever</c> bilan graf to'g'ri INSERT qilinadi.</summary>
    [Fact]
    public async Task Faculty_AddDirection_AddGroup_Graf_SaqlanadiVa409Emas()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var year = await Factory.WithDbAsync(async db =>
        {
            var y = AcademicYear.Create($"Y-{suffix}", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
            db.AcademicYears.Add(y);
            await db.SaveChangesAsync();
            return y;
        });

        Guid facultyId = Guid.Empty, departmentId = Guid.Empty, directionId = Guid.Empty, groupId = Guid.Empty;
        var act = () => Factory.WithDbAsync(async db =>
        {
            var faculty = Faculty.Create($"Fakultet {suffix}", $"F{suffix}");
            db.Faculties.Add(faculty);
            await db.SaveChangesAsync();

            // Kuzatilayotgan (tracked) fakultetga navigatsiya orqali yangi entity'lar qo'shiladi — Add chaqirilmaydi.
            var department = faculty.AddDepartment("Dasturiy injiniring kafedrasi", $"K{suffix}");
            var direction = department.AddDirection("Dasturiy injiniring", $"D{suffix}");
            var group = direction.AddGroup("412-22", 3, year.Id);
            await db.SaveChangesAsync();

            facultyId = faculty.Id;
            departmentId = department.Id;
            directionId = direction.Id;
            groupId = group.Id;
        });

        await act.Should().NotThrowAsync<DbUpdateConcurrencyException>();

        await Factory.WithDbAsync(async db =>
        {
            (await db.Departments.AnyAsync(d => d.Id == departmentId && d.FacultyId == facultyId)).Should().BeTrue();
            (await db.Directions.AnyAsync(d => d.Id == directionId && d.DepartmentId == departmentId)).Should().BeTrue();
            (await db.StudentGroups.AnyAsync(g => g.Id == groupId && g.DirectionId == directionId)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task User_IssueRefreshToken_NavigatsiyaOrqali_InsertBoladi()
    {
        var tutor = await Factory.CreateTutorAsync();

        await Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.Include(u => u.RefreshTokens).FirstAsync(u => u.Id == tutor.Id);
            var token = user.IssueRefreshToken("tok-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddDays(1), "127.0.0.1");
            await db.SaveChangesAsync();

            (await db.RefreshTokens.AnyAsync(t => t.Id == token.Id)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task Company_GeoPoint_SaqlanadiVaOqiladi_PostgisMasofaIshlaydi()
    {
        // Amir Temur 108 (41.3111, 69.2797). 150 m radius.
        var company = await Factory.CreateCompanyAsync(41.3111, 69.2797, 150);

        await Factory.WithDbAsync(async db =>
        {
            var loaded = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == company.Id);
            loaded.Location.Latitude.Should().BeApproximately(41.3111, 1e-9);
            loaded.Location.Longitude.Should().BeApproximately(69.2797, 1e-9);

            // Talaba nuqtasi ~100 m shimolda (0.0009° kenglik ≈ 100 m).
            var near = new Point(69.2797, 41.3120) { SRID = 4326 };
            var far = new Point(69.2900, 41.3111) { SRID = 4326 };

            var within = await db.Companies
                .Where(c => c.Id == company.Id)
                .Select(c => EF.Property<Point>(c, CompanyConfiguration.LocationGeog).IsWithinDistance(near, 200))
                .SingleAsync();
            within.Should().BeTrue();

            var withinFar = await db.Companies
                .Where(c => c.Id == company.Id)
                .Select(c => EF.Property<Point>(c, CompanyConfiguration.LocationGeog).IsWithinDistance(far, 200))
                .SingleAsync();
            withinFar.Should().BeFalse();

            var distance = await db.Companies
                .Where(c => c.Id == company.Id)
                .Select(c => EF.Property<Point>(c, CompanyConfiguration.LocationGeog).Distance(near))
                .SingleAsync();
            distance.Should().BeInRange(90, 110, "geography masofasi metrda");

            // Domain Haversine bilan mos.
            new GeoPoint(41.3120, 69.2797).DistanceMetersTo(loaded.Location).Should().BeApproximately(distance, 2);
        });
    }

    [Fact]
    public async Task Company_Relocate_HisoblanganUstun_Yangilanadi()
    {
        var company = await Factory.CreateCompanyAsync(41.3111, 69.2797, 150);

        await Factory.WithDbAsync(async db =>
        {
            var tracked = await db.Companies.SingleAsync(c => c.Id == company.Id);
            tracked.Relocate(41.3265, 69.2285);
            await db.SaveChangesAsync();
        });

        await Factory.WithDbAsync(async db =>
        {
            var target = new Point(69.2285, 41.3265) { SRID = 4326 };
            var distance = await db.Companies
                .Where(c => c.Id == company.Id)
                .Select(c => EF.Property<Point>(c, CompanyConfiguration.LocationGeog).Distance(target))
                .SingleAsync();
            distance.Should().BeLessThan(1);
        });
    }

    [Fact]
    public async Task AttendanceEvent_GeoPoint_ComplexType_Saqlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        var company = await Factory.CreateCompanyAsync();

        var eventId = await Factory.WithDbAsync(async db =>
        {
            var e = AttendanceEvent.Record(
                student.Id, company.Id, new DateOnly(2026, 10, 12), AttendanceEventKind.CheckIn,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new GeoPoint(41.3112, 69.2798), 12, 45, 150,
                CheckInVerdict.Accept());
            db.AttendanceEvents.Add(e);
            await db.SaveChangesAsync();
            return e.Id;
        });

        await Factory.WithDbAsync(async db =>
        {
            var loaded = await db.AttendanceEvents.AsNoTracking().SingleAsync(e => e.Id == eventId);
            loaded.Location.Latitude.Should().BeApproximately(41.3112, 1e-9);
            loaded.Accepted.Should().BeTrue();
            loaded.RejectReason.Should().Be(CheckInRejectReason.None);

            // IAuditExempt — audit yozuvi yo'q.
            (await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(AttendanceEvent) && a.EntityId == eventId.ToString()))
                .Should().BeFalse();
        });
    }

    /// <summary>Npgsql timestamptz faqat UTC DateTimeOffset qabul qiladi; Toshkent (+05:00) qiymati
    /// (PracticeTime.At) model konvensiyasi bilan UTC'ga o'tkaziladi — moment saqlanadi.</summary>
    [Fact]
    public async Task DateTimeOffset_ToshkentOffset_UtcgaOtkazibSaqlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        var period = await Factory.CreateActivePeriodAsync(group, admin.Id);

        var date = new DateOnly(2026, 10, 12);
        var checkInAt = PracticeTime.At(date, new TimeOnly(9, 2));
        checkInAt.Offset.Should().Be(TimeSpan.FromHours(5));

        var id = await Factory.WithDbAsync(async db =>
        {
            var attendance = DailyAttendance.CheckIn(student.Id, period.Id, date, checkInAt, 45, 12, CheckInVerdict.Accept());
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance.Id;
        });

        await Factory.WithDbAsync(async db =>
        {
            var loaded = await db.DailyAttendances.AsNoTracking().SingleAsync(a => a.Id == id);
            loaded.CheckInAt.Should().Be(checkInAt, "bir xil moment");
            loaded.CheckInAt!.Value.Offset.Should().Be(TimeSpan.Zero, "bazadan UTC qaytadi");
            PracticeTime.Hm(loaded.CheckInAt.Value).Should().Be("09:02");

            // Parametr ham konvertatsiya qilinadi.
            (await db.DailyAttendances.AnyAsync(a => a.Id == id && a.CheckInAt == checkInAt)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task Company_Yaratilganda_AuditYoziladi_XminBor()
    {
        var company = await Factory.CreateCompanyAsync();

        await Factory.WithDbAsync(async db =>
        {
            (await db.AuditLogs.AnyAsync(a => a.EntityName == nameof(Company) && a.EntityId == company.Id.ToString()))
                .Should().BeTrue();

            var xmin = await db.Companies
                .Where(c => c.Id == company.Id)
                .Select(c => EF.Property<uint>(c, "xmin"))
                .SingleAsync();
            xmin.Should().BeGreaterThan(0);
        });
    }
}
