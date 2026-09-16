using System.Net;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.Settings;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Tutor;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Korxona tafsiloti, talabalar ro'yxati va STIR nazorati (<c>maxStudentsPerCompany</c>).</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminCompanyDetailTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Detal_200_Shakl_VaDavrlarKesimi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync(radiusM: 250);
        var a = await Factory.CreateStudentAsync(group: group, fullName: "Anvar Detal");
        var b = await Factory.CreateStudentAsync(group: group, fullName: "Zufar Detal");
        await Factory.CreateApprovedApplicationAsync(a, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(b, period, company, tutor.Id);

        var days = await Factory.PastWorkDaysAsync(period, 3);
        foreach (var day in days)
            await Factory.CheckInAsync(a, period, day);
        await Factory.CheckInAsync(b, period, days[0], suspicious: true);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/companies/{company.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadAsync<CompanyDetail>())!;
        detail.Id.Should().Be(company.Id);
        detail.Tin.Should().Be(company.Tin).And.MatchRegex("^[0-9]{9}$");
        detail.Activity.Should().Be(company.Activity);
        detail.Address.Should().Be(company.Address);
        detail.Lat.Should().BeApproximately(41.3111, 0.0001);
        detail.Lng.Should().BeApproximately(69.2797, 0.0001);
        detail.RadiusM.Should().Be(250);
        detail.SupervisorName.Should().Be(company.SupervisorName);
        detail.SupervisorPhone.Should().Be(company.SupervisorPhone);
        detail.MentorName.Should().BeNull();
        detail.IsActive.Should().BeTrue();
        detail.Students.Should().Be(2);
        detail.TotalStudents.Should().Be(2, "admin ko'lami cheklanmagan");
        detail.SuspiciousDays.Should().Be(1);
        detail.MaxStudents.Should().Be(10, "standart sozlama");
        detail.OverLimit.Should().BeFalse();
        detail.Flag.Should().BeNull();

        var slice = detail.Periods.Should().ContainSingle().Subject;
        slice.Id.Should().Be(period.Id);
        slice.Name.Should().Be(period.Name);
        slice.StartDate.Should().Be(period.StartDate);
        slice.EndDate.Should().Be(period.EndDate);
        slice.Students.Should().Be(2);
    }

    [Fact]
    public async Task Detal_NomalumId_404()
    {
        var client = await Factory.LoginAsAdminAsync();

        (await client.GetAsync($"/api/admin/companies/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/admin/companies/{Guid.NewGuid()}/students")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Talabalar_200_FISHTartibi_VaKorsatkichlar()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group, fullName: "Tolib Tyutorov");
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var active = await Factory.CreateStudentAsync(group: group, fullName: "Anvar Faolov");
        var weak = await Factory.CreateStudentAsync(group: group, fullName: "Zufar Zaifov");
        await Factory.CreateApprovedApplicationAsync(active, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(weak, period, company, tutor.Id);

        var days = await Factory.PastWorkDaysAsync(period, 100);
        days.Should().HaveCountGreaterThan(5, "davr 14 kun oldin boshlangan");
        foreach (var day in days)
            await Factory.CheckInAsync(active, period, day);
        await Factory.AddDiaryAsync(active, period, days[0], tutor.Id, score: 5);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.GetAsync($"/api/admin/companies/{company.Id}/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadAsync<List<CompanyStudent>>())!;
        rows.Select(r => r.Name).Should().Equal("Anvar Faolov", "Zufar Zaifov");

        var first = rows[0];
        first.StudentId.Should().Be(active.Id);
        first.HemisId.Should().NotBeNullOrEmpty();
        first.Group.Should().Be(group.GroupName);
        first.Course.Should().Be(group.Course);
        first.Faculty.Should().NotBeNullOrEmpty();
        first.TutorName.Should().Be("Tolib Tyutorov");
        first.ApplicationStatus.Should().Be(ApplicationStatus.Approved);
        first.PeriodName.Should().Be(period.Name);
        first.AttendedDays.Should().Be(days.Count);
        first.TotalDays.Should().BeGreaterThanOrEqualTo(first.AttendedDays);
        first.AttendancePct.Should().BeGreaterThanOrEqualTo(70);
        first.DiaryCount.Should().Be(1);
        first.State.Should().Be(StudentState.Active);
        first.SuspiciousCount.Should().Be(0);

        var second = rows[1];
        second.AttendedDays.Should().Be(0);
        second.TotalDays.Should().BeGreaterThan(0);
        second.AttendancePct.Should().Be(0);
        second.State.Should().Be(StudentState.RedFlag);
    }

    [Fact]
    public async Task Sozlama_ChegaradanOshgan_OverLimit_VaTooManyStudentsBayrogi()
    {
        var marker = Guid.NewGuid().ToString("N")[..6];
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        // Radius 600 — "tooManyStudents" aynan "largeRadius" dan ustunligini ko'rsatish uchun.
        var company = await Factory.CreateCompanyAsync(radiusM: 600, name: $"Korxona {marker} Chegara");
        var s1 = await Factory.CreateStudentAsync(group: group);
        var s2 = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(s1, period, company, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(s2, period, company, tutor.Id);

        var client = await Factory.LoginAsAdminAsync();
        var before = (await client.GetFromJsonAsync<AdminSettingsDto>("/api/admin/settings"))!;
        var old = before.Settings.Single(s => s.Key == SettingKeys.MaxStudentsPerCompany);
        old.Value.Should().Be("10", "seed standart qiymat");
        old.Min.Should().Be(1);
        old.Max.Should().Be(200);
        old.Unit.Should().BeNull();

        try
        {
            var put = await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string> { [SettingKeys.MaxStudentsPerCompany] = "1" }
            });
            put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

            var row = (await client.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={marker}")).Items.Single();
            row.Students.Should().Be(2);
            row.MaxStudents.Should().Be(1);
            row.OverLimit.Should().BeTrue();
            row.Flag.Should().Be(CompanyFlag.TooManyStudents, "tooManyStudents largeRadius'dan ustun");

            var detail = (await (await client.GetAsync($"/api/admin/companies/{company.Id}")).Content.ReadAsync<CompanyDetail>())!;
            detail.MaxStudents.Should().Be(1);
            detail.OverLimit.Should().BeTrue();
            detail.Flag.Should().Be(CompanyFlag.TooManyStudents);

            // Shubhali kunlar chegarasiga yetsa — suspicious hammasidan ustun.
            var days = await Factory.PastWorkDaysAsync(period, 3);
            foreach (var day in days)
                await Factory.CheckInAsync(s1, period, day, suspicious: true);

            var suspicious = (await client.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={marker}")).Items.Single();
            suspicious.SuspiciousDays.Should().Be(3);
            suspicious.Flag.Should().Be(CompanyFlag.Suspicious);
        }
        finally
        {
            var restore = await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string> { [SettingKeys.MaxStudentsPerCompany] = old.Value }
            });
            restore.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Sozlama qaytarilgach chegara ham qaytadi.
        var after = (await client.GetPagedAsync<CompanyRow>($"/api/admin/companies?q={marker}")).Items.Single();
        after.MaxStudents.Should().Be(10);
        after.OverLimit.Should().BeFalse();
    }
}
