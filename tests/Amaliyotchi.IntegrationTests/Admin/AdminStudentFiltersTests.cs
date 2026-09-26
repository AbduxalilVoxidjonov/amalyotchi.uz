using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Nav;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>GET /api/admin/students</c> filtrlari (<c>facultyId</c>, <c>directionId</c>, <c>course</c>) va
/// <c>GET /api/admin/students/filters</c> variantlari.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentFiltersTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private sealed record Setup(string Tag, TestGroup A, TestGroup B, TestGroup Other, Guid[] InA, Guid[] InB, Guid[] InOther);

    /// <summary>Fakultet F: yo'nalish A (2-kurs, 1 talaba), yo'nalish B (4-kurs, 2 talaba); boshqa fakultet:
    /// 2-kurs guruh (1 talaba). Hamma talaba ismida noyob teg — umumiy bazada <c>q</c> bilan ajratish uchun.</summary>
    private async Task<Setup> SeedAsync()
    {
        var tag = $"Filtr{Guid.NewGuid():N}"[..14];
        var a = await Factory.CreateGroupAsync(course: 2);
        var b = await Factory.CreateGroupAsync(facultyId: a.FacultyId, course: 4);
        var other = await Factory.CreateGroupAsync(course: 2);

        var a1 = await Factory.CreateStudentAsync(group: a, fullName: $"{tag} Anvar");
        var b1 = await Factory.CreateStudentAsync(group: b, fullName: $"{tag} Botir");
        var b2 = await Factory.CreateStudentAsync(group: b, fullName: $"{tag} Bekzod");
        var o1 = await Factory.CreateStudentAsync(group: other, fullName: $"{tag} Olim");

        return new Setup(tag, a, b, other, [a1.Id], [b1.Id, b2.Id], [o1.Id]);
    }

    [Fact]
    public async Task Filtrlar_AlohidaVaKombinatsiyada_TotalTogri()
    {
        var s = await SeedAsync();
        var client = await Factory.LoginAsAdminAsync();

        async Task<Paged<StudentRow>> Get(string filters)
            => await client.GetPagedAsync<StudentRow>($"/api/admin/students?q={s.Tag}&pageSize=100{filters}");

        (await Get("")).Total.Should().Be(4);

        var byFaculty = await Get($"&facultyId={s.A.FacultyId}");
        byFaculty.Total.Should().Be(3);
        byFaculty.Items.Select(x => x.Id).Should().BeEquivalentTo(s.InA.Concat(s.InB));

        var byDirection = await Get($"&directionId={s.B.DirectionId}");
        byDirection.Total.Should().Be(2);
        byDirection.Items.Select(x => x.Id).Should().BeEquivalentTo(s.InB);

        var byCourse = await Get("&course=2");
        byCourse.Total.Should().Be(2);
        byCourse.Items.Select(x => x.Id).Should().BeEquivalentTo(s.InA.Concat(s.InOther));
        byCourse.Items.Should().OnlyContain(x => x.Course == 2);

        var facultyAndCourse = await Get($"&facultyId={s.A.FacultyId}&course=2");
        facultyAndCourse.Total.Should().Be(1);
        facultyAndCourse.Items.Single().Id.Should().Be(s.InA[0]);

        var all3 = await Get($"&facultyId={s.A.FacultyId}&directionId={s.B.DirectionId}&course=4");
        all3.Total.Should().Be(2);

        (await Get($"&facultyId={s.A.FacultyId}&directionId={s.B.DirectionId}&course=2")).Total.Should().Be(0);

        // q bilan birga: ism bo'yicha toraytirish + fakultet filtri.
        var qAndFaculty = await client.GetPagedAsync<StudentRow>(
            $"/api/admin/students?q={s.Tag} B&facultyId={s.A.FacultyId}");
        qAndFaculty.Total.Should().Be(2);
        qAndFaculty.Items.Select(x => x.Id).Should().BeEquivalentTo(s.InB);

        // Sahifalash filtrlangan total bo'yicha.
        var paged = await client.GetPagedAsync<StudentRow>(
            $"/api/admin/students?q={s.Tag}&facultyId={s.A.FacultyId}&page=2&pageSize=2");
        paged.Total.Should().Be(3);
        paged.Items.Should().HaveCount(1);

        // Filtr q'siz ham ishlaydi — faqat shu fakultet talabalari.
        var facultyOnly = await client.GetPagedAsync<StudentRow>($"/api/admin/students?facultyId={s.A.FacultyId}&pageSize=100");
        facultyOnly.Total.Should().Be(3);
    }

    [Fact]
    public async Task BoshqaFakultetYonalishi_BoshNatija_400Emas()
    {
        var s = await SeedAsync();
        var client = await Factory.LoginAsAdminAsync();

        var page = await client.GetPagedAsync<StudentRow>(
            $"/api/admin/students?facultyId={s.Other.FacultyId}&directionId={s.B.DirectionId}");

        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();

        (await client.GetPagedAsync<StudentRow>($"/api/admin/students?directionId={Guid.NewGuid()}")).Total.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task NotogriKurs_400(int course)
    {
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/admin/students?course={course}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("errors").TryGetProperty("Course", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Nav_TalabalarSoni_Filtrsiz()
    {
        var s = await SeedAsync();
        var client = await Factory.LoginAsAdminAsync();

        var nav = (await (await client.GetAsync("/api/admin/nav")).Content.ReadAsync<AdminNavDto>())!;
        var unfiltered = await client.GetPagedAsync<StudentRow>("/api/admin/students?pageSize=1");
        var filtered = await client.GetPagedAsync<StudentRow>($"/api/admin/students?pageSize=1&facultyId={s.A.FacultyId}");

        nav.Counts.Students.Should().Be(unfiltered.Total);
        filtered.Total.Should().Be(3);
        nav.Counts.Students.Should().BeGreaterThan(filtered.Total);
    }

    [Fact]
    public async Task Filters_200_Shakl()
    {
        var group = await Factory.CreateGroupAsync(course: 5);
        var client = await Factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/students/filters");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["faculties", "directions", "courses"]);

        var faculty = root.GetProperty("faculties").EnumerateArray()
            .Single(f => f.GetProperty("id").GetGuid() == group.FacultyId);
        faculty.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "name"]);

        var direction = root.GetProperty("directions").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == group.DirectionId);
        direction.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "name", "facultyId"]);
        direction.GetProperty("facultyId").GetGuid().Should().Be(group.FacultyId);

        root.GetProperty("courses").EnumerateArray().Select(c => c.GetInt32()).Should().Contain(5);
    }

    [Fact]
    public async Task Filters_YangiYozuvlarPaydoBoladi_OchirilganlarYoqoladi_Tartib()
    {
        var client = await Factory.LoginAsAdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];

        // Yangi fakultet (kafedrasiz) + boshqa fakultetda yangi yo'nalish (guruhsiz), nofaol yo'nalish ham.
        var facultyId = await Factory.CreateFacultyAsync($"Filtr fakultet {tag}");
        var group = await Factory.CreateGroupAsync(course: 1);
        var directionId = await Factory.CreateDirectionAsync(group.DepartmentId, $"filtr yo'nalish {tag}");
        var inactiveId = await Factory.CreateDirectionAsync(group.DepartmentId, $"Filtr nofaol {tag}");
        (await client.PatchAsJsonAsync($"/api/admin/directions/{inactiveId}/status", new { isActive = false }))
            .IsSuccessStatusCode.Should().BeTrue();

        var before = await GetFiltersAsync(client);
        before.Faculties.Should().Contain(f => f.Id == facultyId && f.Name == $"Filtr fakultet {tag}");
        before.Directions.Should().Contain(d => d.Id == directionId && d.FacultyId == group.FacultyId);
        before.Directions.Should().Contain(d => d.Id == inactiveId, "nofaol yo'nalish ham filtrda qoladi");
        before.Directions.Should().Contain(d => d.Id == group.DirectionId && d.FacultyId == group.FacultyId);
        before.Courses.Should().Contain(1).And.BeInAscendingOrder().And.OnlyHaveUniqueItems();

        // Tartib — nom bo'yicha case-insensitive ("filtr ..." kichik harf bilan ham "Filtr ..." lar orasida).
        before.Faculties.Select(f => f.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        before.Directions.Select(d => d.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);

        // Kurslar — o'chirilmagan guruhlardagi noyob qiymatlar bilan aynan bir xil.
        var dbCourses = await Factory.WithDbAsync(db =>
            db.StudentGroups.Select(g => g.Course).Distinct().OrderBy(c => c).ToListAsync());
        before.Courses.Should().Equal(dbCourses);

        // O'chirish (soft delete) → ro'yxatdan yo'qoladi.
        (await client.DeleteAsync($"/api/admin/faculties/{facultyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/admin/directions/{directionId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetFiltersAsync(client);
        after.Faculties.Should().NotContain(f => f.Id == facultyId);
        after.Directions.Should().NotContain(d => d.Id == directionId);
        after.Directions.Should().Contain(d => d.Id == inactiveId);
    }

    [Fact]
    public async Task Tyutor_403_Anonim_401()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/admin/students/filters")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync("/api/admin/students?course=2")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync("/api/admin/students/filters")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/admin/students?course=2")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<AdminStudentFiltersDto> GetFiltersAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/admin/students/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<AdminStudentFiltersDto>())!;
    }
}
