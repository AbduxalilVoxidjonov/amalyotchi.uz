using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Dashboard;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminDashboardTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    /// <summary>Barqaror ish kuni (chorshanba, bayram emas) — "bugun" ko'rsatkichlari real hafta kuniga bog'liq bo'lmasin.</summary>
    private static readonly DateOnly Day = new(2026, 11, 18);

    [Fact]
    public async Task Dashboard_MalumotBilan_200_ShaklVaAgregatlar()
    {
        // Token soat muzlatilishidan OLDIN olinadi (JwtBearer o'z soatini ishlatadi).
        var client = await Factory.LoginAsAdminAsync();
        Factory.Clock.Set(PracticeTime.At(Day, new TimeOnly(10, 0)));
        try
        {
            await AssertShapeAndAggregatesAsync(client);
        }
        finally
        {
            Factory.Clock.Reset();
        }
    }

    private async Task AssertShapeAndAggregatesAsync(HttpClient client)
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group, "Dashboard Tyutor");
        var company = await Factory.CreateCompanyAsync();
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var present = await Factory.CreateStudentAsync(group: group, fullName: "Dashboard Kelgan");
        var late = await Factory.CreateStudentAsync(group: group, fullName: "Dashboard Kech");
        var pending = await Factory.CreateStudentAsync(group: group, fullName: "Dashboard Ariza");
        await Factory.CreateApprovedApplicationAsync(present, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(late, period, company, tutor.Id);
        await Factory.CreatePendingApplicationAsync(pending, period, company, TimeSpan.FromHours(72));

        var today = Factory.Today();
        await Factory.CheckInAsync(present, period, today);
        await Factory.CheckInAsync(late, period, today, late: true);

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.TryGetProperty("stats", out var stats).Should().BeTrue("camelCase");
        root.TryGetProperty("faculties", out _).Should().BeTrue();
        root.TryGetProperty("tutors", out _).Should().BeTrue();
        root.TryGetProperty("audit", out _).Should().BeTrue();
        stats.GetProperty("attendanceTodayPct").ValueKind.Should().Be(JsonValueKind.Number, "xom raqam, matn emas");
        root.GetProperty("date").GetString().Should().Be(today.ToString("yyyy-MM-dd"));

        var dto = JsonSerializer.Deserialize<AdminDashboardDto>(body, JsonDefaults.Options)!;
        dto.Stats.StudentsTotal.Should().BeGreaterThanOrEqualTo(3);
        dto.Stats.PresentToday.Should().BeGreaterThanOrEqualTo(1);
        dto.Stats.LateToday.Should().BeGreaterThanOrEqualTo(1);
        dto.Stats.ApplicationsPending.Should().BeGreaterThanOrEqualTo(1);
        dto.Stats.ApplicationsOverdue.Should().BeGreaterThanOrEqualTo(1, "72 soat oldin yuborilgan ariza 48 soatdan eski");
        dto.Stats.ContractsApproved.Should().BeGreaterThanOrEqualTo(2);
        dto.Stats.AttendanceTodayPct.Should().BeInRange(0, 100);

        var faculty = dto.Faculties.Should().ContainSingle(f => f.Id == group.FacultyId).Subject;
        faculty.StudentCount.Should().Be(3);
        faculty.ExpectedToday.Should().Be(3);
        faculty.AttendedToday.Should().Be(2);
        faculty.AttendancePct.Should().Be(67);

        var tutorRow = dto.Tutors.Should().ContainSingle(t => t.Id == tutor.Id).Subject;
        tutorRow.Groups.Should().Equal(group.GroupName);
        tutorRow.StudentCount.Should().Be(3);
        tutorRow.PendingCount.Should().Be(1);
        tutorRow.Status.Should().Be(Application.Features.Admin.Common.TutorStatus.Late);
        tutorRow.AvgDecisionHours.Should().NotBeNull("ikkita ariza tasdiqlangan");

        dto.Audit.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(8);
        body.Should().Contain("\"action\":\"", "audit amali enum string");
        dto.Audit.Should().BeInDescendingOrder(a => a.At);
    }

    [Fact]
    public async Task Dashboard_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_Anonim_401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

/// <summary>Bo'sh baza (faqat seed: admin, sozlamalar, bayramlar) — dashboard 200 va nol qiymatlar.</summary>
[Collection(AdminEmptyDbCollection.Name)]
public sealed class AdminDashboardEmptyDbTests(AdminEmptyDbFixture fixture)
{
    [Fact]
    public async Task Dashboard_BoshBaza_200_NolQiymatlar()
    {
        var client = await fixture.Factory.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadAsync<AdminDashboardDto>();
        dto!.Stats.StudentsTotal.Should().Be(0);
        dto.Stats.Faculties.Should().Be(0);
        dto.Stats.Groups.Should().Be(0);
        dto.Stats.CompaniesActive.Should().Be(0);
        dto.Stats.ApplicationsPending.Should().Be(0);
        dto.Stats.ExpectedToday.Should().Be(0);
        dto.Stats.PresentToday.Should().Be(0);
        dto.Stats.AbsentToday.Should().Be(0);
        dto.Stats.AttendanceTodayPct.Should().Be(0);
        dto.Stats.AttendanceYesterdayPct.Should().Be(0);
        dto.Faculties.Should().BeEmpty();
        dto.Tutors.Should().BeEmpty();
        // Admin yaratilishi va kirish audit'ga tushadi — ro'yxat bo'sh bo'lmasligi mumkin, lekin ≤ 8.
        dto.Audit.Should().HaveCountLessThanOrEqualTo(8);
    }

    [Fact]
    public async Task Royxatlar_BoshBaza_200_BoshSahifa()
    {
        var client = await fixture.Factory.LoginAsAdminAsync();

        foreach (var path in new[] { "faculties", "groups", "students", "companies" })
        {
            var response = await client.GetAsync($"/api/admin/{path}");
            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("items").GetArrayLength().Should().Be(0, path);
            json.RootElement.GetProperty("total").GetInt32().Should().Be(0, path);
            json.RootElement.GetProperty("page").GetInt32().Should().Be(1, path);
            json.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20, path);
        }
    }
}
