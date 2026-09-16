using System.Net;
using System.Text;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Check-in selfisi: multipart <c>photo</c> maydoni, <c>checkinPhotoRequired</c> sozlamasi,
/// fayl validatsiyasi, rad etilgan urinishning rasmi va <c>/api/files/{id}</c> ko'lami.
/// Soat <see cref="MutableClock"/> bilan muzlatiladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInPhotoTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    /// <summary>Eng kichik haqiqiy PNG sarlavhasi — mazmuni muhim emas, bayt'lar qaytishi tekshiriladi.</summary>
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5];

    [Fact]
    public async Task RasmBilan_CheckIn_200_RasmSaqlanadiVaBoglanadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(photo: ("selfi.png", "image/png", Png));
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Present);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id));
        attempt.Accepted.Should().BeTrue();
        attempt.PhotoFileId.Should().NotBeNull();

        var attendance = await Factory.WithDbAsync(db =>
            db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id));
        attendance.CheckInPhotoFileId.Should().Be(attempt.PhotoFileId, "qabul qilingan rasm kunlik qatorga ham yoziladi");
        attendance.CheckOutPhotoFileId.Should().BeNull();

        var stored = await Factory.WithDbAsync(db => db.StoredFiles.SingleAsync(f => f.Id == attempt.PhotoFileId));
        stored.Kind.Should().Be(StoredFileKind.CheckInPhoto);
        stored.FileName.Should().Be("selfi.png");
        stored.ContentType.Should().Be("image/png");
        stored.SizeBytes.Should().Be(Png.Length);
        stored.UploadedByUserId.Should().Be(scene.Student.Id);

        // Talaba o'z selfisini ko'ra oladi.
        var file = await scene.Client.GetAsync($"/api/files/{attempt.PhotoFileId}");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
    }

    [Fact]
    public async Task RasmsizMultipart_CheckIn_200_RasmIxtiyoriy()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm();
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id));
        attempt.PhotoFileId.Should().BeNull();
        (await Factory.WithDbAsync(db => db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id)))
            .CheckInPhotoFileId.Should().BeNull();
    }

    [Fact]
    public async Task SozlamaYoqilgan_Rasmsiz_400_ErrorsPhoto()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await using var setting = await Factory.UseSettingAsync(SettingKeys.CheckInPhotoRequired, "true");

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm();
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("detail").GetString().Should().Be("Check-in uchun rasm majburiy.");
            json.RootElement.GetProperty("errors").TryGetProperty("Photo", out var errors).Should().BeTrue();
            errors[0].GetString().Should().Be("Check-in uchun rasm majburiy.");

            // Rasm bilan — o'sha sozlamada qabul qilinadi.
            using var withPhoto = Factory.GeoForm(photo: ("selfi.png", "image/png", Png));
            (await scene.Client.PostAsync("/api/student/checkin", withPhoto)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task NotogriFaylTuri_400_ErrorsPhoto()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(photo: ("hisobot.pdf", "application/pdf", Png));
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("errors").TryGetProperty("Photo", out var errors).Should().BeTrue();
            errors[0].GetString().Should().Contain("Faqat rasm");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        // Validatsiya handler'gacha yetmaydi — urinish ham, fayl ham yozilmaydi.
        (await Factory.WithDbAsync(db => db.AttendanceEvents.AnyAsync(e => e.StudentUserId == scene.Student.Id)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task KattaFayl_400_ErrorsPhoto()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var tooBig = new byte[(5 * 1024 * 1024) + 1024];

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(photo: ("katta.jpg", "image/jpeg", tooBig));
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("errors").TryGetProperty("Photo", out var errors).Should().BeTrue();
            errors[0].GetString().Should().Contain("5 MB");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task RadEtilganUrinish_409_RasmBaribirSaqlanadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(lat: StudentTestData.FarLat, photo: ("selfi.jpg", "image/jpeg", Png));
            var response = await scene.Client.PostAsync("/api/student/checkin", form);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id));
        attempt.Accepted.Should().BeFalse();
        attempt.RejectReason.Should().Be(CheckInRejectReason.OutOfRadius);
        attempt.PhotoFileId.Should().NotBeNull("tyutor shubhani rasm bo'yicha tekshiradi");

        var stored = await Factory.WithDbAsync(db => db.StoredFiles.SingleAsync(f => f.Id == attempt.PhotoFileId));
        stored.Kind.Should().Be(StoredFileKind.CheckInPhoto);

        (await Factory.WithDbAsync(db => db.DailyAttendances.AnyAsync(a => a.StudentUserId == scene.Student.Id)))
            .Should().BeFalse("rad etilgan urinish davomatga yozilmaydi");

        // Fayl bayt'lari haqiqatan saqlangan.
        var file = await scene.Client.GetAsync($"/api/files/{attempt.PhotoFileId}");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
    }

    [Fact]
    public async Task CheckOut_RasmBilan_CheckOutPhotoFileIdToldiriladi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.CheckInDirectlyAsync(scene, day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
            using var form = Factory.GeoForm(photo: ("ketdim.webp", "image/webp", Png));
            var response = await scene.Client.PostAsync("/api/student/checkout", form);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id && e.Kind == AttendanceEventKind.CheckOut));
        attempt.PhotoFileId.Should().NotBeNull();

        var attendance = await Factory.WithDbAsync(db =>
            db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id));
        attendance.CheckOutPhotoFileId.Should().Be(attempt.PhotoFileId);
        attendance.CheckInPhotoFileId.Should().BeNull("to'g'ridan-to'g'ri yozilgan check-in rasmsiz edi");
    }

    [Fact]
    public async Task FaylKolami_Tyutor_OzGuruhi_200_BoshqaGuruh_404()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            using var form = Factory.GeoForm(photo: ("selfi.png", "image/png", Png));
            (await scene.Client.PostAsync("/api/student/checkin", form)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var photoId = (await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id))).PhotoFileId!.Value;

        var ownTutor = await Factory.LoginAsync(scene.Tutor);
        (await ownTutor.GetAsync($"/api/files/{photoId}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "tyutor o'z ko'lamidagi talabaning selfisini ko'radi");

        var otherGroup = await Factory.CreateGroupAsync();
        var otherTutorClient = await Factory.LoginAsTutorAsync(otherGroup);
        (await otherTutorClient.GetAsync($"/api/files/{photoId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "ko'lamdan tashqari — mavjudligi oshkor qilinmaydi");

        var otherStudent = await Factory.CreateStudentAsync(group: otherGroup);
        var otherStudentClient = await Factory.LoginAsStudentAsync(otherStudent);
        (await otherStudentClient.GetAsync($"/api/files/{photoId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetAsync($"/api/files/{photoId}")).StatusCode.Should().Be(HttpStatusCode.OK, "admin — hammasi");
    }

    [Fact]
    public async Task JsonSoRov_Rasmsiz_HamonIshlaydi()
    {
        // Mavjud klientlar (va offline navbat) uchun JSON yo'li saqlangan.
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo());

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    /// <summary>Bitta yo'lda ikkita action bor: multipart'da <c>[Consumes]</c> cheklovi, JSON action'da esa
    /// ATAYLAB yo'q. Ikkalasida ham cheklov bo'lsa <c>Content-Type</c> siz so'rov ikkala kandidatga mos kelib
    /// <c>AmbiguousMatchException</c> (500) beradi — shuning uchun JSON action cheklovsiz "fallback".</summary>
    [Fact]
    public async Task ContentTypeMarshrutlash_JsonVaMultipart_200()
    {
        var day = await Factory.NextWorkDayAsync();
        var jsonScene = await Factory.CreateSceneAsync(day);
        var formScene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));

            var json = await jsonScene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo());
            json.StatusCode.Should().Be(HttpStatusCode.OK, "application/json → JSON action");

            using var form = Factory.GeoForm(photo: ("selfi.png", "image/png", Png));
            var multipart = await formScene.Client.PostAsync("/api/student/checkin", form);
            multipart.StatusCode.Should().Be(HttpStatusCode.OK, "multipart/form-data → multipart action");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Theory]
    [InlineData("/api/student/checkin")]
    [InlineData("/api/student/checkout")]
    public async Task NotogriContentType_415(string path)
    {
        var scene = await Factory.CreateSceneAsync();

        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");
        var response = await scene.Client.PostAsync(path, content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/student/checkin")]
    [InlineData("/api/student/checkout")]
    public async Task ContentTypeYoq_415_IchkiXatolikEmas(string path)
    {
        var scene = await Factory.CreateSceneAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("{}"))
        };
        request.Content.Headers.ContentType.Should().BeNull("Content-Type ataylab qo'yilmaydi");

        var response = await scene.Client.SendAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.UnsupportedMediaType,
            "marshrutlash noaniq bo'lmasligi kerak (AmbiguousMatchException → 500)");
        ((int)response.StatusCode).Should().NotBe(StatusCodes500, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Bitta yo'lda ikkita action — OpenAPI hujjati baribir generatsiya bo'lishi kerak
    /// (<c>MapOpenApi</c> faqat Development'da ulanadi).</summary>
    [Fact]
    public async Task OpenApiHujjati_IkkiActionBilanHamGeneratsiyaBoladi()
    {
        using var development = Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        var client = development.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body[..Math.Min(2000, body.Length)]);
        body.Should().Contain("/api/student/checkin").And.Contain("/api/student/checkout");
        body.Should().Contain("multipart/form-data");
    }

    private const int StatusCodes500 = 500;
}
