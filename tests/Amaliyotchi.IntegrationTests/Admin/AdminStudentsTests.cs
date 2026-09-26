using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AdminStudentsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Talabalar_200_Shakl_Holatlar()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync(name: "Talaba Korxonasi");
        var today = Factory.Today();

        var active = await Factory.CreateStudentAsync(group: group, fullName: "Holat Faol");
        await Factory.CreateApprovedApplicationAsync(active, period, company, tutor.Id);
        for (var d = 1; d <= 14; d++)
            await Factory.CheckInAsync(active, period, today.AddDays(-d));

        var flagged = await Factory.CreateStudentAsync(group: group, fullName: "Holat Shubhali");
        await Factory.CreateApprovedApplicationAsync(flagged, period, company, tutor.Id);
        for (var d = 1; d <= 14; d++)
            await Factory.CheckInAsync(flagged, period, today.AddDays(-d), suspicious: d <= 2);

        var unlinked = await Factory.CreateUnlinkedStudentAsync(group, "Holat Ulanmagan");

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/students?q={group.GroupName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var item = json.RootElement.GetProperty("items")[0];
            item.TryGetProperty("hemisId", out _).Should().BeTrue("camelCase");
            item.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        }

        var page = await client.GetPagedAsync<StudentRow>($"/api/admin/students?q={group.GroupName}");
        page.Total.Should().Be(3);
        page.Items.Should().BeInAscendingOrder(s => s.FullName);

        var a = page.Items.Single(s => s.Id == active.Id);
        a.Status.Should().Be(AdminStudentStatus.Active);
        a.Company.Should().Be("Talaba Korxonasi");
        a.Group.Should().Be(group.GroupName);
        a.AttendancePct.Should().Be(100);
        a.TelegramLinked.Should().BeTrue();

        var f = page.Items.Single(s => s.Id == flagged.Id);
        f.SuspiciousDays.Should().Be(2);
        f.Status.Should().Be(AdminStudentStatus.Flagged);

        var u = page.Items.Single(s => s.Id == unlinked.Id);
        u.Status.Should().Be(AdminStudentStatus.Unlinked);
        u.TelegramLinked.Should().BeFalse();
        u.Company.Should().BeNull();
    }

    [Fact]
    public async Task Talabalar_PastDavomat_Flagged_HemisBoyichaQidiruv()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id);
        await Factory.CheckInAsync(student, period, Factory.Today().AddDays(-1)); // 14 kundan 1 tasi

        var hemisId = await Factory.WithDbAsync(db =>
            db.StudentProfiles.Where(p => p.UserId == student.Id).Select(p => p.HemisId).FirstAsync());

        var client = await Factory.LoginAsAdminAsync();
        var page = await client.GetPagedAsync<StudentRow>($"/api/admin/students?q={hemisId}");

        page.Total.Should().Be(1);
        page.Items.Single().HemisId.Should().Be(hemisId);
        page.Items.Single().AttendancePct.Should().BeLessThan(AdminThresholds.FlaggedAttendancePct);
        page.Items.Single().Status.Should().Be(AdminStudentStatus.Flagged);
    }

    [Fact]
    public async Task Talabalar_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.GetAsync("/api/admin/students")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Talabalar_PageSize_500gacha_Ruxsat_Oshsa_Sukut20()
    {
        var group = await Factory.CreateGroupAsync();
        for (var i = 0; i < 3; i++)
            await Factory.CreateStudentAsync(group: group);

        var client = await Factory.LoginAsAdminAsync();

        // 100 dan katta hajm talabalar ro'yxatida qabul qilinadi va javobda aks etadi.
        var filtered = await client.GetPagedAsync<StudentRow>($"/api/admin/students?q={group.GroupName}&pageSize=250");
        filtered.PageSize.Should().Be(250);
        filtered.Items.Should().HaveCount(3);

        var all = await client.GetPagedAsync<StudentRow>("/api/admin/students?pageSize=250");
        all.PageSize.Should().Be(250);
        all.Items.Should().HaveCount(Math.Min(all.Total, 250));

        var max = await client.GetPagedAsync<StudentRow>("/api/admin/students?pageSize=500");
        max.PageSize.Should().Be(500);

        // Chegaradan oshsa — mavjud qoida: xato emas, sukut (20) ga tushiriladi.
        var over = await client.GetAsync("/api/admin/students?pageSize=501");
        over.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetPagedAsync<StudentRow>("/api/admin/students?pageSize=501")).PageSize.Should().Be(20);

        // Boshqa ro'yxatlar 100 chegarasida qoladi.
        using var companies = JsonDocument.Parse(await (await client.GetAsync("/api/admin/companies?pageSize=250")).Content.ReadAsStringAsync());
        companies.RootElement.GetProperty("pageSize").GetInt32().Should().Be(20);
    }
}
