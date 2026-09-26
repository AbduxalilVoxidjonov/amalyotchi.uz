using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>POST /api/admin/students/{id}/company</c> — talaba profilidan korxonaga biriktirish / boshqa korxonaga
/// o'tkazish (eski ariza <c>transferred</c>, yangi korxonaga tasdiqlangan ariza).</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentCompanyTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static string Url(Guid studentId) => $"/api/admin/students/{studentId}/company";

    private Task<List<PracticeApplication>> ApplicationsAsync(Guid studentId) =>
        Factory.WithDbAsync(db => db.PracticeApplications.AsNoTracking()
            .Where(a => a.StudentUserId == studentId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync());

    private async Task<(TestGroup Group, TestUser Tutor, PracticePeriod Period)> GroupWithPeriodAsync()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        return (group, tutor, period);
    }

    [Fact]
    public async Task BirinchiBiriktirish_200_TasdiqlanganAriza_Audit()
    {
        var (group, _, period) = await GroupWithPeriodAsync();
        var company = await Factory.CreateCompanyAsync(name: "Birinchi Joy MCHJ");
        var student = await Factory.CreateStudentAsync(group: group);

        var client = await Factory.LoginAsAdminAsync();
        var response = await client.PostJsonAsync(Url(student.Id), new { companyId = company.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<AdminStudentDetail>())!;
        detail.Id.Should().Be(student.Id);
        detail.Company!.Id.Should().Be(company.Id);
        detail.Company.Name.Should().Be("Birinchi Joy MCHJ");
        detail.Application!.Status.Should().Be(ApplicationStatus.Approved);
        detail.Application.Comment.Should().Be("Admin tomonidan biriktirildi.");
        detail.SelectedPeriodId.Should().Be(period.Id);

        var application = (await ApplicationsAsync(student.Id)).Should().ContainSingle().Subject;
        application.Status.Should().Be(ApplicationStatus.Approved);
        application.PeriodId.Should().Be(period.Id);

        var audit = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.Action == AuditAction.StudentCompanyReassigned && l.EntityId == student.Id.ToString()));
        using var changes = JsonDocument.Parse(audit.Changes!);
        changes.RootElement.GetProperty("toCompanyId").GetString().Should().Be(company.Id.ToString());
        changes.RootElement.GetProperty("fromCompanyId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Otkazish_EskiTransferred_YangiApproved_HammaJoydaYangiKorxona()
    {
        var (group, tutor, period) = await GroupWithPeriodAsync();
        var oldCompany = await Factory.CreateCompanyAsync(name: "Eski Korxona Otkazish");
        var newCompany = await Factory.CreateCompanyAsync(name: "Yangi Korxona Otkazish");
        var student = await Factory.CreateStudentAsync(group: group, fullName: $"Otkazish {Guid.NewGuid():N}"[..24]);
        var old = await Factory.CreateApprovedApplicationAsync(student, period, oldCompany, tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.PostJsonAsync(Url(student.Id), new { companyId = newCompany.Id, comment = "  Korxona yopildi  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var detail = (await response.Content.ReadAsync<AdminStudentDetail>())!;
        detail.Company!.Id.Should().Be(newCompany.Id);
        detail.Application!.Status.Should().Be(ApplicationStatus.Approved);
        detail.Application.Comment.Should().Be("Korxona yopildi");

        var apps = await ApplicationsAsync(student.Id);
        apps.Should().HaveCount(2);
        var transferred = apps.Single(a => a.Id == old.Id);
        transferred.Status.Should().Be(ApplicationStatus.Transferred);
        transferred.CompanyId.Should().Be(oldCompany.Id);
        transferred.DecisionComment.Should().Be("Korxona yopildi");
        apps.Single(a => a.Id != old.Id).Should().Match<PracticeApplication>(a =>
            a.Status == ApplicationStatus.Approved && a.CompanyId == newCompany.Id && a.PeriodId == period.Id);

        // Korxona sahifasi — faqat aktiv talabalar (v3.15): eski korxonada yo'q, yangisida bor (approved).
        var oldStudents = (await (await admin.GetAsync($"/api/admin/companies/{oldCompany.Id}/students"))
            .Content.ReadAsync<List<CompanyStudent>>())!;
        oldStudents.Should().NotContain(s => s.StudentId == student.Id);
        var newStudents = (await (await admin.GetAsync($"/api/admin/companies/{newCompany.Id}/students"))
            .Content.ReadAsync<List<CompanyStudent>>())!;
        newStudents.Should().ContainSingle(s => s.StudentId == student.Id)
            .Which.ApplicationStatus.Should().Be(ApplicationStatus.Approved);

        // GET profil ham yangi korxonani qaytaradi.
        var get = (await (await admin.GetAsync($"/api/admin/students/{student.Id}")).Content.ReadAsync<AdminStudentDetail>())!;
        get.Company!.Id.Should().Be(newCompany.Id);

        // Admin ro'yxatidagi korxona ustuni.
        var list = (await (await admin.GetAsync($"/api/admin/students?q={Uri.EscapeDataString(student.FullName)}"))
            .Content.ReadAsync<Paged<StudentRow>>())!;
        list.Items.Should().ContainSingle(r => r.Id == student.Id).Which.Company.Should().Be(newCompany.Name);

        // Davr statistikasi: bitta korxonali talaba, kutilayotgan ariza yo'q; qator — yangi korxona, holat approved.
        var stats = (await (await admin.GetAsync($"/api/admin/practice-periods/{period.Id}/groups/{group.GroupId}/students"))
            .Content.ReadAsync<PeriodGroupStudents>())!;
        stats.Metrics.WithCompanyCount.Should().Be(1);
        stats.Metrics.PendingApplicationsCount.Should().Be(0);
        var row = stats.Students.Should().ContainSingle().Subject;
        row.Company.Should().Be(newCompany.Name);
        row.ApplicationStatus.Should().Be(ApplicationStatus.Approved);

        // Tyutor moderatsiya navbati (statussiz) o'tkazilgan arizani ko'rsatmaydi.
        var tutorClient = await Factory.LoginAsync(tutor);
        var queue = (await (await tutorClient.GetAsync("/api/tutor/applications")).Content.ReadAsync<ApplicationListResponse>())!;
        queue.Items.Where(i => i.StudentId == student.Id).Should().ContainSingle()
            .Which.Status.Should().Be(ApplicationStatus.Approved);

        // Talaba "Amaliyot joyim".
        var studentClient = await Factory.LoginAsStudentAsync(student);
        var place = (await (await studentClient.GetAsync("/api/student/place")).Content.ReadAsync<PracticePlaceDto>())!;
        place.Company.Should().Be(newCompany.Name);
        place.Status.Should().Be(ApplicationStatus.Approved);

        var audit = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.Action == AuditAction.StudentCompanyReassigned && l.EntityId == student.Id.ToString()));
        audit.Changes.Should().Contain(oldCompany.Id.ToString()).And.Contain(newCompany.Id.ToString()).And.Contain("\"approved\"");
        audit.Reason.Should().Be("Korxona yopildi");
    }

    [Fact]
    public async Task Otkazish_KutilayotganAriza_Transferred_OmmaviyBiriktirishToGriIshlaydi()
    {
        var (group, _, period) = await GroupWithPeriodAsync();
        var pendingCompany = await Factory.CreateCompanyAsync();
        var target = await Factory.CreateCompanyAsync(name: "Maqsad Korxona Pending");
        var other = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        var pending = await Factory.WithDbAsync(async db =>
        {
            var app = PracticeApplication.Create(student.Id, period.Id, pendingCompany.Id, 150, null, DateTimeOffset.UtcNow.AddHours(-1));
            db.PracticeApplications.Add(app);
            await db.SaveChangesAsync();
            return app;
        });

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.PostJsonAsync(Url(student.Id), new { companyId = target.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var apps = await ApplicationsAsync(student.Id);
        apps.Single(a => a.Id == pending.Id).Status.Should().Be(ApplicationStatus.Transferred);
        apps.Single(a => a.Id == pending.Id).DecisionComment.Should().Be("Boshqa korxonaga o'tkazildi: Maqsad Korxona Pending.");
        apps.Single(a => a.Id != pending.Id).CompanyId.Should().Be(target.Id);

        // Ommaviy biriktirish: Transferred "joriy" hisoblanmaydi — tasdiqlangan yangi ariza bo'yicha qaror.
        var same = (await (await admin.PostJsonAsync("/api/admin/students/assign-company",
            new { studentIds = new[] { student.Id }, companyId = target.Id })).Content.ReadAsync<AssignCompanyResult>())!;
        same.Errors.Should().ContainSingle().Which.Message.Should().Be(AssignCompanyMessages.AlreadyHereMessage);

        var another = (await (await admin.PostJsonAsync("/api/admin/students/assign-company",
            new { studentIds = new[] { student.Id }, companyId = other.Id })).Content.ReadAsync<AssignCompanyResult>())!;
        another.Errors.Should().ContainSingle().Which.Message.Should().Be(AssignCompanyMessages.OtherCompanyMessage(target.Name));

        // Yana bir o'tkazish: oldingi approved → transferred, jami 3 ariza, faqat bittasi approved.
        (await admin.PostJsonAsync(Url(student.Id), new { companyId = other.Id })).StatusCode.Should().Be(HttpStatusCode.OK);
        apps = await ApplicationsAsync(student.Id);
        apps.Should().HaveCount(3);
        apps.Count(a => a.Status == ApplicationStatus.Transferred).Should().Be(2);
        apps.Should().ContainSingle(a => a.Status == ApplicationStatus.Approved).Which.CompanyId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Xatolar_404_409_400_403()
    {
        var (group, tutor, period) = await GroupWithPeriodAsync();
        var company = await Factory.CreateCompanyAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        var admin = await Factory.LoginAsAdminAsync();

        // 404 — talaba yo'q / korxona yo'q.
        (await admin.PostJsonAsync(Url(Guid.NewGuid()), new { companyId = company.Id }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostJsonAsync(Url(student.Id), new { companyId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 400 — companyId bo'sh, izoh uzun.
        var empty = await admin.PostJsonAsync(Url(student.Id), new { comment = "x" });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorsAsync(empty)).TryGetProperty("CompanyId", out _).Should().BeTrue();
        var longComment = await admin.PostJsonAsync(Url(student.Id), new { companyId = company.Id, comment = new string('a', 501) });
        longComment.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorsAsync(longComment)).TryGetProperty("Comment", out _).Should().BeTrue();

        // 409 — korxona faol emas.
        var inactiveCompany = await Factory.CreateCompanyAsync();
        await admin.PatchAsJsonAsync($"/api/admin/companies/{inactiveCompany.Id}/status", new { isActive = false }, JsonDefaults.Options);
        (await DetailAsync(await admin.PostJsonAsync(Url(student.Id), new { companyId = inactiveCompany.Id }), HttpStatusCode.Conflict))
            .Should().Be(ReassignCompanyMessages.CompanyInactiveMessage);

        // 409 — talaba faol emas.
        var inactive = await Factory.CreateStudentAsync(group: group, active: false);
        (await DetailAsync(await admin.PostJsonAsync(Url(inactive.Id), new { companyId = company.Id }), HttpStatusCode.Conflict))
            .Should().Be(ReassignCompanyMessages.StudentInactiveMessage);

        // 409 — guruhida ochiq davr yo'q.
        var orphan = await Factory.CreateStudentAsync();
        (await DetailAsync(await admin.PostJsonAsync(Url(orphan.Id), new { companyId = company.Id }), HttpStatusCode.Conflict))
            .Should().Be(ReassignCompanyMessages.NoPeriodMessage);

        // 409 — allaqachon shu korxonada.
        await Factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id);
        (await DetailAsync(await admin.PostJsonAsync(Url(student.Id), new { companyId = company.Id }), HttpStatusCode.Conflict))
            .Should().Be(ReassignCompanyMessages.AlreadyHereMessage);
        (await ApplicationsAsync(student.Id)).Should().ContainSingle(a => a.Status == ApplicationStatus.Approved);

        // 403 — tyutor.
        var tutorClient = await Factory.LoginAsync(tutor);
        (await tutorClient.PostJsonAsync(Url(student.Id), new { companyId = company.Id }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OtkazilgandanKeyin_CheckIn_YangiKorxonaQrVaGeofence_EskiQrRad()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var oldQr = scene.Qr();
        var newCompany = await Factory.CreateCompanyAsync(StudentTestData.FarLat, StudentTestData.CompanyLng, 150, "Yangi Joy CheckIn");

        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PostJsonAsync(Url(scene.Student.Id), new { companyId = newCompany.Id }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var today = (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!;
        today.Place!.Company.Should().Be("Yangi Joy CheckIn");

        await using var noPhoto = await Factory.WithoutPhotoRequirementAsync();
        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));

            // Eski korxona joyida, eski QR bilan — rad etiladi (yangi korxona hisobida).
            var rejected = await scene.Client.PostJsonAsync("/api/student/checkin",
                Factory.Geo(StudentTestData.CompanyLat, StudentTestData.CompanyLng, qr: oldQr));
            rejected.StatusCode.Should().Be(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());

            // Eski QR yangi joyda ham yaroqsiz.
            var wrongQr = await scene.Client.PostJsonAsync("/api/student/checkin",
                Factory.Geo(StudentTestData.FarLat, StudentTestData.CompanyLng, qr: oldQr));
            wrongQr.StatusCode.Should().Be(HttpStatusCode.Conflict);

            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 6)));
            var accepted = await scene.Client.PostJsonAsync("/api/student/checkin",
                Factory.Geo(StudentTestData.FarLat, StudentTestData.CompanyLng, qr: newCompany.CheckInQrPayload));
            accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var events = await Factory.WithDbAsync(db => db.AttendanceEvents.AsNoTracking()
            .Where(e => e.StudentUserId == scene.Student.Id).ToListAsync());
        events.Should().HaveCount(3).And.OnlyContain(e => e.CompanyId == newCompany.Id);
        events.Should().Contain(e => !e.Accepted && e.RejectReason == CheckInRejectReason.QrInvalid);
        events.Should().ContainSingle(e => e.Accepted);
    }

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Clone();
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("detail").GetString();
    }
}
