using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Tutor.Companies;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Korxona sahifalari (admin va tyutor) — faqat HOZIR shu korxonada aktiv amaliyot o'tayotgan talabalar
/// (§4.7 aktiv korxona qoidasi): ro'yxat, sonlar (<c>students</c>/<c>totalStudents</c>), <c>periods</c>.
/// O'chirish taqiqi esa tarix bo'yicha qoladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CompanyActiveStudentsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private sealed record Scene(TestGroup Group, TestUser Tutor, Company Company);

    private async Task<Scene> SceneAsync(string name)
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var company = await Factory.CreateCompanyAsync(name: $"{name} {Guid.NewGuid():N}"[..30]);
        return new Scene(group, tutor, company);
    }

    private Task<PracticePeriod> PeriodAsync(TestGroup group, Guid createdBy, int from, int to) =>
        Factory.WithDbAsync(async db =>
        {
            var today = Factory.Today();
            var period = PracticePeriod.Create(
                $"Davr {Guid.NewGuid():N}"[..16], group.AcademicYearId, today.AddDays(from), today.AddDays(to),
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

    /// <summary>Qoralama/yuborilgan/rad etilgan/o'tkazilgan ariza (tasdiqlanmagan holatlar).</summary>
    private Task AddApplicationAsync(TestUser student, PracticePeriod period, Company company, Guid tutorId, ApplicationStatus status) =>
        Factory.WithDbAsync(async db =>
        {
            var now = Factory.Services.GetRequiredService<IClock>().UtcNow;
            var application = PracticeApplication.Create(student.Id, period.Id, company.Id, company.RadiusM, null, now.AddDays(-5));
            switch (status)
            {
                case ApplicationStatus.Submitted:
                    break;
                case ApplicationStatus.Rejected:
                    application.Reject(tutorId, "Mos emas", now.AddDays(-4));
                    break;
                case ApplicationStatus.Transferred:
                    application.Approve(tutorId, company.RadiusM, Enumerable.Range(0, PracticeApplication.ChecklistItemCount), "OK", now.AddDays(-4));
                    application.Transfer(tutorId, "Ko'chirildi", now.AddDays(-3));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }

            db.PracticeApplications.Add(application);
            await db.SaveChangesAsync();
        });

    private static async Task<CompanyRow> AdminRowAsync(HttpClient admin, Company company)
    {
        var page = await admin.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={Uri.EscapeDataString(company.Name)}");
        return page.Items.Should().ContainSingle(r => r.Id == company.Id).Subject;
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<T>())!;
    }

    private static async Task AssertEmptyAsync(HttpClient client, string area, Company company)
    {
        var detail = await GetOkAsync<CompanyDetail>(client, $"/api/{area}/companies/{company.Id}");
        detail.Students.Should().Be(0);
        detail.TotalStudents.Should().Be(0);
        detail.OverLimit.Should().BeFalse();
        detail.Periods.Should().BeEmpty();
        (await GetOkAsync<List<CompanyStudent>>(client, $"/api/{area}/companies/{company.Id}/students")).Should().BeEmpty();
    }

    [Fact]
    public async Task YopilganDavr_Approved_KorxonadaYoq_Son0_PeriodsBosh_AdminVaTyutor()
    {
        var s = await SceneAsync("Yopilgan Tarix");
        var closed = await PeriodAsync(s.Group, s.Tutor.Id, -20, 10);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(student, closed, s.Company, s.Tutor.Id);
        await ClosePeriodAsync(closed.Id);

        var admin = await Factory.LoginAsAdminAsync();
        var row = await AdminRowAsync(admin, s.Company);
        row.Students.Should().Be(0);
        row.OverLimit.Should().BeFalse();
        await AssertEmptyAsync(admin, "admin", s.Company);

        // Tyutor: ro'yxatda yo'q; tafsilot tarix bo'yicha ochiladi (200), lekin sonlar 0 va talabalar bo'sh.
        var tutor = await Factory.LoginAsync(s.Tutor);
        (await GetOkAsync<List<TutorCompany>>(tutor, "/api/tutor/companies")).Should().NotContain(c => c.Id == s.Company.Id);
        await AssertEmptyAsync(tutor, "tutor", s.Company);
    }

    [Fact]
    public async Task TugaganDavr_Approved_KorxonadaYoq()
    {
        var s = await SceneAsync("Tugagan Tarix");
        var ended = await PeriodAsync(s.Group, s.Tutor.Id, -40, -10);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(student, ended, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Company)).Students.Should().Be(0);
        await AssertEmptyAsync(admin, "admin", s.Company);
    }

    [Fact]
    public async Task AktivDavr_Approved_Bor_Son1_PeriodsdaShuDavr_AdminVaTyutor()
    {
        var s = await SceneAsync("Aktiv Joy");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(student, period, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Company)).Students.Should().Be(1);

        foreach (var (client, area) in new[] { (admin, "admin"), (await Factory.LoginAsync(s.Tutor), "tutor") })
        {
            var detail = await GetOkAsync<CompanyDetail>(client, $"/api/{area}/companies/{s.Company.Id}");
            detail.Students.Should().Be(1);
            detail.TotalStudents.Should().Be(1);
            var slice = detail.Periods.Should().ContainSingle().Subject;
            slice.Id.Should().Be(period.Id);
            slice.Students.Should().Be(1);

            var rows = await GetOkAsync<List<CompanyStudent>>(client, $"/api/{area}/companies/{s.Company.Id}/students");
            var only = rows.Should().ContainSingle().Subject;
            only.StudentId.Should().Be(student.Id);
            only.ApplicationStatus.Should().Be(ApplicationStatus.Approved);
            only.PeriodName.Should().Be(period.Name);
        }

        var tutor = await Factory.LoginAsync(s.Tutor);
        var tutorRow = (await GetOkAsync<List<TutorCompany>>(tutor, "/api/tutor/companies"))
            .Should().ContainSingle(c => c.Id == s.Company.Id).Subject;
        tutorRow.Students.Should().Be(1);
        tutorRow.TotalStudents.Should().Be(1);
    }

    [Fact]
    public async Task AktivVaYopilganAralash_FaqatAktivDavrPeriodsda()
    {
        var s = await SceneAsync("Aralash Joy");
        var closed = await PeriodAsync(s.Group, s.Tutor.Id, -30, 5);
        var old = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(old, closed, s.Company, s.Tutor.Id);
        await ClosePeriodAsync(closed.Id);

        var otherGroup = await Factory.CreateGroupAsync();
        var open = await PeriodAsync(otherGroup, s.Tutor.Id, -3, 20);
        var current = await Factory.CreateStudentAsync(group: otherGroup);
        await Factory.CreateApprovedApplicationAsync(current, open, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        var detail = await GetOkAsync<CompanyDetail>(admin, $"/api/admin/companies/{s.Company.Id}");
        detail.Students.Should().Be(1);
        detail.Periods.Should().ContainSingle().Which.Id.Should().Be(open.Id);
        (await GetOkAsync<List<CompanyStudent>>(admin, $"/api/admin/companies/{s.Company.Id}/students"))
            .Select(r => r.StudentId).Should().Equal(current.Id);
    }

    [Fact]
    public async Task Submitted_Rejected_Transferred_KorxonadaYoq()
    {
        var s = await SceneAsync("Kutilayotgan Joy");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20);
        foreach (var status in new[] { ApplicationStatus.Submitted, ApplicationStatus.Rejected, ApplicationStatus.Transferred })
        {
            var student = await Factory.CreateStudentAsync(group: s.Group);
            await AddApplicationAsync(student, period, s.Company, s.Tutor.Id, status);
        }

        var admin = await Factory.LoginAsAdminAsync();
        (await AdminRowAsync(admin, s.Company)).Students.Should().Be(0);
        await AssertEmptyAsync(admin, "admin", s.Company);

        // Tyutor ko'lamida tasdiqlangan ariza hech qachon bo'lmagan — tafsilot 404, ro'yxatda yo'q.
        var tutor = await Factory.LoginAsync(s.Tutor);
        (await GetOkAsync<List<TutorCompany>>(tutor, "/api/tutor/companies")).Should().NotContain(c => c.Id == s.Company.Id);
        (await tutor.GetAsync($"/api/tutor/companies/{s.Company.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Otkazilgan_EskiKorxonadaYoq_YangisidaBor_AdminVaTyutor()
    {
        var s = await SceneAsync("Eski Joy");
        var period = await PeriodAsync(s.Group, s.Tutor.Id, -5, 20);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(student, period, s.Company, s.Tutor.Id);
        var target = await Factory.CreateCompanyAsync(name: $"Yangi Joy {Guid.NewGuid():N}"[..30]);

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.PostJsonAsync($"/api/admin/students/{student.Id}/company", new { companyId = target.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        (await AdminRowAsync(admin, s.Company)).Students.Should().Be(0);
        (await AdminRowAsync(admin, target)).Students.Should().Be(1);
        (await GetOkAsync<List<CompanyStudent>>(admin, $"/api/admin/companies/{s.Company.Id}/students")).Should().BeEmpty();
        (await GetOkAsync<List<CompanyStudent>>(admin, $"/api/admin/companies/{target.Id}/students"))
            .Should().ContainSingle(r => r.StudentId == student.Id);

        var tutor = await Factory.LoginAsync(s.Tutor);
        var companies = await GetOkAsync<List<TutorCompany>>(tutor, "/api/tutor/companies");
        companies.Should().NotContain(c => c.Id == s.Company.Id);
        companies.Should().ContainSingle(c => c.Id == target.Id).Which.Students.Should().Be(1);
        // Eski korxonadagi ariza endi `transferred` — tyutor ko'lam darvozasi (tasdiqlangan ariza) o'zgarmagan → 404.
        (await tutor.GetAsync($"/api/tutor/companies/{s.Company.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetOkAsync<List<CompanyStudent>>(admin, $"/api/admin/companies/{s.Company.Id}/students")).Should().BeEmpty();
        (await GetOkAsync<CompanyDetail>(tutor, $"/api/tutor/companies/{target.Id}")).Students.Should().Be(1);
    }

    [Fact]
    public async Task Ochirish_TarixBor_AktivYoq_409_TarixXabari()
    {
        var s = await SceneAsync("Tarixli Joy");
        var ended = await PeriodAsync(s.Group, s.Tutor.Id, -40, -10);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(student, ended, s.Company, s.Tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        await admin.PatchAsJsonAsync($"/api/admin/companies/{s.Company.Id}/status", new { isActive = false }, JsonDefaults.Options);

        var response = await admin.DeleteAsync($"/api/admin/companies/{s.Company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Korxonada amaliyot tarixi (arizalar) bor");
        body.Should().NotContain("talaba biriktirilgan");
    }
}
