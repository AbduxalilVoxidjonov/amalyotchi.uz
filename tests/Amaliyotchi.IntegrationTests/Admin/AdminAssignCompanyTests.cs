using System.Net;
using System.Net.Http.Json;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.Domain.Practice;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Talabalar ro'yxatidan belgilangan talabalarni korxonaga ommaviy biriktirish.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminAssignCompanyTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/students/assign-company";

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Biriktirish_200_ArizaTasdiqlangan_TalabaJoyiniKoradi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync(name: "Biriktirish Korxonasi");
        var first = await Factory.CreateStudentAsync(group: group, fullName: "Biriktirish Birinchi");
        var second = await Factory.CreateStudentAsync(group: group, fullName: "Biriktirish Ikkinchi");

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.PostJsonAsync(Url, new { studentIds = new[] { first.Id, second.Id }, companyId = company.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadAsync<AssignCompanyResult>())!;
        result.Total.Should().Be(2);
        result.Assigned.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.CompanyName.Should().Be("Biriktirish Korxonasi");
        result.Errors.Should().BeEmpty();

        var application = await Factory.WithDbAsync(db => db.PracticeApplications.AsNoTracking()
            .FirstAsync(a => a.StudentUserId == first.Id));
        application.Status.Should().Be(ApplicationStatus.Approved);
        application.CompanyId.Should().Be(company.Id);
        application.DecisionComment.Should().Be(AssignStudentsToCompanyCommandHandlerComment);

        // Talaba "Amaliyot joyim" ekranida darhol ko'radi.
        var studentClient = await Factory.LoginAsStudentAsync(first);
        var place = (await (await studentClient.GetAsync("/api/student/place")).Content.ReadAsync<PracticePlaceDto>())!;
        place.Company.Should().Be("Biriktirish Korxonasi");
        place.Status.Should().Be(ApplicationStatus.Approved);

        // Korxona kartasida ham ko'rinadi.
        var students = (await (await client.GetAsync($"/api/admin/companies/{company.Id}/students"))
            .Content.ReadAsync<IReadOnlyList<CompanyStudent>>())!;
        students.Should().HaveCount(2);
    }

    [Fact]
    public async Task Biriktirish_TakrorVaDavrsizTalaba_SababBilanTashlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group, fullName: "Takror Talaba");
        // Boshqa guruh — unga faol davr biriktirilmagan.
        var orphan = await Factory.CreateStudentAsync(fullName: "Davrsiz Talaba");

        var client = await Factory.LoginAsAdminAsync();
        await client.PostJsonAsync(Url, new { studentIds = new[] { student.Id }, companyId = company.Id });

        var response = await client.PostJsonAsync(Url, new
        {
            studentIds = new[] { student.Id, orphan.Id },
            companyId = company.Id
        });

        var result = (await response.Content.ReadAsync<AssignCompanyResult>())!;
        result.Assigned.Should().Be(0);
        result.Skipped.Should().Be(2);
        result.Errors.Should().Contain(e => e.StudentId == student.Id
            && e.Message == AssignCompanyMessages.AlreadyHereMessage);
        result.Errors.Should().Contain(e => e.StudentId == orphan.Id
            && e.Message == AssignCompanyMessages.NoPeriodMessage);
    }

    [Fact]
    public async Task Biriktirish_BoshqaKorxonagaBiriktirilgan_SababBilanTashlanadi()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var first = await Factory.CreateCompanyAsync(name: "Birinchi Korxona");
        var second = await Factory.CreateCompanyAsync(name: "Ikkinchi Korxona");
        var student = await Factory.CreateStudentAsync(group: group);
        await Factory.CreateApprovedApplicationAsync(student, period, first, tutor.Id);

        var client = await Factory.LoginAsAdminAsync();
        var result = (await (await client.PostJsonAsync(Url, new
        {
            studentIds = new[] { student.Id },
            companyId = second.Id
        })).Content.ReadAsync<AssignCompanyResult>())!;

        result.Assigned.Should().Be(0);
        result.Errors.Single().Message.Should().Contain("Birinchi Korxona");
    }

    [Fact]
    public async Task Biriktirish_FaolBolmaganKorxona_409_BoshRoyxat_400()
    {
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsAdminAsync();
        await client.PatchAsJsonAsync($"/api/admin/companies/{company.Id}/status", new { isActive = false }, JsonDefaults.Options);

        var inactive = await client.PostJsonAsync(Url, new { studentIds = new[] { student.Id }, companyId = company.Id });
        inactive.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var empty = await client.PostJsonAsync(Url, new { studentIds = Array.Empty<Guid>(), companyId = company.Id });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Biriktirish_Tyutor_403()
    {
        var company = await Factory.CreateCompanyAsync();
        var client = await Factory.LoginAsTutorAsync();

        (await client.PostJsonAsync(Url, new { studentIds = new[] { Guid.NewGuid() }, companyId = company.Id }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Handler ichki klass — izoh matnini shu yerda takrorlaymiz (kontrakt sifatida).</summary>
    private const string AssignStudentsToCompanyCommandHandlerComment = "Admin tomonidan biriktirildi.";
}
