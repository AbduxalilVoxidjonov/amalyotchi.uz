using System.Net;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Admin.Nav;
using Amaliyotchi.Application.Features.Supervision.Students;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Faces;

/// <summary>Admin: ro'yxat, approve/reject/reset, nav soni, profil <c>face</c> bloki, bildirishnoma. Baza umumiy —
/// ro'yxat va sonlar shu testning talabasi bo'yicha (filtr / farq) tekshiriladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminFaceEnrollmentTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private async Task<(HttpClient Client, TestUser Student)> SceneAsync(bool enroll = true)
    {
        var group = await Factory.CreateGroupAsync();
        var student = await Factory.CreateStudentAsync(group: group);
        if (enroll)
            await (await Factory.LoginAsStudentAsync(student)).EnrollAsync("t-" + student.Id);
        return (await Factory.LoginAsAdminAsync(), student);
    }

    [Fact]
    public async Task Royxat_Nav_Approve_Profil_Bildirishnoma()
    {
        var (client, student) = await SceneAsync();

        var list = await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/admin/face-enrollments");
        var item = list.Items.Should().ContainSingle(i => i.StudentId == student.Id).Subject;
        item.Status.Should().Be(StudentFaceStatus.Pending);
        item.PhotoUrl.Should().StartWith("/api/files/");
        (await client.GetAsync(item.PhotoUrl)).StatusCode.Should().Be(HttpStatusCode.OK, "admin etalon rasmni ko'radi");

        var nav = await client.GetFromJsonSafeAsync<AdminNavDto>("/api/admin/nav");
        nav.Counts.PendingFaceEnrollments.Should().Be(list.Items.Count, "nav soni sukut (pending) ro'yxati uzunligi");
        (await client.GetStringAsync("/api/admin/nav")).Should().Contain("\"pendingFaceEnrollments\":");

        var approve = await client.PostAsync($"/api/admin/students/{student.Id}/face/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await approve.Content.ReadAsync<StudentFaceDto>())!.Status.Should().Be(StudentFaceStatus.Approved);

        (await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/admin/face-enrollments")).Items
            .Should().NotContain(i => i.StudentId == student.Id);
        (await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/admin/face-enrollments?status=approved")).Items
            .Should().ContainSingle(i => i.StudentId == student.Id);
        (await client.GetFromJsonSafeAsync<AdminNavDto>("/api/admin/nav")).Counts.PendingFaceEnrollments
            .Should().Be(nav.Counts.PendingFaceEnrollments - 1);

        var detail = await client.GetFromJsonSafeAsync<StudentDetail>($"/api/admin/students/{student.Id}");
        detail.Face.Status.Should().Be(StudentFaceStatus.Approved);
        detail.Face.ReviewedAt.Should().NotBeNull();

        (await client.PostAsync($"/api/admin/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        Factory.Messenger.CallsTo(student.TelegramId!.Value).Should().ContainSingle(c => c.Text.Contains("tasdiqlandi"));
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Action == AuditAction.FaceEnrollmentApproved))).Should().BeTrue();
    }

    [Fact]
    public async Task Reject_SababMajburiy_409_Bildirishnoma_Reset()
    {
        var (client, student) = await SceneAsync();
        var url = $"/api/admin/students/{student.Id}/face";

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

        var rejectedList = await client.GetFromJsonSafeAsync<FaceEnrollmentList>("/api/admin/face-enrollments?status=rejected");
        rejectedList.Items.Single(i => i.StudentId == student.Id).RejectReason.Should().Be("Yuz xira");

        var reset = await client.PostAsync($"{url}/reset", null);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reset.Content.ReadAsync<StudentFaceDto>())!.Status.Should().Be(StudentFaceStatus.None);
        (await client.PostAsync($"{url}/reset", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Action == AuditAction.FaceEnrollmentReset))).Should().BeTrue();
    }

    [Fact]
    public async Task Talaba_403_YoqTalaba_404_ProfildaFace()
    {
        var (admin, student) = await SceneAsync();

        var missing = Guid.NewGuid();
        (await admin.PostAsync($"/api/admin/students/{missing}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostJsonAsync($"/api/admin/students/{missing}/face/reject", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync($"/api/admin/students/{missing}/face/reset", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await admin.PostAsync($"/api/admin/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetStringAsync($"/api/admin/students/{student.Id}")).Should().Contain("\"face\":{\"status\":\"approved\"");

        var studentClient = await Factory.LoginAsStudentAsync(student);
        (await studentClient.GetAsync("/api/admin/face-enrollments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.PostAsync($"/api/admin/students/{student.Id}/face/reset", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtalonYoq_Approve409_ProfildaNone()
    {
        var (client, student) = await SceneAsync(enroll: false);
        (await client.PostAsync($"/api/admin/students/{student.Id}/face/approve", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonSafeAsync<StudentDetail>($"/api/admin/students/{student.Id}")).Face.Status
            .Should().Be(StudentFaceStatus.None);
    }
}
