using System.Net;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>POST /api/student/checkin</c>: geofence oqimi. Davr standart qoidalar (09:00–17:00, kechikish 09:15,
/// oyna 10:30 gacha) bilan, soat <see cref="MutableClock"/> orqali kerakli momentga muzlatiladi — haqiqiy vaqtga bog'liq emas.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckInTests(ApiFixture fixture) : IAsyncLifetime
{
    private ApiFactory Factory => fixture.Factory;

    private IAsyncDisposable? _photoSetting;

    /// <summary>Bu klass geofence/oyna mantiqini rasmsiz JSON yo'li orqali tekshiradi — selfi talabi (sukut <c>true</c>)
    /// har test uchun o'chiriladi va keyin qaytariladi. QR talabi yoqiq: har so'rov sahna korxonasining QR'ini yuboradi.</summary>
    public async Task InitializeAsync() => _photoSetting = await Factory.WithoutPhotoRequirementAsync();

    public async Task DisposeAsync()
    {
        if (_photoSetting is not null)
            await _photoSetting.DisposeAsync();
    }

    [Fact]
    public async Task OynaIchida_RadiusIchida_200_Present()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var today = await response.Content.ReadAsync<TodayDto>();
            today!.Date.Should().Be(day);
            today.Checkin.Status.Should().Be(AttendanceStatus.Present);
            today.Checkin.CheckInAt.Should().Be(PracticeTime.At(day, new TimeOnly(9, 5)));
            today.Checkin.CheckOutAt.Should().BeNull();
            today.Checkin.DistanceM.Should().BeLessThan(5);
            today.Checkin.GpsAccuracyM.Should().Be(10);
            today.Checkin.RadiusM.Should().Be(scene.Company.RadiusM);
            today.Checkin.Suspicious.Should().BeFalse();
            today.Place.Should().NotBeNull();
            today.Place!.Company.Should().Be(scene.Company.Name);
            today.Place.DaysPresent.Should().BeGreaterThanOrEqualTo(1);
            today.Window.IsOpen.Should().BeFalse("check-in bo'ldi, check-out oynasi hali ochilmagan");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var events = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.Where(e => e.StudentUserId == scene.Student.Id).ToListAsync());
        events.Should().ContainSingle(e => e.Accepted && e.Kind == AttendanceEventKind.CheckIn);
    }

    [Fact]
    public async Task KechikishChegarasidanKeyin_Late()
    {
        // 09:20 — kechikish chegarasi (09:15) o'tgan, oyna (10:30) hali ochiq → "kech keldi".
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 20)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Late);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task RadiusTashqarisi_409_UrinishSaqlanadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(lat: StudentTestData.FarLat, qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            (await response.Content.ReadAsStringAsync()).Should().Contain("amaliyot joyida emassiz");

            // Holat hali pending, oxirgi urinish masofasi ko'rinadi.
            var today = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            today!.Checkin.Status.Should().Be(AttendanceStatus.Pending);
            today.Checkin.DistanceM.Should().BeGreaterThan(900);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id));
        attempt.Accepted.Should().BeFalse();
        attempt.RejectReason.Should().Be(CheckInRejectReason.OutOfRadius);
        attempt.DistanceM.Should().BeGreaterThan(900);
        attempt.RadiusM.Should().Be(scene.Company.RadiusM);

        var attendance = await Factory.WithDbAsync(db =>
            db.DailyAttendances.AnyAsync(a => a.StudentUserId == scene.Student.Id));
        attendance.Should().BeFalse("rad etilgan urinish davomatga yozilmaydi");
    }

    [Fact]
    public async Task IkkiMarta_409_Allaqachon()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            (await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()))).StatusCode.Should().Be(HttpStatusCode.OK);

            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 6)));
            var second = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            second.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await second.Content.ReadAsStringAsync()).Should().Contain("allaqachon");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var events = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.Where(e => e.StudentUserId == scene.Student.Id).ToListAsync());
        events.Should().HaveCount(2);
        events.Should().ContainSingle(e => !e.Accepted && e.RejectReason == CheckInRejectReason.AlreadyCheckedIn);
    }

    [Fact]
    public async Task BirXilOccurredAt_Idempotent_200()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var occurredAt = fixture.Clock.UtcNow.AddSeconds(-5);

            var first = await scene.Client.PostJsonAsync("/api/student/checkin", StudentTestData.Geo(occurredAt, qr: scene.Qr()));
            var repeat = await scene.Client.PostJsonAsync("/api/student/checkin", StudentTestData.Geo(occurredAt, qr: scene.Qr()));

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            repeat.StatusCode.Should().Be(HttpStatusCode.OK, "takror so'rov (offline navbat) — mavjud natija");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var events = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.CountAsync(e => e.StudentUserId == scene.Student.Id));
        events.Should().Be(1, "takror urinish yangi hodisa yaratmaydi");
    }

    [Fact]
    public async Task AniqlikYomon_400()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(accuracy: 500, qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("GPS aniqligi");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attempt = await Factory.WithDbAsync(db => db.AttendanceEvents.SingleAsync(e => e.StudentUserId == scene.Student.Id));
        attempt.RejectReason.Should().Be(CheckInRejectReason.PoorAccuracy);
    }

    [Fact]
    public async Task DamOlishKuni_400_IshKuniEmas()
    {
        // Ish kunlari — faqat muzlatilgan kundan boshqa bitta kun.
        var day = await Factory.NextWorkDayAsync();
        var otherDay = WorkDaysExtensions.Of(day.AddDays(1).DayOfWeek);
        var scene = await Factory.CreateSceneAsync(day, (g, by) =>
            Factory.CreatePeriodAtAsync(g, by, day, workDays: otherDay));

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "CheckInRejectReason.NotWorkDay → DomainException (400)");
            (await response.Content.ReadAsStringAsync()).Should().Contain("ish kuni emas");

            var todayDto = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            todayDto!.Checkin.Status.Should().Be(AttendanceStatus.DayOff);
            todayDto.Window.IsOpen.Should().BeFalse();
            todayDto.Checkin.Note.Should().Contain("ish kuni emas");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task OynaYopiq_400()
    {
        // 11:00 — check-in oynasi (10:30) yopilgan.
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(11, 0)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("yopilgan");
            var todayDto = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            todayDto!.Checkin.Status.Should().Be(AttendanceStatus.Absent, "oyna yopilgan, yozuv yo'q → kelmadi");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task ArizaTasdiqlanmagan_400_PlaceNull()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day, approve: false);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("tasdiqlanmagan");

            var todayDto = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            todayDto!.Place.Should().BeNull();
            todayDto.Checkin.Status.Should().Be(AttendanceStatus.Pending);
            todayDto.Checkin.Note.Should().Contain("tasdiqlanmagan");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Theory]
    [InlineData(+300)]
    [InlineData(-660)]
    public async Task OccurredAt_KelajakdaYokiEski_400_Validatsiya(int offsetSeconds)
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.PostJsonAsync(
                "/api/student/checkin", StudentTestData.Geo(fixture.Clock.UtcNow.AddSeconds(offsetSeconds), qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("OccurredAt");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();

        (await tutor.PostJsonAsync("/api/student/checkin", Factory.Geo())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync("/api/student/today")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tokensiz_401()
    {
        (await Factory.CreateClient().GetAsync("/api/student/today")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
