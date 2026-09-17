using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Place;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Talaba amaliyot joyini STIR orqali tanlaydi: korxona ma'lumoti qo'lda kiritilmaydi,
/// admin oldindan yaratgan yozuvdan olinadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentPlaceSubmitTests(ApiFixture fixture)
{
    private const string Url = "/api/student/place";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Stir_201_KorxonaMalumoti_Avtomatik_Toldiriladi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync(name: "STIR Korxonasi");
        var student = await Factory.CreateStudentAsync(group: group);

        var client = await Factory.LoginAsStudentAsync(student);
        var response = await client.PostJsonAsync(Url, new { tin = company.Tin });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var place = (await response.Content.ReadAsync<PracticePlaceDto>())!;
        place.Company.Should().Be("STIR Korxonasi");
        place.Address.Should().Be(company.Address);
        place.RadiusM.Should().Be(company.RadiusM);
        place.Status.Should().Be(ApplicationStatus.Submitted);

        // Tyutorning arizalar ro'yxatiga tushadi.
        var tutorClient = await Factory.LoginAsync(tutor);
        var applications = await tutorClient.GetAsync("/api/tutor/applications?status=submitted");
        (await applications.Content.ReadAsStringAsync()).Should().Contain("STIR Korxonasi");
    }

    [Fact]
    public async Task Stir_TakrorYuborish_409()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        var client = await Factory.LoginAsStudentAsync(student);

        (await client.PostJsonAsync(Url, new { tin = company.Tin })).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await client.PostJsonAsync(Url, new { tin = company.Tin });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("ko'rib chiqilmoqda");
    }

    [Fact]
    public async Task Stir_FaolBolmaganKorxona_404()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);

        var admin = await Factory.LoginAsAdminAsync();
        await admin.PatchAsJsonAsync($"/api/admin/companies/{company.Id}/status", new { isActive = false }, JsonDefaults.Options);

        var client = await Factory.LoginAsStudentAsync(student);
        var response = await client.PostJsonAsync(Url, new { tin = company.Tin });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stir_NotogriFormat_400_DavrYoq_409()
    {
        var student = await Factory.CreateStudentAsync();
        var company = await Factory.CreateCompanyAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var bad = await client.PostJsonAsync(Url, new { tin = "12ab" });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadAsStringAsync()).Should().Contain(SubmitPracticePlaceCommandValidator.TinFormatMessage);

        // Talabaga faol davr biriktirilmagan.
        var noPeriod = await client.PostJsonAsync(Url, new { tin = company.Tin });
        noPeriod.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await noPeriod.Content.ReadAsStringAsync()).Should().Contain("amaliyot davri");
    }

    [Fact]
    public async Task Stir_Tyutor_403()
    {
        var client = await Factory.LoginAsTutorAsync();

        (await client.PostJsonAsync(Url, new { tin = "123456789" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
