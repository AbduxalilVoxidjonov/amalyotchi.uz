using System.Net;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Tutor.Companies;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

/// <summary>Tyutor korxonalari — ko'lam qat'iy: begona korxona ro'yxatda ham, tafsilotda ham ko'rinmaydi (404).</summary>
[Collection(ApiCollection.Name)]
public sealed class TutorCompaniesTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    /// <summary>Bitta davr + ikki guruh: tyutor faqat o'z guruhini ko'radi.
    /// <c>shared</c> — ikkala guruh talabasi, <c>foreign</c> — faqat begona guruh talabasi.</summary>
    private sealed record Scene(
        HttpClient Client,
        TestUser Tutor,
        TestGroup Group,
        PracticePeriod Period,
        TestUser Mine,
        Domain.Companies.Company Shared,
        Domain.Companies.Company Foreign);

    private async Task<Scene> CreateSceneAsync()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group, fullName: "Tolib Tyutorov");
        var client = await Factory.LoginAsync(tutor);
        var otherGroup = await Factory.CreateGroupAsync();
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id, otherGroup.GroupId);

        var mine = await Factory.CreateStudentAsync(group: group, fullName: "Anvar Mening");
        var stranger = await Factory.CreateStudentAsync(group: otherGroup, fullName: "Begona Talaba");
        var stranger2 = await Factory.CreateStudentAsync(group: otherGroup, fullName: "Begona Ikkinchi");

        var shared = await Factory.CreateCompanyAsync(name: "Alfa Umumiy Korxona");
        var foreign = await Factory.CreateCompanyAsync(name: "Beta Begona Korxona");

        await Factory.CreateApprovedApplicationAsync(mine, period, shared, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(stranger, period, shared, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(stranger2, period, foreign, tutor.Id);

        return new Scene(client, tutor, group, period, mine, shared, foreign);
    }

    [Fact]
    public async Task Royxat_FaqatKolamdagiKorxonalar_StudentsTotalStudentsdanKam()
    {
        var scene = await CreateSceneAsync();
        var days = await Factory.PastWorkDaysAsync(scene.Period, 3);
        await Factory.AddAttendanceAsync(scene.Mine, scene.Period, days[0]);
        await Factory.AddAttendanceAsync(scene.Mine, scene.Period, days[1], suspiciousReason: "Radius chetida");

        var response = await scene.Client.GetAsync("/api/tutor/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadAsync<List<TutorCompany>>())!;
        list.Select(c => c.Id).Should().Contain(scene.Shared.Id).And.NotContain(scene.Foreign.Id,
            "ko'lamda talabasi bo'lmagan korxona ro'yxatga kirmaydi");

        var row = list.Single(c => c.Id == scene.Shared.Id);
        row.Name.Should().Be(scene.Shared.Name);
        row.Tin.Should().Be(scene.Shared.Tin);
        row.Address.Should().Be(scene.Shared.Address);
        row.Lat.Should().BeApproximately(41.3111, 0.0001);
        row.Lng.Should().BeApproximately(69.2797, 0.0001);
        row.RadiusM.Should().Be(150);
        row.Students.Should().Be(1, "faqat tyutor ko'lamidagi talaba");
        row.TotalStudents.Should().Be(2, "butun tizim bo'yicha");
        row.MaxStudents.Should().Be(10);
        row.OverLimit.Should().BeFalse();
        row.SuspiciousDays.Should().Be(1);
        row.AttendancePct.Should().BeGreaterThan(0);
        row.Flag.Should().BeNull();
        list.Select(c => c.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Detal_Kolamda_200_VaKolamdanTashqari_404()
    {
        var scene = await CreateSceneAsync();

        var response = await scene.Client.GetAsync($"/api/tutor/companies/{scene.Shared.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadAsync<CompanyDetail>())!;
        detail.Id.Should().Be(scene.Shared.Id);
        detail.Students.Should().Be(1, "ko'lamdagi talabalar");
        detail.TotalStudents.Should().Be(2);
        detail.MaxStudents.Should().Be(10);
        detail.OverLimit.Should().BeFalse();
        detail.Periods.Should().ContainSingle().Which.Students.Should().Be(1, "davr kesimi ham ko'lamda");

        (await scene.Client.GetAsync($"/api/tutor/companies/{scene.Foreign.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "mavjudligi oshkor qilinmaydi");
        (await scene.Client.GetAsync($"/api/tutor/companies/{scene.Foreign.Id}/students")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await scene.Client.GetAsync($"/api/tutor/companies/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Talabalar_FaqatKolamdagilar_VaTalaba403()
    {
        var scene = await CreateSceneAsync();
        var days = await Factory.PastWorkDaysAsync(scene.Period, 2);
        await Factory.AddAttendanceAsync(scene.Mine, scene.Period, days[0]);
        await Factory.AddDiaryAsync(scene.Mine, scene.Period, days[0], scene.Tutor.Id, score: 4);

        var response = await scene.Client.GetAsync($"/api/tutor/companies/{scene.Shared.Id}/students");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadAsync<List<CompanyStudent>>())!;
        var row = rows.Should().ContainSingle("begona guruh talabasi ko'rinmaydi").Subject;
        row.StudentId.Should().Be(scene.Mine.Id);
        row.Name.Should().Be("Anvar Mening");
        row.Group.Should().Be(scene.Group.GroupName);
        row.Faculty.Should().NotBeNullOrEmpty();
        row.TutorName.Should().Be("Tolib Tyutorov");
        row.ApplicationStatus.Should().Be(ApplicationStatus.Approved);
        row.PeriodName.Should().Be(scene.Period.Name);
        row.AttendedDays.Should().Be(1);
        row.DiaryCount.Should().Be(1);
        row.State.Should().Be(StudentState.RedFlag, "bitta kun — davomat 70% dan past");

        var studentClient = await Factory.LoginAsStudentAsync(scene.Mine);
        (await studentClient.GetAsync("/api/tutor/companies")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync($"/api/tutor/companies/{scene.Shared.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
