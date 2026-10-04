using System.Net;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Tutor.Nav;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Faces;

/// <summary>Tyutor/admin: ro'yxat, approve/reject/reset, ko'lam (404), nav soni, profil <c>face</c> bloki, bildirishnoma.</summary>
[Collection(ApiCollection.Name)]
public sealed class TutorFaceEnrollmentTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private async Task<(TestUser Tutor, HttpClient TutorClient, TestUser Student)> SceneAsync(bool enroll = true)
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        if (enroll)
            await (await Factory.LoginAsStudentAsync(student)).EnrollAsync("t-" + student.Id);
        return (tutor, await Factory.LoginAsync(tutor), student);
    }

    [Fact]
    public async Task Royxat_Nav_Approve_Profil_Bildirishnoma()
    {
        var (_, client, student) = await SceneAsync();

        var list = await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments");
        var item = list.Items.Should().ContainSingle().Subject;
        item.StudentId.Should().Be(student.Id);
        item.Status.Should().Be(StudentFaceStatus.Pending);
        item.PhotoUrl.Should().StartWith("/api/files/");
        (await client.GetAsync(item.PhotoUrl)).StatusCode.Should().Be(HttpStatusCode.OK, "tyutor etalon rasmni ko'radi");

        var nav = await client.GetFromJsonSafeAsync<TutorNavDto>("/api/tutor/nav");
        nav.Counts.PendingFaceEnrollments.Should().Be(1);
        (await client.GetStringAsync("/api/tutor/nav")).Should().Contain("\"pendingFaceEnrollments\":1");

        var approve = await client.PostAsync($"/api/tutor/students/{student.Id}/face/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await approve.Content.ReadAsync<StudentFaceDto>())!.Status.Should().Be(StudentFaceStatus.Approved);

        (await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments")).Items.Should().BeEmpty();
        (await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments?status=approved")).Items.Should().ContainSingle();
        (await client.GetFromJsonSafeAsync<TutorNavDto>("/api/tutor/nav")).Counts.PendingFaceEnrollments.Should().Be(0);

        var detail = await client.GetFromJsonSafeAsync<TutorStudentDetail>($"/api/tutor/students/{student.Id}");
        detail.Face.Status.Should().Be(StudentFaceStatus.Approved);
        detail.Face.ReviewedAt.Should().NotBeNull();

        (await client.PostAsync($"/api/tutor/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Factory.Messenger.CallsTo(student.TelegramId!.Value).Should().ContainSingle(c => c.Text.Contains("tasdiqlandi"));
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Action == AuditAction.FaceEnrollmentApproved))).Should().BeTrue();
    }

    [Fact]
    public async Task Reject_SababMajburiy_409_Bildirishnoma_Reset()
    {
        var (_, client, student) = await SceneAsync();
        var url = $"/api/tutor/students/{student.Id}/face";

        var empty = await client.PostJsonAsync($"{url}/reject", new { reason = "  " });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empty.ProblemAsync()).FirstError("Reason").Should().Be("Rad etish sababi majburiy.");
        (await client.PostJsonAsync($"{url}/reject", new { reason = new string('a', 501) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reject = await client.PostJsonAsync($"{url}/reject", new { reason = "Yuz xira" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        var dto = (await reject.Content.ReadAsync<StudentFaceDto>())!;
        dto.Status.Should().Be(StudentFaceStatus.Rejected);
        dto.RejectReason.Should().Be("Yuz xira");
        (await client.PostJsonAsync($"{url}/reject", new { reason = "yana" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Factory.Messenger.CallsTo(student.TelegramId!.Value).Should().ContainSingle(c => c.Text.Contains("Yuz xira"));

        var rejectedList = await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments?status=rejected");
        rejectedList.Items.Single().RejectReason.Should().Be("Yuz xira");

        var reset = await client.PostAsync($"{url}/reset", null);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reset.Content.ReadAsync<StudentFaceDto>())!.Status.Should().Be(StudentFaceStatus.None);
        (await client.PostAsync($"{url}/reset", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Action == AuditAction.FaceEnrollmentReset))).Should().BeTrue();
    }

    [Fact]
    public async Task BoshqaTyutor_404_RoyxatdaKorinmaydi_Admin_Koradi()
    {
        var (_, _, student) = await SceneAsync();
        var other = await Factory.LoginAsTutorAsync();

        (await other.PostAsync($"/api/tutor/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostJsonAsync($"/api/tutor/students/{student.Id}/face/reject", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/tutor/students/{student.Id}/face/reset", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments")).Items.Should().BeEmpty();
        (await other.GetFromJsonSafeAsync<TutorNavDto>("/api/tutor/nav")).Counts.PendingFaceEnrollments.Should().Be(0);

        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/tutor/face-enrollments")).Items
            .Should().Contain(i => i.StudentId == student.Id);
        (await admin.PostAsync($"/api/tutor/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetStringAsync($"/api/admin/students/{student.Id}")).Should().Contain("\"face\":{\"status\":\"approved\"");

        var studentClient = await Factory.LoginAsStudentAsync(student);
        (await studentClient.GetAsync("/api/tutor/face-enrollments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtalonYoq_Approve409_ProfildaNone()
    {
        var (_, client, student) = await SceneAsync(enroll: false);
        (await client.PostAsync($"/api/tutor/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonSafeAsync<TutorStudentDetail>($"/api/tutor/students/{student.Id}")).Face.Status
            .Should().Be(StudentFaceStatus.None);
    }
}
