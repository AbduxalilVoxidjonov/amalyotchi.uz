using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Check-in/check-out QR kodi: <c>checkinQrRequired</c> (sukut <c>true</c>), korxona tokeniga moslik,
/// rad etilgan urinish (<c>QrInvalid</c>, 409) selfisi bilan saqlanishi, almashtirilgan QR. Selfi sukut bo'yicha
/// majburiy — so'rovlar multipart, rasm bilan. Soat <see cref="MutableClock"/> bilan muzlatiladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInQrTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5];

    private static readonly (string, string, byte[]) Selfie = ("selfi.png", "image/png", Png);

    private static string RandomPayload() => CheckInQr.ToPayload(CheckInQr.NewToken());

    /// <summary>09:05 da (yoki berilgan vaqtda) multipart check-in/check-out; soat keyin tiklanadi.</summary>
    private async Task<HttpResponseMessage> PostAsync(
        StudentScene scene, DateOnly day, string? qr, string path = "/api/student/checkin", TimeOnly? at = null)
    {
        try
        {
            fixture.Clock.Set(PracticeTime.At(day, at ?? new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(photo: Selfie, qr: qr);
            return await scene.Client.PostAsync(path, form);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    private Task<List<AttendanceEvent>> EventsAsync(StudentScene scene) =>
        Factory.WithDbAsync(db => db.AttendanceEvents.Where(e => e.StudentUserId == scene.Student.Id).ToListAsync());

    [Theory]
    [InlineData("/api/student/checkin")]
    [InlineData("/api/student/checkout")]
    public async Task Sukut_QrMajburiy_QrYoq_400_ErrorsQr_HodisaYozilmaydi(string path)
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        if (path.EndsWith("checkout", StringComparison.Ordinal))
            await Factory.CheckInDirectlyAsync(scene, day);

        var response = await PostAsync(scene, day, qr: null, path, at: path.EndsWith("checkout", StringComparison.Ordinal) ? new TimeOnly(17, 5) : null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("detail").GetString().Should().Be("Amaliyot joyidagi QR kodni skanerlang.");
        json.RootElement.GetProperty("errors").TryGetProperty("Qr", out var errors).Should().BeTrue();
        errors[0].GetString().Should().Be("Amaliyot joyidagi QR kodni skanerlang.");
        json.RootElement.GetProperty("errors").TryGetProperty("Photo", out _).Should().BeFalse("selfi yuborilgan");

        (await EventsAsync(scene)).Should().BeEmpty("majburiy qism yo'q — urinish hodisasi yozilmaydi");
    }

    [Fact]
    public async Task BoshQr_QrYoqDebHisoblanadi_400()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        var response = await PostAsync(scene, day, qr: "   ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("QR kodni skanerlang");
    }

    [Fact]
    public async Task ToGriQr_200_Qabul()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        var response = await PostAsync(scene, day, scene.Qr());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Present);
        (await EventsAsync(scene)).Should().ContainSingle(e => e.Accepted);
    }

    [Fact]
    public async Task NotogriQr_409_RadEtilganHodisa_QrInvalid_RasmSaqlanadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        var response = await PostAsync(scene, day, RandomPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("detail").GetString().Should().Be("QR kod bu amaliyot joyiga tegishli emas.");

        var attempt = (await EventsAsync(scene)).Should().ContainSingle().Subject;
        attempt.Accepted.Should().BeFalse();
        attempt.RejectReason.Should().Be(CheckInRejectReason.QrInvalid);
        attempt.Kind.Should().Be(AttendanceEventKind.CheckIn);
        attempt.PhotoFileId.Should().NotBeNull("tyutor kim urinib ko'rganini selfidan ko'radi");

        var stored = await Factory.WithDbAsync(db => db.StoredFiles.SingleAsync(f => f.Id == attempt.PhotoFileId));
        stored.Kind.Should().Be(StoredFileKind.CheckInPhoto);

        (await Factory.WithDbAsync(db => db.DailyAttendances.AnyAsync(a => a.StudentUserId == scene.Student.Id)))
            .Should().BeFalse("rad etilgan urinish davomatga yozilmaydi");
    }

    [Theory]
    [InlineData("salom")]
    [InlineData("AMLQR:1:")]
    [InlineData("AMLQR:2:0123456789abcdef0123456789abcdef")]
    public async Task NotogriFormat_409_QrInvalid(string qr)
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        var response = await PostAsync(scene, day, qr);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventsAsync(scene)).Should().ContainSingle(e => !e.Accepted && e.RejectReason == CheckInRejectReason.QrInvalid);
    }

    [Fact]
    public async Task BoshqaKorxonaQri_409_RadEtiladi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var other = await Factory.CreateCompanyAsync(StudentTestData.CompanyLat, StudentTestData.CompanyLng);

        var response = await PostAsync(scene, day, other.CheckInQrPayload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventsAsync(scene)).Should().ContainSingle(e => !e.Accepted && e.RejectReason == CheckInRejectReason.QrInvalid);
    }

    [Fact]
    public async Task NotogriQr_RadiusTashqarisidaHam_SababQrInvalid()
    {
        // QR tekshiruvi radiusdan oldin: boshqa joyda turib boshqa QR'ni skanerlagan — sabab QR.
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(lat: StudentTestData.FarLat, photo: Selfie, qr: RandomPayload());
            (await scene.Client.PostAsync("/api/student/checkin", form)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        (await EventsAsync(scene)).Single().RejectReason.Should().Be(CheckInRejectReason.QrInvalid);
    }

    [Fact]
    public async Task SozlamaOchiq_QrYoq_200_Qabul()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var setting = await Factory.UseSettingAsync(SettingKeys.CheckInQrRequired, "false");

        var response = await PostAsync(scene, day, qr: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await EventsAsync(scene)).Should().ContainSingle(e => e.Accepted);
    }

    [Fact]
    public async Task SozlamaOchiq_NotogriQrYuborilsa_BaribirRadEtiladi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var setting = await Factory.UseSettingAsync(SettingKeys.CheckInQrRequired, "false");

        var response = await PostAsync(scene, day, RandomPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await EventsAsync(scene)).Should().ContainSingle(e => e.RejectReason == CheckInRejectReason.QrInvalid);
    }

    [Fact]
    public async Task Rotate_EskiQr_409_YangiQr_200()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var oldPayload = scene.Qr();

        var admin = await Factory.LoginAsAdminAsync();
        var rotate = await admin.PostAsync($"/api/admin/companies/{scene.Company.Id}/checkin-qr/rotate", null);
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        var newPayload = (await rotate.Content.ReadAsync<Application.Features.Admin.Companies.CompanyCheckInQrDto>())!.Payload;
        newPayload.Should().NotBe(oldPayload);

        (await PostAsync(scene, day, oldPayload)).StatusCode.Should().Be(HttpStatusCode.Conflict, "osilgan eski QR yaroqsiz");
        var accepted = await PostAsync(scene, day, newPayload, at: new TimeOnly(9, 6));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());

        var events = await EventsAsync(scene);
        events.Should().HaveCount(2);
        events.Should().ContainSingle(e => !e.Accepted && e.RejectReason == CheckInRejectReason.QrInvalid);
        events.Should().ContainSingle(e => e.Accepted);
    }

    [Fact]
    public async Task CheckOut_NotogriQr_409_ToGriQr_200()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.CheckInDirectlyAsync(scene, day);

        var rejected = await PostAsync(scene, day, RandomPayload(), "/api/student/checkout", new TimeOnly(17, 5));
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var accepted = await PostAsync(scene, day, scene.Qr(), "/api/student/checkout", new TimeOnly(17, 6));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());

        var events = await EventsAsync(scene);
        events.Should().ContainSingle(e => e.Kind == AttendanceEventKind.CheckOut && !e.Accepted
                                           && e.RejectReason == CheckInRejectReason.QrInvalid && e.PhotoFileId != null);
        events.Should().ContainSingle(e => e.Kind == AttendanceEventKind.CheckOut && e.Accepted);
    }

    [Fact]
    public async Task JsonYoli_QrBilan_Ishlaydi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var setting = await Factory.WithoutPhotoRequirementAsync();

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var wrong = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: RandomPayload()));
            wrong.StatusCode.Should().Be(HttpStatusCode.Conflict);

            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 6)));
            var ok = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));
            ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Today_PhotoRequiredVaQrRequired_SozlamaniAksEttiradi()
    {
        var scene = await Factory.CreateSceneAsync();

        using (var json = JsonDocument.Parse(await scene.Client.GetStringAsync("/api/student/today")))
        {
            var checkin = json.RootElement.GetProperty("checkin");
            checkin.GetProperty("photoRequired").GetBoolean().Should().BeTrue("sukut — true");
            checkin.GetProperty("qrRequired").GetBoolean().Should().BeTrue("sukut — true");
        }

        await using (await Factory.UseSettingAsync(SettingKeys.CheckInQrRequired, "false"))
        await using (await Factory.WithoutPhotoRequirementAsync())
        {
            var dto = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            dto!.Checkin.QrRequired.Should().BeFalse();
            dto.Checkin.PhotoRequired.Should().BeFalse();
        }
    }
}
