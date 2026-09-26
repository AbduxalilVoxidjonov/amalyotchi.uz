using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>"Aktiv korxona" qoidasi: ro'yxatlardagi <c>company</c> (admin, tyutor) va profildagi <c>activeCompany</c>
/// (admin, tyutor) — faqat bugun davom etayotgan, yopilmagan, guruh biriktirilgan davrdagi tasdiqlangan arizadan.
/// Oldingi (yopilgan/tugagan/kelgusi) davr korxonasiga fallback yo'q.</summary>
[Collection(ApiCollection.Name)]
public sealed class ActiveCompanyTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private sealed record Scene(TestGroup Group, TestUser Tutor, TestUser Student, Company Company);

    private async Task<Scene> SceneAsync(string companyName)
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group, fullName: $"Aktiv {Guid.NewGuid():N}"[..24]);
        var company = await Factory.CreateCompanyAsync(name: $"{companyName} {Guid.NewGuid():N}"[..30]);
        return new Scene(group, tutor, student, company);
    }

    /// <summary>Guruhga biriktirilgan davr (<paramref name="from"/>..<paramref name="to"/> kun bugunga nisbatan).</summary>
    private Task<PracticePeriod> PeriodAsync(TestGroup group, Guid createdBy, int from, int to, string? name = null) =>
        Factory.WithDbAsync(async db =>
        {
            var today = Factory.Today();
            var period = PracticePeriod.Create(
                name ?? $"Davr {Guid.NewGuid():N}"[..16], group.AcademicYearId, today.AddDays(from), today.AddDays(to),
                createdBy, CheckInRules.Default, WorkDays.MondayToSaturday, 20, dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    private Task ClosePeriodAsync(Guid periodId) =>
        Factory.WithDbAsync(async db =>
        {
            var period = await db.PracticePeriods.FindAsync(periodId);
            period!.Close();
            await db.SaveChangesAsync();
        });

    private async Task<StudentRow> AdminRowAsync(HttpClient admin, TestUser student)
    {
        var page = await admin.GetPagedAsync<StudentRow>($"/api/admin/students?q={Uri.EscapeDataString(student.FullName)}");
        return page.Items.Should().ContainSingle(r => r.Id == student.Id).Subject;
    }

    private static async Task<AdminStudentDetail> AdminDetailAsync(HttpClient admin, Guid studentId)
    {
        var response = await admin.GetAsync($"/api/admin/students/{studentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadAsync<AdminStudentDetail>())!;
    }

    private async Task<(TutorStudent Row, TutorStudentDetail Detail)> TutorViewAsync(Scene s)
    {
        var tutor = await Factory.LoginAsync(s.Tutor);
        var list = (await (await tutor.GetAsync("/api/tutor/students")).Content.ReadAsync<List<TutorStudent>>())!;
        var detail = (await (await tutor.GetAsync($"/api/tutor/students/{s.Student.Id}")).Content.ReadAsync<TutorStudentDetail>())!;
        return (list.Should().ContainSingle(r => r.Id == s.Student.Id).Subject, detail);
    }

    [Fact]
    public async Task YopilganDavr_Approved_KorxonaNull_SukutOchiqDavr()
    {
        // Jonli holat: yopilgan davrda ariza bor, guruh yana ochiq davom etayotgan davrga biriktirilgan (arizasiz).
        var s = await SceneAsync("Yopilgan Davr Korxona");
        var closed = await PeriodAsync(s.Group, s.Tutor.Id, -26, 18);
        await Factory.CreateApprovedApplicationAsync(s.Student, closed, s.Company, s.Tutor.Id);
        await ClosePeriodAsync(closed.Id);
        var open = await PeriodAsync(s.Group, s.Tutor.Id, -25, 4);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Student)).Company.Should().BeNull();

        var detail = await AdminDetailAsync(admin, s.Student.Id);
        detail.ActiveCompany.Should().BeNull();
        // Sukut davri — ochiq davom etayotgan davr (yopilgani emas); davrga bog'liq korxona ham shu davr bo'yicha (yo'q).
        detail.SelectedPeriodId.Should().Be(open.Id);
        detail.Company.Should().BeNull();
        // Tanlangan davr tarixi o'zgarmagan: yopilgan davrni tanlasa — o'sha korxona, activeCompany esa baribir null.
        var history = (await (await admin.GetAsync($"/api/admin/students/{s.Student.Id}?periodId={closed.Id}"))
            .Content.ReadAsync<AdminStudentDetail>())!;
        history.Company!.Id.Should().Be(s.Company.Id);
        history.ActiveCompany.Should().BeNull();

        var (row, tutorDetail) = await TutorViewAsync(s);
        row.Company.Should().BeNull();
        tutorDetail.ActiveCompany.Should().BeNull();
    }

    [Fact]
    public async Task TugaganDavr_Approved_KorxonaNull()
    {
        var s = await SceneAsync("Tugagan Davr Korxona");
        var ended = await PeriodAsync(s.Group, s.Tutor.Id, -40, -10);
        await Factory.CreateApprovedApplicationAsync(s.Student, ended, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Student)).Company.Should().BeNull();
        var detail = await AdminDetailAsync(admin, s.Student.Id);
        detail.ActiveCompany.Should().BeNull();
        detail.Company!.Id.Should().Be(s.Company.Id, "davrga bog'liq company — tanlangan (tugagan) davr tarixi");

        var (row, tutorDetail) = await TutorViewAsync(s);
        row.Company.Should().BeNull();
        tutorDetail.ActiveCompany.Should().BeNull();
    }

    [Fact]
    public async Task OchiqDavomEtayotganDavr_Approved_RoyxatVaProfilBirXil()
    {
        var s = await SceneAsync("Aktiv Korxona");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20, name: $"Ochiq {Guid.NewGuid():N}"[..14]);
        await Factory.CreateApprovedApplicationAsync(s.Student, period, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Student)).Company.Should().Be(s.Company.Name);

        var response = await admin.GetAsync($"/api/admin/students/{s.Student.Id}");
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var active = json.RootElement.GetProperty("activeCompany");
            active.GetProperty("id").GetGuid().Should().Be(s.Company.Id);
            active.GetProperty("name").GetString().Should().Be(s.Company.Name);
            active.GetProperty("periodId").GetGuid().Should().Be(period.Id);
            active.GetProperty("periodName").GetString().Should().Be(period.Name);
        }

        var (row, tutorDetail) = await TutorViewAsync(s);
        row.Company.Should().Be(s.Company.Name);
        tutorDetail.ActiveCompany.Should().Be(new ActiveCompanyRef(s.Company.Id, s.Company.Name, period.Id, period.Name));
    }

    [Fact]
    public async Task Transferred_KeyinYangiKorxona()
    {
        var s = await SceneAsync("Eski Aktiv");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20);
        await Factory.CreateApprovedApplicationAsync(s.Student, period, s.Company, s.Tutor.Id);
        var newCompany = await Factory.CreateCompanyAsync(name: $"Yangi Aktiv {Guid.NewGuid():N}"[..30]);

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.PostJsonAsync($"/api/admin/students/{s.Student.Id}/company", new { companyId = newCompany.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var reassigned = (await response.Content.ReadAsync<AdminStudentDetail>())!;
        reassigned.ActiveCompany!.Id.Should().Be(newCompany.Id);
        reassigned.ActiveCompany.PeriodId.Should().Be(period.Id);

        (await AdminRowAsync(admin, s.Student)).Company.Should().Be(newCompany.Name);
        (await AdminDetailAsync(admin, s.Student.Id)).ActiveCompany!.Name.Should().Be(newCompany.Name);

        var (row, tutorDetail) = await TutorViewAsync(s);
        row.Company.Should().Be(newCompany.Name);
        tutorDetail.ActiveCompany!.Id.Should().Be(newCompany.Id);
    }

    [Fact]
    public async Task KelgusiDavr_Approved_KorxonaNull()
    {
        var s = await SceneAsync("Kelgusi Korxona");
        var upcoming = await PeriodAsync(s.Group, s.Tutor.Id, 5, 30);
        await Factory.CreateApprovedApplicationAsync(s.Student, upcoming, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Student)).Company.Should().BeNull();
        (await AdminDetailAsync(admin, s.Student.Id)).ActiveCompany.Should().BeNull();

        var (row, tutorDetail) = await TutorViewAsync(s);
        row.Company.Should().BeNull();
        tutorDetail.ActiveCompany.Should().BeNull();
    }

    [Fact]
    public async Task GuruhDavrdanAjratilgan_KorxonaNull()
    {
        var s = await SceneAsync("Ajratilgan Korxona");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20);
        await Factory.CreateApprovedApplicationAsync(s.Student, period, s.Company, s.Tutor.Id);
        await Factory.WithDbAsync(async db =>
        {
            var tracked = db.PracticePeriods.Include(p => p.Groups).Single(p => p.Id == period.Id);
            tracked.DetachGroup(s.Group.GroupId, hasAttendanceRecords: false);
            await db.SaveChangesAsync();
        });

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Student)).Company.Should().BeNull();
        (await AdminDetailAsync(admin, s.Student.Id)).ActiveCompany.Should().BeNull();
    }
}
