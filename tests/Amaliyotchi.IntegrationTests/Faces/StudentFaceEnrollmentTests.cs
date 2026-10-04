using System.Net;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Faces;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Faces;

/// <summary><c>GET/POST /api/student/face</c> — soxta yuz dvigateli (<see cref="FakeFaceEngine"/>) bilan.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentFaceEnrollmentTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private async Task<(TestUser Student, HttpClient Client)> StudentAsync()
    {
        var student = await Factory.CreateStudentAsync();
        return (student, await Factory.LoginAsStudentAsync(student));
    }

    [Fact]
    public async Task Get_Yuborilmagan_None_RequiredSozlamadan()
    {
        var (_, client) = await StudentAsync();

        var face = await client.GetFromJsonSafeAsync<StudentFaceDto>("/api/student/face");
        face.Should().Be(new StudentFaceDto(StudentFaceStatus.None, false, null, null, null, null));

        await using (await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true"))
            (await client.GetFromJsonSafeAsync<StudentFaceDto>("/api/student/face")).Required.Should().BeTrue();

        // JSON shakli: status camelCase string.
        var raw = await client.GetStringAsync("/api/student/face");
        raw.Should().Contain("\"status\":\"none\"").And.Contain("\"photoUrl\":null");
    }

    [Fact]
    public async Task Post_Pending_RasmSaqlanadi_Audit_TalabaRasminiKoradi()
    {
        var (student, client) = await StudentAsync();

        var face = await client.EnrollAsync("ali");

        face.Status.Should().Be(StudentFaceStatus.Pending);
        face.PhotoUrl.Should().StartWith("/api/files/");
        face.SubmittedAt.Should().NotBeNull();
        face.ReviewedAt.Should().BeNull();
        var got = await client.GetFromJsonSafeAsync<StudentFaceDto>("/api/student/face");
        got.Status.Should().Be(StudentFaceStatus.Pending);
        got.PhotoUrl.Should().Be(face.PhotoUrl);
        got.SubmittedAt.Should().BeCloseTo(face.SubmittedAt!.Value, TimeSpan.FromMilliseconds(1));

        var enrollment = await Factory.WithDbAsync(db => db.StudentFaceEnrollments.SingleAsync(f => f.StudentUserId == student.Id));
        enrollment.Embedding.Should().HaveCount(FakeFaceEngine.Dimensions);
        enrollment.ConsentAt.Should().Be(enrollment.SubmittedAt);
        var stored = await Factory.WithDbAsync(db => db.StoredFiles.SingleAsync(f => f.Id == enrollment.PhotoFileId));
        stored.Kind.Should().Be(StoredFileKind.FacePhoto);
        stored.UploadedByUserId.Should().Be(student.Id);

        var file = await client.GetAsync(face.PhotoUrl);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(FakeFaceEngine.Photo("ali"));

        (await Factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l =>
            l.Action == AuditAction.FaceEnrollmentSubmitted && l.EntityId == enrollment.Id.ToString())))
            .Should().BeTrue();

        // Begona talaba bu rasmni ko'rmaydi.
        var (_, other) = await StudentAsync();
        (await other.GetAsync(face.PhotoUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_PendingniAlmashtiradi_RadEtilgandanKeyinHam()
    {
        var (student, client) = await StudentAsync();
        var first = await client.EnrollAsync("ali");
        var second = await client.EnrollAsync("ali");
        second.PhotoUrl.Should().NotBe(first.PhotoUrl);
        second.Status.Should().Be(StudentFaceStatus.Pending);

        await Factory.WithDbAsync(async db =>
        {
            var e = await db.StudentFaceEnrollments.SingleAsync(f => f.StudentUserId == student.Id);
            e.Reject(student.Id, "Xira", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        (await client.GetFromJsonSafeAsync<StudentFaceDto>("/api/student/face")).RejectReason.Should().Be("Xira");

        var third = await client.EnrollAsync("ali");
        third.Status.Should().Be(StudentFaceStatus.Pending);
        third.RejectReason.Should().BeNull();
        (await Factory.WithDbAsync(db => db.StudentFaceEnrollments.CountAsync(f => f.StudentUserId == student.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Post_Tasdiqlangan_409()
    {
        var (student, client) = await StudentAsync();
        await client.EnrollAsync("ali");
        await Factory.WithDbAsync(async db =>
        {
            var e = await db.StudentFaceEnrollments.SingleAsync(f => f.StudentUserId == student.Id);
            e.Approve(student.Id, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        using var form = FaceTestData.FaceForm(FakeFaceEngine.Photo("ali"));
        var response = await client.PostAsync("/api/student/face", form);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ProblemAsync()).GetProperty("detail").GetString()
            .Should().Be("Yuz allaqachon tasdiqlangan. O'zgartirish uchun tyutoringizga murojaat qiling.");
    }

    [Theory]
    [InlineData(null, "Rozilik berilishi kerak.")]
    [InlineData("false", "Rozilik berilishi kerak.")]
    public async Task Post_RoziliksIz_400_ErrorsConsent(string? consent, string message)
    {
        var (_, client) = await StudentAsync();
        using var form = FaceTestData.FaceForm(FakeFaceEngine.Photo("ali"), consent);

        var response = await client.PostAsync("/api/student/face", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemAsync()).FirstError("Consent").Should().Be(message);
    }

    [Fact]
    public async Task Post_YuzYoq_KopYuz_OqibBolmaydi_400_ErrorsPhoto()
    {
        var (student, client) = await StudentAsync();

        async Task<string> ErrorFor(byte[] bytes)
        {
            using var form = FaceTestData.FaceForm(bytes);
            var response = await client.PostAsync("/api/student/face", form);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            return (await response.ProblemAsync()).FirstError("Photo");
        }

        (await ErrorFor(FakeFaceEngine.NoFace))
            .Should().Be("Rasmda yuz topilmadi. Yuzingiz aniq ko'rinadigan qilib qayta suratga oling.");
        (await ErrorFor(FakeFaceEngine.Group(2, "ali"))).Should().Be("Rasmda faqat bitta yuz bo'lishi kerak.");
        (await ErrorFor(FakeFaceEngine.Unreadable)).Should().Contain("o'qib bo'lmadi");

        (await Factory.WithDbAsync(db => db.StudentFaceEnrollments.AnyAsync(f => f.StudentUserId == student.Id))).Should().BeFalse();
        (await Factory.WithDbAsync(db => db.StoredFiles.AnyAsync(f => f.UploadedByUserId == student.Id))).Should().BeFalse();
    }

    [Fact]
    public async Task Post_FaylQoidalari_400()
    {
        var (_, client) = await StudentAsync();

        using (var pdf = FaceTestData.FaceForm(FakeFaceEngine.Photo("ali"), contentType: "application/pdf"))
        {
            var response = await client.PostAsync("/api/student/face", pdf);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ProblemAsync()).FirstError("Photo").Should().Contain("Faqat rasm");
        }

        using (var none = FaceTestData.FaceForm(null))
        {
            var response = await client.PostAsync("/api/student/face", none);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ProblemAsync()).FirstError("Photo").Should().Be("Yuz rasmi majburiy.");
        }
    }

    [Fact]
    public async Task Post_ModellarYoq_503()
    {
        var (_, client) = await StudentAsync();
        Factory.FaceEngine.IsReady = false;
        try
        {
            using var form = FaceTestData.FaceForm(FakeFaceEngine.Photo("ali"));
            var response = await client.PostAsync("/api/student/face", form);
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.ProblemAsync()).GetProperty("detail").GetString().Should().Contain("vaqtincha ishlamayapti");
        }
        finally
        {
            Factory.FaceEngine.IsReady = true;
        }
    }

    [Fact]
    public async Task Tyutor_TalabaEndpointigaKirmaydi_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/face")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

internal static class HttpJsonExtensions
{
    public static async Task<T> GetFromJsonSafeAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadAsync<T>())!;
    }
}
