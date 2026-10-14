using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Settings;
using Amaliyotchi.Application.Features.Supervision.Today;
using Amaliyotchi.Application.Features.Supervision.Tracking;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.ValueObjects;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Supervision;

/// <summary>Live joylashuv — admin qismi (docs/LIVE-JOYLASHUV.md): <c>today.presence</c> (bitta so'rov bilan agregatsiya),
/// <c>/students/{id}/location-pings</c>, sozlamalar validatsiyasi, ko'lam/ruxsat va 60 kunlik tozalash.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminLocationTrackingTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    private static DateTimeOffset At(DateOnly day, int hour, int minute) => PracticeTime.At(day, new TimeOnly(hour, minute));

    private sealed record Scene(HttpClient Admin, TestGroup Group, Guid AdminId, Amaliyotchi.Domain.Practice.PracticePeriod Period,
        Amaliyotchi.Domain.Companies.Company Company, DateOnly Day, string Marker);

    private async Task<Scene> SceneAsync()
    {
        var day = await Factory.NextWorkDayAsync();
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);
        var company = await Factory.CreateCompanyAsync(StudentTestData.CompanyLat, StudentTestData.CompanyLng, 150);
        var period = await Factory.CreatePeriodAtAsync(group, admin.Id, day);
        return new Scene(client, group, admin.Id, period, company, day, "Joy" + Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>Talaba + ariza + (ixtiyoriy) 09:00 check-in, rozilik, check-out.</summary>
    private async Task<(TestUser Student, DailyAttendance? Row)> StudentAsync(
        Scene s, string name, bool checkIn = true, bool consent = true, TimeOnly? checkOut = null)
    {
        var student = await Factory.CreateStudentAsync(group: s.Group, fullName: $"{s.Marker} {name}");
        await Factory.CreateApprovedApplicationAsync(student, s.Period, s.Company, s.AdminId);
        DailyAttendance? row = null;
        if (checkIn)
        {
            row = await Factory.WithDbAsync(async db =>
            {
                var attendance = DailyAttendance.CheckIn(
                    student.Id, s.Period.Id, s.Day, At(s.Day, 9, 0), 20, 10, CheckInVerdict.Accept());
                if (checkOut is { } co)
                    attendance.CheckOut(PracticeTime.At(s.Day, co), 20);
                db.DailyAttendances.Add(attendance);
                await db.SaveChangesAsync();
                return attendance;
            });
        }

        if (consent)
        {
            await Factory.WithDbAsync(async db =>
            {
                var profile = await db.StudentProfiles.SingleAsync(p => p.UserId == student.Id);
                profile.GiveTrackingConsent(At(s.Day, 8, 0));
                await db.SaveChangesAsync();
            });
        }

        return (student, row);
    }

    private Task AddPingsAsync(TestUser student, DailyAttendance row, params (int Hour, int Minute, double Lat, double Accuracy, bool Mocked)[] pings)
        => Factory.WithDbAsync(async db =>
        {
            var company = new GeoPoint(StudentTestData.CompanyLat, StudentTestData.CompanyLng);
            foreach (var (hour, minute, lat, accuracy, mocked) in pings)
            {
                var location = new GeoPoint(lat, StudentTestData.CompanyLng);
                db.LocationPings.Add(LocationPing.Record(
                    student.Id, row.Id, row.Date, At(row.Date, hour, minute), location, accuracy,
                    company.DistanceMetersTo(location), 150, 100, mocked, LocationPingSource.Background, At(row.Date, hour, minute)));
            }

            await db.SaveChangesAsync();
        });

    private const double In = StudentTestData.CompanyLat;
    private const double Out = StudentTestData.FarLat;

    [Fact]
    public async Task Today_Presence_Holatlar()
    {
        var s = await SceneAsync();
        var (outside, outsideRow) = await StudentAsync(s, "Tashqarida");
        var (inside, insideRow) = await StudentAsync(s, "Ichkarida");
        var (lost, lostRow) = await StudentAsync(s, "Signalsiz");
        var (noConsent, _) = await StudentAsync(s, "Roziliksiz", consent: false);
        var (stopped, stoppedRow) = await StudentAsync(s, "Ketgan", checkOut: new TimeOnly(9, 50));
        var (absent, _) = await StudentAsync(s, "Kelmagan", checkIn: false);

        await AddPingsAsync(outside, outsideRow!,
            (9, 30, In, 10, false), (9, 35, Out, 10, true), (9, 40, Out, 10, false), (9, 58, Out, 10, false));
        await AddPingsAsync(inside, insideRow!, (9, 55, In, 10, false));
        await AddPingsAsync(lost, lostRow!, (9, 30, In, 10, false));
        await AddPingsAsync(stopped, stoppedRow!, (9, 40, Out, 10, false));

        try
        {
            fixture.Clock.Set(At(s.Day, 10, 0));
            var response = await s.Admin.GetAsync($"/api/admin/today?q={s.Marker}&pageSize=50");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var raw = await response.Content.ReadAsStringAsync();
            var body = JsonSerializer.Deserialize<TodayResponse>(raw, JsonDefaults.Options)!;
            var rows = body.Rows.Items.ToDictionary(r => r.StudentId);

            var o = rows[outside.Id].Presence!;
            o.State.Should().Be(Amaliyotchi.Domain.Attendance.PresenceState.Outside);
            o.LastPingAt.Should().Be(At(s.Day, 9, 58));
            o.DistanceM.Should().BeApproximately(1000, 30);
            o.OutsideMinutes.Should().Be(5 + 10, "9:35→9:40 = 5; 9:40→9:58 = 18, lekin 2×300 s = 10 da kesiladi");
            o.Mocked.Should().BeTrue();

            rows[inside.Id].Presence!.State.Should().Be(Amaliyotchi.Domain.Attendance.PresenceState.Inside);
            rows[lost.Id].Presence!.State.Should().Be(Amaliyotchi.Domain.Attendance.PresenceState.SignalLost, "oxirgi ping 30 daqiqa oldin");
            rows[noConsent.Id].Presence!.State.Should().Be(Amaliyotchi.Domain.Attendance.PresenceState.NoConsent);
            rows[stopped.Id].Presence!.State.Should().Be(Amaliyotchi.Domain.Attendance.PresenceState.Stopped);
            rows[stopped.Id].Presence!.LastPingAt.Should().Be(At(s.Day, 9, 40));
            rows[absent.Id].Presence.Should().BeNull("bugun check-in yo'q");

            using (var json = JsonDocument.Parse(raw))
            {
                var items = json.RootElement.GetProperty("rows").GetProperty("items").EnumerateArray().ToList();
                var lostJson = items.Single(i => i.GetProperty("studentId").GetGuid() == lost.Id);
                lostJson.GetProperty("presence").GetProperty("state").GetString().Should().Be("signalLost");
                var absentJson = items.Single(i => i.GetProperty("studentId").GetGuid() == absent.Id);
                absentJson.GetProperty("presence").ValueKind.Should().Be(JsonValueKind.Null);
            }

            // ?presence= filtri.
            var onlyOutside = (await (await s.Admin.GetAsync($"/api/admin/today?q={s.Marker}&presence=outside"))
                .Content.ReadAsync<TodayResponse>())!;
            onlyOutside.Rows.Items.Select(r => r.StudentId).Should().Equal(outside.Id);

            // Kuzatuv sozlamada o'chiq → presence null.
            await using (await Factory.UseSettingAsync(SettingKeys.TrackingEnabled, "false"))
            {
                var off = (await (await s.Admin.GetAsync($"/api/admin/today?q={s.Marker}&pageSize=50")).Content.ReadAsync<TodayResponse>())!;
                off.Rows.Items.Should().OnlyContain(r => r.Presence == null);
            }
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task StudentPings_XulosaVaJoy()
    {
        var s = await SceneAsync();
        var (student, row) = await StudentAsync(s, "Tarix");
        await AddPingsAsync(student, row!,
            (9, 5, In, 10, false), (9, 10, Out, 10, false), (9, 15, Out, 10, true), (9, 37, In, 150, false));

        var response = await s.Admin.GetAsync(
            $"/api/admin/students/{student.Id}/location-pings?date={s.Day:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = (await response.Content.ReadAsync<StudentLocationPingsDto>())!;
        body.Date.Should().Be(s.Day);
        body.Place.Should().Be(new TrackingPlaceDto(s.Company.Name, StudentTestData.CompanyLat, StudentTestData.CompanyLng, 150));
        body.CheckInAt.Should().Be(At(s.Day, 9, 0));
        body.CheckOutAt.Should().BeNull();
        body.ConsentGiven.Should().BeTrue();
        body.Pings.Select(p => p.Status).Should().Equal(
            LocationPingStatus.Inside, LocationPingStatus.Outside, LocationPingStatus.Outside, LocationPingStatus.Inaccurate);
        body.Pings[2].IsMocked.Should().BeTrue();
        body.Pings[0].DistanceM.Should().Be(0);
        body.Summary.Should().Be(new LocationPingsSummaryDto(4, 1, 2, 1, 5 + 10, 22, At(s.Day, 9, 37)));

        // Ping'siz kun — bo'sh, joy null (davomat yo'q).
        var empty = (await (await s.Admin.GetAsync(
            $"/api/admin/students/{student.Id}/location-pings?date={s.Day.AddDays(-3):yyyy-MM-dd}")).Content.ReadAsync<StudentLocationPingsDto>())!;
        empty.Pings.Should().BeEmpty();
        empty.Place.Should().BeNull();
        empty.Summary.Should().Be(new LocationPingsSummaryDto(0, 0, 0, 0, 0, 0, null));
    }

    [Fact]
    public async Task StudentPings_KolamVaRuxsat()
    {
        var s = await SceneAsync();
        var (student, _) = await StudentAsync(s, "Kolam");

        (await s.Admin.GetAsync($"/api/admin/students/{Guid.CreateVersion7()}/location-pings"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var studentClient = await Factory.LoginAsStudentAsync(student);
        (await studentClient.GetAsync($"/api/admin/students/{student.Id}/location-pings"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync("/api/admin/today")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().GetAsync($"/api/admin/students/{student.Id}/location-pings"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sozlamalar_UchtaYangiKalit_Validatsiya()
    {
        var client = await Factory.LoginAsAdminAsync();

        var settings = (await (await client.GetAsync("/api/admin/settings")).Content.ReadAsync<AdminSettingsDto>())!;
        var byKey = settings.Settings.ToDictionary(x => x.Key);
        byKey[SettingKeys.TrackingEnabled].Type.Should().Be(SettingType.Bool);
        byKey[SettingKeys.TrackingIntervalSec].Should().Match<SettingDto>(x =>
            x.Type == SettingType.Int && x.Min == 60 && x.Max == 900 && x.Unit == "sec");
        byKey[SettingKeys.TrackingDistanceFilterM].Should().Match<SettingDto>(x =>
            x.Type == SettingType.Int && x.Min == 10 && x.Max == 500 && x.Unit == "m");

        var bad = await client.PutAsJsonAsync("/api/admin/settings", new
        {
            values = new Dictionary<string, string>
            {
                [SettingKeys.TrackingIntervalSec] = "30",
                [SettingKeys.TrackingDistanceFilterM] = "600",
                [SettingKeys.TrackingEnabled] = "balki"
            }
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using (var json = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()))
        {
            var errors = json.RootElement.GetProperty("errors");
            errors.TryGetProperty(SettingKeys.TrackingIntervalSec, out _).Should().BeTrue();
            errors.TryGetProperty(SettingKeys.TrackingDistanceFilterM, out _).Should().BeTrue();
            errors.TryGetProperty(SettingKeys.TrackingEnabled, out _).Should().BeTrue();
        }

        try
        {
            var ok = await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string> { [SettingKeys.TrackingIntervalSec] = "120" }
            });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ok.Content.ReadAsync<AdminSettingsDto>())!.Settings
                .Single(x => x.Key == SettingKeys.TrackingIntervalSec).Value.Should().Be("120");

            // Talaba "today" yangi oraliqni oladi.
            var scene = await Factory.CreateSceneAsync();
            var today = (await (await scene.Client.GetAsync("/api/student/today"))
                .Content.ReadAsync<Amaliyotchi.Application.Features.Student.TodayDto>())!;
            today.Tracking.IntervalSec.Should().Be(120);
        }
        finally
        {
            await client.PutAsJsonAsync("/api/admin/settings", new
            {
                values = new Dictionary<string, string> { [SettingKeys.TrackingIntervalSec] = "300" }
            });
        }
    }

    [Fact]
    public async Task Tozalash_60KundanEskiPinglarOchiriladi()
    {
        var s = await SceneAsync();
        var (student, row) = await StudentAsync(s, "Eski");
        var oldDay = Factory.LocalToday().AddDays(-LocationTracking.RetentionDays - 1);
        var oldRow = await Factory.WithDbAsync(async db =>
        {
            var attendance = DailyAttendance.CheckIn(student.Id, s.Period.Id, oldDay, At(oldDay, 9, 0), 20, 10, CheckInVerdict.Accept());
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        });
        await AddPingsAsync(student, oldRow, (9, 30, In, 10, false));
        await AddPingsAsync(student, row!, (9, 30, In, 10, false));

        using (var scope = Factory.Services.CreateScope())
        {
            var deleted = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PurgeOldLocationPingsCommand());
            deleted.Should().BeGreaterThanOrEqualTo(1);
        }

        var remaining = await Factory.WithDbAsync(db => db.LocationPings.AsNoTracking()
            .Where(p => p.StudentUserId == student.Id).Select(p => p.Date).ToListAsync());
        remaining.Should().Equal(s.Day);
    }
}
