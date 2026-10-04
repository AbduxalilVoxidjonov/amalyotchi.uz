using System.Net;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Faces;

/// <summary>Check-in'da yuzni tasdiqlash (<c>faceVerificationEnabled</c>): rad sabablari, ball, sozlama o'chiq — no-op.
/// Soat <see cref="MutableClock"/> bilan 09:05 ga muzlatiladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInFaceTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private async Task<HttpResponseMessage> CheckInAsync(StudentScene scene, DateOnly day, byte[]? photo)
    {
        fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
        try
        {
            using var form = Factory.GeoForm(photo: photo is null ? null : ("selfi.jpg", "image/jpeg", photo), qr: scene.Qr());
            return await scene.Client.PostAsync("/api/student/checkin", form);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    private Task<List<AttendanceEvent>> EventsAsync(StudentScene scene)
        => Factory.WithDbAsync(db => db.AttendanceEvents.Where(e => e.StudentUserId == scene.Student.Id)
            .OrderBy(e => e.ReceivedAt).ToListAsync());

    [Fact]
    public async Task SozlamaOchiq_YuzTekshirilmaydi_BallNull()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var calls = Factory.FaceEngine.Calls;

        var response = await CheckInAsync(scene, day, FakeFaceEngine.NoFace);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<TodayDto>())!.Checkin.FaceRequired.Should().BeFalse();
        Factory.FaceEngine.Calls.Should().Be(calls, "sozlama o'chiq — dvigatel chaqirilmaydi");
        (await Factory.WithDbAsync(db => db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id)))
            .FaceMatchScore.Should().BeNull();
    }

    [Fact]
    public async Task Yoqilgan_RasmMajburiy_PhotoSozlamasidanQatiNazar()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var photoOff = await Factory.UseSettingAsync(SettingKeys.CheckInPhotoRequired, "false");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, photo: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemAsync()).FirstError("Photo").Should().Be("Check-in uchun rasm majburiy.");
        (await EventsAsync(scene)).Should().BeEmpty();
    }

    [Fact]
    public async Task Yoqilgan_EtalonYoq_FaceNotEnrolled_HodisaYoziladi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, FakeFaceEngine.Photo("ali"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ProblemAsync();
        problem.GetProperty("detail").GetString().Should().Be("Avval yuzingizni tasdiqlang.");
        problem.GetProperty("rejectReason").GetString().Should().Be("faceNotEnrolled");

        var attempt = (await EventsAsync(scene)).Should().ContainSingle().Subject;
        attempt.Accepted.Should().BeFalse();
        attempt.RejectReason.Should().Be(CheckInRejectReason.FaceNotEnrolled);
        attempt.PhotoFileId.Should().NotBeNull("rad etilgan urinish rasmi saqlanadi");
        attempt.FaceMatchScore.Should().BeNull();
    }

    [Fact]
    public async Task Yoqilgan_PendingEtalon_Mos_200_BallSaqlanadi_TyutorKoradi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("vali");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, FakeFaceEngine.Photo("vali", 81));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var today = (await response.Content.ReadAsync<TodayDto>())!;
        today.Checkin.Status.Should().Be(AttendanceStatus.Present);
        today.Checkin.FaceRequired.Should().BeTrue();

        (await Factory.WithDbAsync(db => db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id)))
            .FaceMatchScore.Should().Be(81);
        (await EventsAsync(scene)).Single().FaceMatchScore.Should().Be(81);

        // Tyutor kun-bakun davomatida: kun va urinish balli.
        var tutor = await Factory.LoginAsync(scene.Tutor);
        var days = await tutor.GetFromJsonSafeAsync<List<StudentAttendanceDay>>(
            $"/api/tutor/students/{scene.Student.Id}/attendance?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
        var row = days.Single();
        row.FaceMatchScore.Should().Be(81);
        row.Events.Single().FaceMatchScore.Should().Be(81);

        // Talabaning period-days paneli ham.
        var periodDays = await scene.Client.GetStringAsync("/api/student/period-days");
        periodDays.Should().Contain("\"faceMatchScore\":81");
    }

    [Fact]
    public async Task Yoqilgan_MosEmas_FaceMismatch_BallBilan_KeyinMosRasm200()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("sardor");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, FakeFaceEngine.Photo("sardor", 20));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ProblemAsync();
        problem.GetProperty("detail").GetString().Should().Be("Yuz etalonga mos kelmadi (20%). Qayta suratga oling.");
        problem.GetProperty("rejectReason").GetString().Should().Be("faceMismatch");
        problem.GetProperty("faceMatchScore").GetInt32().Should().Be(20);

        var rejected = (await EventsAsync(scene)).Single();
        rejected.RejectReason.Should().Be(CheckInRejectReason.FaceMismatch);
        rejected.FaceMatchScore.Should().Be(20);
        (await Factory.WithDbAsync(db => db.DailyAttendances.AnyAsync(a => a.StudentUserId == scene.Student.Id))).Should().BeFalse();

        // Chegaraga teng ball (36) — qabul.
        var retry = await CheckInAsync(scene, day, FakeFaceEngine.Photo("sardor", 36));
        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());

        // Tyutor rad etilgan urinishni ball bilan ko'radi.
        var tutor = await Factory.LoginAsync(scene.Tutor);
        var row = (await tutor.GetFromJsonSafeAsync<List<StudentAttendanceDay>>(
            $"/api/tutor/students/{scene.Student.Id}/attendance?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}")).Single();
        row.Events.Should().HaveCount(2);
        row.Events[0].RejectReason.Should().Be(CheckInRejectReason.FaceMismatch);
        row.Events[0].FaceMatchScore.Should().Be(20);
        row.FaceMatchScore.Should().Be(36);
    }

    [Fact]
    public async Task Yoqilgan_BoshqaOdam_VaChegaraSozlamasi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("nodira");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");
        await using var strict = await Factory.UseSettingAsync(SettingKeys.FaceMatchThreshold, "60");

        var other = await CheckInAsync(scene, day, FakeFaceEngine.Photo("boshqa-odam"));
        other.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await other.ProblemAsync()).GetProperty("detail").GetString().Should().Be("Yuz etalonga mos kelmadi (0%). Qayta suratga oling.");

        var below = await CheckInAsync(scene, day, FakeFaceEngine.Photo("nodira", 50));
        below.StatusCode.Should().Be(HttpStatusCode.BadRequest, "60% chegarada 50% — rad");

        var ok = await CheckInAsync(scene, day, FakeFaceEngine.Photo("nodira", 60));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Yoqilgan_SelfidaYuzYoq_FaceNotDetected()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("aziz");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, FakeFaceEngine.NoFace);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ProblemAsync();
        problem.GetProperty("detail").GetString().Should().Be("Rasmda yuz topilmadi. Qayta suratga oling.");
        problem.GetProperty("rejectReason").GetString().Should().Be("faceNotDetected");
        (await EventsAsync(scene)).Single().RejectReason.Should().Be(CheckInRejectReason.FaceNotDetected);
    }

    [Fact]
    public async Task Yoqilgan_RadEtilganEtalon_FaceNotEnrolled()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("kamola");
        await Factory.WithDbAsync(async db =>
        {
            var e = await db.StudentFaceEnrollments.SingleAsync(f => f.StudentUserId == scene.Student.Id);
            e.Reject(scene.Tutor.Id, "Boshqa odam", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");

        var response = await CheckInAsync(scene, day, FakeFaceEngine.Photo("kamola"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemAsync()).GetProperty("rejectReason").GetString().Should().Be("faceNotEnrolled");
    }

    [Fact]
    public async Task Yoqilgan_ModellarYoq_503_HodisaYozilmaydi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");
        Factory.FaceEngine.IsReady = false;
        try
        {
            var response = await CheckInAsync(scene, day, FakeFaceEngine.Photo("ali"));
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            Factory.FaceEngine.IsReady = true;
        }

        (await EventsAsync(scene)).Should().BeEmpty();
    }

    [Fact]
    public async Task MavjudRadSabablari_HamRejectReasonOladi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
        HttpResponseMessage response;
        try
        {
            using var form = Factory.GeoForm(lat: StudentTestData.FarLat, photo: ("s.jpg", "image/jpeg", FakeFaceEngine.NoFace), qr: scene.Qr());
            response = await scene.Client.PostAsync("/api/student/checkin", form);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ProblemAsync()).GetProperty("rejectReason").GetString().Should().Be("outOfRadius");
    }

    [Fact]
    public async Task CheckOut_YuzTekshirilmaydi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await scene.Client.EnrollAsync("bek");
        await using var faceOn = await Factory.UseSettingAsync(SettingKeys.FaceVerificationEnabled, "true");
        (await CheckInAsync(scene, day, FakeFaceEngine.Photo("bek"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var calls = Factory.FaceEngine.Calls;
        fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
        try
        {
            using var form = Factory.GeoForm(photo: ("s.jpg", "image/jpeg", FakeFaceEngine.NoFace), qr: scene.Qr());
            var response = await scene.Client.PostAsync("/api/student/checkout", form);
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }

        Factory.FaceEngine.Calls.Should().Be(calls);
    }
}
