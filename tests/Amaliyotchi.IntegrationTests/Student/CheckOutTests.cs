using System.Net;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>POST /api/student/checkout</c>: check-out oynasi (ish tugashi 17:00 dan avto-yopish 18:00 gacha), check-in shartligi.
/// Soat <see cref="MutableClock"/> bilan muzlatiladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class CheckOutTests(ApiFixture fixture) : IAsyncLifetime
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
    public async Task IshTugashidanOldin_400_OynaOchilmagan()
    {
        // 09:05 check-in, 09:10 check-out → ish tugashi (17:00) hali kelmagan.
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            (await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()))).StatusCode.Should().Be(HttpStatusCode.OK);

            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 10)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "CheckInRejectReason.WindowNotOpen → DomainException (400)");
            (await response.Content.ReadAsStringAsync()).Should().Contain("ochilmagan");
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var events = await Factory.WithDbAsync(db =>
            db.AttendanceEvents.Where(e => e.StudentUserId == scene.Student.Id && e.Kind == AttendanceEventKind.CheckOut).ToListAsync());
        events.Should().ContainSingle(e => !e.Accepted && e.RejectReason == CheckInRejectReason.WindowNotOpen);
    }

    [Fact]
    public async Task IshTugagach_200_CheckOutAtToldiriladi()
    {
        // 09:00 da kelgan, 17:05 — check-out oynasi (17:00–18:00) ochiq.
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.CheckInDirectlyAsync(scene, day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
            var before = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();
            before!.Window.IsOpen.Should().BeTrue("check-in bor, check-out oynasi ochiq");

            var response = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var today = await response.Content.ReadAsync<TodayDto>();
            today!.Checkin.Status.Should().Be(AttendanceStatus.Present);
            today.Checkin.CheckInAt.Should().Be(PracticeTime.At(day, new TimeOnly(9, 0)));
            today.Checkin.CheckOutAt.Should().Be(PracticeTime.At(day, new TimeOnly(17, 5)));
            today.Window.IsOpen.Should().BeFalse("ketish belgilangan — boshqa amal yo'q");

            // Ikkinchi marta → 409 (allaqachon ketgan).
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 6)));
            var again = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(qr: scene.Qr()));
            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task CheckInsiz_409()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Avval kelganingizni");
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task RadiusTashqarisi_409()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.CheckInDirectlyAsync(scene, day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(17, 5)));
            var response = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(lat: StudentTestData.FarLat, qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var attendance = await Factory.WithDbAsync(db => db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id));
        attendance.CheckOutAt.Should().BeNull();
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.PostJsonAsync("/api/student/checkout", Factory.Geo())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
