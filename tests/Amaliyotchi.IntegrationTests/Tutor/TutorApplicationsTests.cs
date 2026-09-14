using System.Net;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorApplicationsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Royxat_Hisoblagichlar_VaStatusFiltri()
    {
        var s = await Factory.CreateTutorScenarioAsync(approved: false);
        var approvedStudent = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(approvedStudent, s.Period, s.Company, s.Tutor.Id);

        var all = (await (await s.Client.GetAsync("/api/tutor/applications")).Content.ReadAsync<ApplicationListResponse>())!;
        all.Counts.Submitted.Should().Be(1);
        all.Counts.Approved.Should().Be(1);
        all.Counts.Rejected.Should().Be(0);
        all.Items.Should().HaveCount(2);
        all.Items[0].Status.Should().Be(ApplicationStatus.Submitted, "yangi arizalar birinchi");

        var submitted = (await (await s.Client.GetAsync("/api/tutor/applications?status=submitted")).Content.ReadAsync<ApplicationListResponse>())!;
        var item = submitted.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(s.Application.Id);
        item.StudentId.Should().Be(s.Student.Id);
        item.Company.Should().Be(s.Company.Name);
        item.Group.Should().Be(s.Group.GroupName);
        item.Course.Should().Be(s.Group.Course);
        item.HemisId.Should().NotBeNullOrEmpty();
        item.DecidedAt.Should().BeNull();
    }

    [Fact]
    public async Task Tafsilot_KorxonaKoordinataShartnoma_BegonaGuruh404()
    {
        var s = await Factory.CreateTutorScenarioAsync(approved: false);
        var contract = await Factory.CreateStoredFileAsync(s.Student.Id);
        var student = await Factory.CreateStudentAsync(group: s.Group);
        var withContract = await Factory.WithDbAsync(async db =>
        {
            var app = PracticeApplication.Create(student.Id, s.Period.Id, s.Company.Id, 300, contract.Id, Factory.UtcNow());
            db.PracticeApplications.Add(app);
            await db.SaveChangesAsync();
            return app;
        });

        var response = await s.Client.GetAsync($"/api/tutor/applications/{withContract.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadAsync<ApplicationDetail>())!;
        detail.Company.Should().Be(s.Company.Name);
        detail.CompanyDetails.Tin.Should().Be(s.Company.Tin);
        detail.CompanyDetails.SupervisorPhone.Should().StartWith("+998");
        detail.Coords.Lat.Should().BeApproximately(41.3111, 0.0001);
        detail.Coords.Lng.Should().BeApproximately(69.2797, 0.0001);
        detail.RadiusM.Should().Be(300);
        detail.Contract.Should().NotBeNull();
        detail.Contract!.Url.Should().Be($"/api/files/{contract.Id}");
        detail.Contract.Name.Should().Be("shartnoma.pdf");
        detail.Contract.SizeBytes.Should().BeGreaterThan(0);
        detail.Checklist.Should().BeEmpty();

        var strangerTutor = await Factory.LoginAsTutorAsync();
        (await strangerTutor.GetAsync($"/api/tutor/applications/{withContract.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await s.Client.GetAsync($"/api/tutor/applications/{Guid.CreateVersion7()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Qaror_Tasdiqlash_RadiusKorxonagaYoziladi_IkkinchiMarta409()
    {
        var s = await Factory.CreateTutorScenarioAsync(approved: false);

        var response = await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision",
            new { decision = "approve", radiusM = 250, checklist = new[] { 0, 1, 2, 6 }, comment = "Hujjatlar to'liq" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadAsync<ApplicationDecisionResponse>())!;
        result.Id.Should().Be(s.Application.Id);
        result.Status.Should().Be(ApplicationStatus.Approved);

        await Factory.WithDbAsync(async db =>
        {
            (await db.Companies.AsNoTracking().SingleAsync(c => c.Id == s.Company.Id)).RadiusM.Should().Be(250);
            var app = await db.PracticeApplications.AsNoTracking().SingleAsync(a => a.Id == s.Application.Id);
            app.Checklist.Should().Equal(0, 1, 2, 6);
            app.DecidedByUserId.Should().Be(s.Tutor.Id);
            (await db.AuditLogs.AnyAsync(l => l.Action == Amaliyotchi.Domain.Enums.AuditAction.ApplicationApproved && l.EntityId == s.Application.Id.ToString())).Should().BeTrue();
            (await db.AuditLogs.AnyAsync(l => l.Action == Amaliyotchi.Domain.Enums.AuditAction.RadiusChanged && l.EntityId == s.Company.Id.ToString())).Should().BeTrue();
        });

        var again = await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision",
            new { decision = "approve", radiusM = 250, checklist = Array.Empty<int>() });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Qaror_Qaytarish_IzohsizValidatsiya_IzohBilanRevisionNeeded()
    {
        var s = await Factory.CreateTutorScenarioAsync(approved: false);

        var noComment = await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision",
            new { decision = "return", radiusM = 150, checklist = Array.Empty<int>() });
        noComment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noComment.Content.ReadAsStringAsync()).Should().Contain("Comment");

        var badRadius = await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision",
            new { decision = "approve", radiusM = 175, checklist = Array.Empty<int>() });
        badRadius.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var returned = await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision",
            new { decision = "return", comment = "Shartnomada imzo yo'q" });
        returned.StatusCode.Should().Be(HttpStatusCode.OK);
        (await returned.Content.ReadAsync<ApplicationDecisionResponse>())!.Status.Should().Be(ApplicationStatus.RevisionNeeded);

        var list = (await (await s.Client.GetAsync("/api/tutor/applications?status=revisionNeeded")).Content.ReadAsync<ApplicationListResponse>())!;
        list.Items.Should().ContainSingle(i => i.Id == s.Application.Id);
    }

    [Fact]
    public async Task Qaror_BegonaTyutor404_Talaba403()
    {
        var s = await Factory.CreateTutorScenarioAsync(approved: false);
        var strangerTutor = await Factory.LoginAsTutorAsync();
        var body = new { decision = "reject", comment = "Korxona yo'nalishga mos emas" };

        (await strangerTutor.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync("/api/tutor/applications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await s.Client.PostJsonAsync($"/api/tutor/applications/{s.Application.Id}/decision", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        await Factory.WithDbAsync(async db =>
            (await db.PracticeApplications.AsNoTracking().SingleAsync(a => a.Id == s.Application.Id)).Status.Should().Be(ApplicationStatus.Rejected));
    }
}
