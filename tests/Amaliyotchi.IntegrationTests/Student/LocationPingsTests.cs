using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Tracking;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary>Live joylashuv — talaba qismi (docs/LIVE-JOYLASHUV.md): <c>today.tracking</c>, rozilik, ping qabul/rad
/// qoidalari (sessiya, 24 soat / 2 daqiqa oynasi, idempotentlik, ≤100, rozilik / sozlama o'chiq → accepted 0), audit yo'qligi,
/// rate limit. Soat <see cref="MutableClock"/> bilan muzlatiladi: check-in 09:00, "hozir" 10:00 (Toshkent).</summary>
[Collection(ApiCollection.Name)]
public sealed class LocationPingsTests(ApiFixture fixture)
{
    private const string PingsUrl = "/api/student/location-pings";
    private const string ConsentUrl = "/api/student/tracking/consent";

    private ApiFactory Factory => fixture.Factory;

    private static object Ping(
        DateTimeOffset at, double lat = StudentTestData.CompanyLat, double lng = StudentTestData.CompanyLng,
        double accuracyM = 10, bool isMocked = false, string source = "background")
        => new { lat, lng, accuracyM, occurredAt = at, isMocked, source };

    private static object Batch(params object[] pings) => new { pings };

    /// <summary>Sahna: davr + tasdiqlangan ariza, bugun 09:00 da check-in (DB'ga to'g'ridan-to'g'ri), soat 10:00 ga muzlatiladi.</summary>
    private async Task<(StudentScene Scene, DateOnly Day)> CheckedInSceneAsync(bool consent = true)
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.CheckInDirectlyAsync(scene, day);
        if (consent)
            (await scene.Client.PutAsJsonAsync(ConsentUrl, new { consent = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        fixture.Clock.Set(At(day, 10, 0));
        return (scene, day);
    }

    private static DateTimeOffset At(DateOnly day, int hour, int minute) => PracticeTime.At(day, new TimeOnly(hour, minute));

    private Task<List<LocationPing>> StoredAsync(Guid studentId)
        => Factory.WithDbAsync(db => db.LocationPings.AsNoTracking()
            .Where(p => p.StudentUserId == studentId).OrderBy(p => p.OccurredAt).ToListAsync());

    [Fact]
    public async Task Today_Tracking_SessiyagaQarab()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        try
        {
            fixture.Clock.Set(At(day, 8, 30));
            var before = (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!;
            before.Tracking.Should().Be(new TrackingDto(false, false, 120, 20, null), "check-in yo'q — sessiya faol emas");

            await Factory.CheckInDirectlyAsync(scene, day);
            fixture.Clock.Set(At(day, 10, 0));
            var response = await scene.Client.GetAsync("/api/student/today");
            var raw = await response.Content.ReadAsStringAsync();
            using (var json = JsonDocument.Parse(raw))
            {
                var tracking = json.RootElement.GetProperty("tracking");
                // UTC (offset 0) — API'dagi boshqa vaqtlar kabi System.Text.Json "+00:00" ko'rinishida.
                tracking.GetProperty("activeUntil").GetString().Should().Be(
                    At(day, 18, 0).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'+00:00'"), "UTC ISO, auto-close 18:00 Toshkent");
            }

            var dto = JsonSerializer.Deserialize<TodayDto>(raw, JsonDefaults.Options)!;
            dto.Tracking.Enabled.Should().BeTrue();
            dto.Tracking.ConsentGiven.Should().BeFalse();
            dto.Tracking.ActiveUntil.Should().Be(At(day, 18, 0));

            // Avtomatik yopilish vaqtidan keyin — sessiya tugagan.
            fixture.Clock.Set(At(day, 18, 1));
            var after = (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!;
            after.Tracking.Enabled.Should().BeFalse();
            after.Tracking.ActiveUntil.Should().BeNull();
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task CheckIn_JavobidagiTodayDto_TrackingEnabled()
    {
        await using var photo = await Factory.WithoutPhotoRequirementAsync();
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        try
        {
            fixture.Clock.Set(At(day, 9, 5));
            var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var today = (await response.Content.ReadAsync<TodayDto>())!;
            today.Tracking.Enabled.Should().BeTrue("klient check-in'dan keyin darhol fonda kuzatuvni boshlaydi");
            today.Tracking.ActiveUntil.Should().Be(At(day, 18, 0));
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Rozilik_Berish_QaytaribOlish_Validatsiya()
    {
        var scene = await Factory.CreateSceneAsync();

        var missing = await scene.Client.PutAsJsonAsync(ConsentUrl, new { });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await missing.Content.ReadAsStringAsync()))
            json.RootElement.GetProperty("errors").TryGetProperty("Consent", out _).Should().BeTrue();

        (await scene.Client.PutAsJsonAsync(ConsentUrl, new { consent = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var given = await Factory.WithDbAsync(db => db.StudentProfiles.AsNoTracking()
            .Where(p => p.UserId == scene.Student.Id).Select(p => p.TrackingConsentAt).SingleAsync());
        given.Should().NotBeNull();
        (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!
            .Tracking.ConsentGiven.Should().BeTrue();

        (await scene.Client.PutAsJsonAsync(ConsentUrl, new { consent = false })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!
            .Tracking.ConsentGiven.Should().BeFalse();

        // Admin bu endpoint'ga kira olmaydi (StudentOnly).
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PutAsJsonAsync(ConsentUrl, new { consent = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pinglar_Qabul_HolatServerdaHisoblanadi()
    {
        var (scene, day) = await CheckedInSceneAsync();
        try
        {
            var response = await scene.Client.PostJsonAsync(PingsUrl, Batch(
                Ping(At(day, 9, 30)),
                Ping(At(day, 9, 35), lat: StudentTestData.FarLat, isMocked: true, source: "foreground"),
                Ping(At(day, 9, 40), accuracyM: 250)));

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var body = (await response.Content.ReadAsync<LocationPingsResultDto>())!;
            body.Accepted.Should().Be(3);
            body.Tracking.Enabled.Should().BeTrue();
            body.Tracking.ConsentGiven.Should().BeTrue();
            body.Last.Should().NotBeNull();
            body.Last!.Status.Should().Be(LocationPingStatus.Inaccurate);
            body.Last.OccurredAt.Should().Be(At(day, 9, 40));

            var stored = await StoredAsync(scene.Student.Id);
            stored.Select(p => p.Status).Should().Equal(
                LocationPingStatus.Inside, LocationPingStatus.Outside, LocationPingStatus.Inaccurate);
            stored[1].IsMocked.Should().BeTrue("soxta joylashuv rad etilmaydi — belgi saqlanadi");
            stored[1].Source.Should().Be(LocationPingSource.Foreground);
            stored[1].DistanceM.Should().BeApproximately(1000, 30);
            stored[0].Source.Should().Be(LocationPingSource.Background);
            stored.Should().OnlyContain(p => p.Date == day && p.RadiusM == 150);

            // Har ping audit jurnaliga yozilmaydi (IAuditExempt).
            var audits = await Factory.WithDbAsync(db => db.AuditLogs.AsNoTracking()
                .CountAsync(a => a.EntityName == nameof(LocationPing)));
            audits.Should().Be(0);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Pinglar_VaqtOynasi_Sessiya_Yaroqsiz_Tashlanadi()
    {
        var (scene, day) = await CheckedInSceneAsync();
        try
        {
            var response = await scene.Client.PostJsonAsync(PingsUrl, Batch(
                Ping(At(day, 8, 30)),                         // check-in (09:00) dan oldin — sessiyadan tashqari
                Ping(At(day, 10, 0).AddMinutes(3)),           // 2 daqiqadan ko'proq kelajakda
                Ping(At(day, 10, 0).AddHours(-25)),           // 24 soatdan eski
                Ping(At(day, 9, 50), lat: 95),                // yaroqsiz koordinata
                Ping(At(day, 9, 55), accuracyM: -1),          // yaroqsiz aniqlik
                Ping(At(day, 10, 0).AddMinutes(1))));         // 1 daqiqa kelajak — ruxsat

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadAsync<LocationPingsResultDto>())!.Accepted.Should().Be(1);
            (await StoredAsync(scene.Student.Id)).Should().ContainSingle(p => p.OccurredAt == At(day, 10, 1));
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Pinglar_Idempotent_TakrorTashlanadi()
    {
        var (scene, day) = await CheckedInSceneAsync();
        try
        {
            var batch = Batch(Ping(At(day, 9, 10)), Ping(At(day, 9, 15)), Ping(At(day, 9, 15)));

            var first = (await (await scene.Client.PostJsonAsync(PingsUrl, batch)).Content.ReadAsync<LocationPingsResultDto>())!;
            var second = (await (await scene.Client.PostJsonAsync(PingsUrl, batch)).Content.ReadAsync<LocationPingsResultDto>())!;
            // Sub-millisekund farq — o'sha ping (Postgres mikrosekund; kalit millisekundgacha).
            var third = (await (await scene.Client.PostJsonAsync(PingsUrl, Batch(Ping(At(day, 9, 10).AddTicks(3)))))
                .Content.ReadAsync<LocationPingsResultDto>())!;

            first.Accepted.Should().Be(2, "paket ichidagi takror ham bir marta");
            second.Accepted.Should().Be(0);
            third.Accepted.Should().Be(0);
            second.Last!.OccurredAt.Should().Be(At(day, 9, 15));
            (await StoredAsync(scene.Student.Id)).Should().HaveCount(2);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task CheckOutdanKeyin_NavbatdagiSessiyaPinglariQabul_KeyingilariYoq()
    {
        var (scene, day) = await CheckedInSceneAsync();
        try
        {
            await Factory.WithDbAsync(async db =>
            {
                var row = await db.DailyAttendances.SingleAsync(a => a.StudentUserId == scene.Student.Id && a.Date == day);
                row.CheckOut(At(day, 9, 45), 20);
                await db.SaveChangesAsync();
            });

            var response = await scene.Client.PostJsonAsync(PingsUrl, Batch(
                Ping(At(day, 9, 40)),     // sessiya ichida (offline navbatdan) — qabul
                Ping(At(day, 9, 50))));   // check-out'dan keyin — tashlanadi

            var body = (await response.Content.ReadAsync<LocationPingsResultDto>())!;
            body.Accepted.Should().Be(1);
            body.Tracking.Enabled.Should().BeFalse("check-out — kuzatuv to'xtaydi");
            body.Tracking.ActiveUntil.Should().BeNull();
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task RoziliksizYokiSozlamaOchiq_Accepted0()
    {
        var (scene, day) = await CheckedInSceneAsync(consent: false);
        try
        {
            var noConsent = (await (await scene.Client.PostJsonAsync(PingsUrl, Batch(Ping(At(day, 9, 30)))))
                .Content.ReadAsync<LocationPingsResultDto>())!;
            noConsent.Accepted.Should().Be(0);
            noConsent.Tracking.ConsentGiven.Should().BeFalse();
            noConsent.Tracking.Enabled.Should().BeTrue("sozlama yoqiq va sessiya faol — rozilik alohida maydonda");
            noConsent.Last.Should().BeNull();

            (await scene.Client.PutAsJsonAsync(ConsentUrl, new { consent = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            await using (await Factory.UseSettingAsync(SettingKeys.TrackingEnabled, "false"))
            {
                var disabled = (await (await scene.Client.PostJsonAsync(PingsUrl, Batch(Ping(At(day, 9, 30)))))
                    .Content.ReadAsync<LocationPingsResultDto>())!;
                disabled.Accepted.Should().Be(0);
                disabled.Tracking.Enabled.Should().BeFalse("klient to'xtaydi");
            }

            (await StoredAsync(scene.Student.Id)).Should().BeEmpty();
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Paket_BoshYoki100danKop_400()
    {
        var (scene, day) = await CheckedInSceneAsync();
        try
        {
            (await scene.Client.PostJsonAsync(PingsUrl, Batch())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await scene.Client.PostJsonAsync(PingsUrl, new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var tooMany = Enumerable.Range(0, 101).Select(i => Ping(At(day, 9, 1).AddSeconds(i))).ToArray();
            var response = await scene.Client.PostJsonAsync(PingsUrl, Batch(tooMany));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("errors").TryGetProperty("Pings", out _).Should().BeTrue();

            var hundred = (await (await scene.Client.PostJsonAsync(PingsUrl, Batch(tooMany[..100])))
                .Content.ReadAsync<LocationPingsResultDto>())!;
            hundred.Accepted.Should().Be(100);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task RateLimit_TalabaBoyicha30SorovDaqiqasiga()
    {
        var scene = await Factory.CreateSceneAsync();
        var other = await Factory.CreateSceneAsync();
        var body = Batch(Ping(Factory.Clock.UtcNow));

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
            statuses.Add((await scene.Client.PostJsonAsync(PingsUrl, body)).StatusCode);

        statuses.Take(30).Should().OnlyContain(s => s == HttpStatusCode.OK);
        statuses[30].Should().Be(HttpStatusCode.TooManyRequests);
        (await other.Client.PostJsonAsync(PingsUrl, body)).StatusCode.Should().Be(HttpStatusCode.OK, "chelak talaba bo'yicha");
    }
}
