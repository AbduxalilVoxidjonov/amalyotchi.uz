using System.Net;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Tutor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Admin — talaba profili: <c>GET /api/admin/students/{id}</c>, <c>.../attendance</c>, <c>.../diaries</c>.
/// Umumiy bloklar tyutor profili bilan bir xil (ayni handler), bu yerda adminga xos qismlar tekshiriladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentProfileTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Profil_200_TyutorKafedraTelegramVaUmumiyBloklar()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[0]);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[1], late: true);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 5);

        var admin = await Factory.LoginAsAdminAsync();
        var response = await admin.GetAsync($"/api/admin/students/{s.Student.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadAsync<AdminStudentDetail>())!;

        // Adminga xos maydonlar
        detail.GroupId.Should().Be(s.Group.GroupId);
        detail.Department.Should().NotBeNullOrEmpty();
        detail.TelegramLinked.Should().BeTrue();
        detail.AdminStatus.Should().Be(AdminStudentStatus.Flagged, "uch kunlik davrda davomat 70% dan past");
        detail.Tutor.Should().NotBeNull();
        detail.Tutor!.Id.Should().Be(s.Tutor.Id);
        detail.Tutor.FullName.Should().Be(s.Tutor.FullName);

        // Tyutor profilidagi bloklar — bir xil qiymatlar
        var fromTutor = (await (await s.Client.GetAsync($"/api/tutor/students/{s.Student.Id}")).Content
            .ReadAsync<TutorStudentDetail>())!;

        detail.Id.Should().Be(fromTutor.Id);
        detail.Name.Should().Be(fromTutor.Name);
        detail.HemisId.Should().Be(fromTutor.HemisId);
        detail.Group.Should().Be(fromTutor.Group);
        detail.Course.Should().Be(fromTutor.Course);
        detail.Faculty.Should().Be(fromTutor.Faculty);
        detail.Direction.Should().Be(fromTutor.Direction);
        detail.Status.Should().Be(StudentStatus.Active);
        detail.State.Should().Be(fromTutor.State);
        detail.Phone.Should().Be(fromTutor.Phone);
        detail.Company!.Id.Should().Be(s.Company.Id);
        detail.Application!.Status.Should().Be(ApplicationStatus.Approved);
        detail.Period!.Id.Should().Be(s.Period.Id);
        detail.Attendance.Should().Be(fromTutor.Attendance);
        detail.Diary.Should().Be(fromTutor.Diary);
        detail.Grade.Should().Be(fromTutor.Grade);
    }

    [Fact]
    public async Task Profil_TyutorBiriktirilmagan_VaTelegramsizTalaba_Ham_Korinadi()
    {
        var group = await Factory.CreateGroupAsync();
        var student = await Factory.CreateUnlinkedStudentAsync(group, "Ulanmagan Profil");

        var admin = await Factory.LoginAsAdminAsync();
        var detail = (await (await admin.GetAsync($"/api/admin/students/{student.Id}")).Content
            .ReadAsync<AdminStudentDetail>())!;

        detail.Name.Should().Be("Ulanmagan Profil");
        detail.Tutor.Should().BeNull("guruhga tyutor biriktirilmagan");
        detail.TelegramLinked.Should().BeFalse();
        detail.AdminStatus.Should().Be(AdminStudentStatus.Unlinked);
        detail.Period.Should().BeNull();
        detail.Company.Should().BeNull();
        detail.Grade.Should().BeNull();
    }

    [Fact]
    public async Task Davomat_VaKundaliklar_TyutorEndpointi_Bilan_BirXil()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[0]);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 4);

        var admin = await Factory.LoginAsAdminAsync();

        var adminDays = (await (await admin.GetAsync($"/api/admin/students/{s.Student.Id}/attendance")).Content
            .ReadAsync<List<StudentAttendanceDay>>())!;
        var tutorDays = (await (await s.Client.GetAsync($"/api/tutor/students/{s.Student.Id}/attendance")).Content
            .ReadAsync<List<StudentAttendanceDay>>())!;
        adminDays.Should().NotBeEmpty().And.BeEquivalentTo(tutorDays);

        var adminDiaries = (await (await admin.GetAsync($"/api/admin/students/{s.Student.Id}/diaries")).Content
            .ReadAsync<List<TutorDiaryEntry>>())!;
        adminDiaries.Should().HaveCount(1);
        adminDiaries[0].Score.Should().Be(4);
    }

    [Fact]
    public async Task Davomat_TeskariOraliq_400()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var admin = await Factory.LoginAsAdminAsync();

        var response = await admin.GetAsync(
            $"/api/admin/students/{s.Student.Id}/attendance?from=2026-10-10&to=2026-10-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kundalikni_Baholash_Admin_Ham_Qila_Oladi()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        var entry = await Factory.AddDiaryAsync(s.Student, s.Period, days[0]);
        var second = await Factory.AddDiaryAsync(s.Student, s.Period, days[1]);

        var admin = await Factory.LoginAsAdminAsync();

        var scored = await admin.PostJsonAsync(
            $"/api/admin/diaries/{entry.Id}/review", new { action = "score", score = 5, comment = "Ajoyib" });
        scored.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await scored.Content.ReadAsync<TutorDiaryEntry>())!;
        dto.Status.Should().Be(DiaryStatus.Approved);
        dto.Score.Should().Be(5);
        dto.Comment.Should().Be("Ajoyib");

        // Ball profil statistikasiga ham tushadi.
        var detail = (await (await admin.GetAsync($"/api/admin/students/{s.Student.Id}")).Content
            .ReadAsync<AdminStudentDetail>())!;
        detail.Diary.ScoredCount.Should().Be(1);
        detail.Diary.Avg.Should().Be(5);

        // Ko'rib chiqilgani qayta baholanmaydi; validatsiya tyutornikidek.
        (await admin.PostJsonAsync($"/api/admin/diaries/{entry.Id}/review", new { action = "approve" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostJsonAsync($"/api/admin/diaries/{second.Id}/review", new { action = "score", score = 7 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/admin/diaries/{second.Id}/review", new { action = "rewrite" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rewrite = await admin.PostJsonAsync(
            $"/api/admin/diaries/{second.Id}/review", new { action = "rewrite", comment = "Batafsilroq yozing" });
        rewrite.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rewrite.Content.ReadAsync<TutorDiaryEntry>())!.Status.Should().Be(DiaryStatus.Rewrite);

        // Audit: tekshiruvchi — admin.
        await Factory.WithDbAsync(async db =>
            (await db.AuditLogs.CountAsync(l => l.Action == AuditAction.DiaryReviewed
                && (l.EntityId == entry.Id.ToString() || l.EntityId == second.Id.ToString()))).Should().Be(2));

        // Tyutor admin yo'liga kira olmaydi (o'z talabasi bo'lsa ham) — o'z endpoint'i bor.
        var third = await Factory.AddDiaryAsync(s.Student, s.Period, Factory.Today());
        (await s.Client.PostJsonAsync($"/api/admin/diaries/{third.Id}/review", new { action = "approve" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NotogriId_404_Tyutor_403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var admin = await Factory.LoginAsAdminAsync();

        foreach (var url in new[]
                 {
                     $"/api/admin/students/{Guid.NewGuid()}",
                     $"/api/admin/students/{Guid.NewGuid()}/attendance",
                     $"/api/admin/students/{Guid.NewGuid()}/diaries"
                 })
        {
            (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, url);
        }

        // Tyutor admin endpoint'iga kira olmaydi (o'z talabasi bo'lsa ham).
        (await s.Client.GetAsync($"/api/admin/students/{s.Student.Id}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }
}
