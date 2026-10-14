using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Profile;
using Amaliyotchi.Application.Features.Supervision.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>PUT /api/admin/students/{id}/work-hours</c>: talabaning ish vaqtini FAQAT admin belgilaydi (o'zgarish ERTADAN
/// kuchga kiradi); <c>GET /api/admin/students/{id}</c> va <c>GET /api/student/profile</c> dagi <c>workHours</c> (talaba uchun
/// faqat o'qish). Keyin check-in oynasi, "kech keldi" chegarasi, auto-close va admin "bugun" shu soatlardan foydalanadi.
/// Talabaning eski <c>PUT /api/student/profile/work-hours</c> yo'li olib tashlangan.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminStudentWorkHoursTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string RemovedStudentUrl = "/api/student/profile/work-hours";

    private static string Url(Guid studentId) => $"/api/admin/students/{studentId}/work-hours";

    private ApiFactory Factory => fixture.Factory;

    private IAsyncDisposable? _photoSetting;

    public async Task InitializeAsync() => _photoSetting = await Factory.WithoutPhotoRequirementAsync();

    public async Task DisposeAsync()
    {
        if (_photoSetting is not null)
            await _photoSetting.DisposeAsync();
    }

    [Fact]
    public async Task Belgilash_200_ErtadanKuchgaKiradi_CheckInAutoCloseVaProfilShuSoatlardan()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var adminClient = await Factory.LoginAsync(scene.Admin);
        var next = await NextWorkDayAfterAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));

            // Hali belgilanmagan — davr soatlari.
            var expectedBefore = new StudentWorkHoursDto(null, null, null, "09:00", "17:00", "09:00", "17:00");
            (await ProfileAsync(scene.Client)).WorkHours.Should().Be(expectedBefore);
            (await DetailAsync(adminClient, scene.Student.Id)).WorkHours.Should().Be(expectedBefore);

            var response = await adminClient.PutAsJsonAsync(Url(scene.Student.Id), new { start = "13:00", end = "18:00" });
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            var detail = JsonSerializer.Deserialize<AdminStudentDetail>(body, JsonDefaults.Options)!;
            detail.Id.Should().Be(scene.Student.Id);
            var expected = new StudentWorkHoursDto("13:00", "18:00", day.AddDays(1), "09:00", "17:00", "09:00", "17:00");
            detail.WorkHours.Should().Be(expected, "bugun hali davr soatlari amalda");
            detail.Period!.DailyStart.Should().Be("09:00");
            detail.Period.CustomWorkHours.Should().BeFalse();

            using (var json = JsonDocument.Parse(body))
            {
                var workHours = json.RootElement.GetProperty("workHours");
                workHours.GetProperty("start").GetString().Should().Be("13:00");
                workHours.GetProperty("effectiveFrom").GetString().Should().Be(day.AddDays(1).ToString("yyyy-MM-dd"));
                workHours.GetProperty("todayStart").GetString().Should().Be("09:00");
                workHours.GetProperty("periodEnd").GetString().Should().Be("17:00");
            }

            // Talaba profili — xuddi shu blok (faqat o'qish uchun).
            (await ProfileAsync(scene.Client)).WorkHours.Should().Be(expected);
            (await DetailAsync(adminClient, scene.Student.Id)).WorkHours.Should().Be(expected);

            // Keyingi ish kuni 11:00 — davr oynasi (10:30) yopilgan, lekin talabaning oynasi (13:00–14:30) hali kelmagan.
            fixture.Clock.Set(PracticeTime.At(next, new TimeOnly(11, 0)));
            var profileNext = await ProfileAsync(scene.Client);
            profileNext.WorkHours.TodayStart.Should().Be("13:00");
            profileNext.WorkHours.TodayEnd.Should().Be("18:00");
            profileNext.WorkHours.PeriodStart.Should().Be("09:00");

            var detailNext = await DetailAsync(adminClient, scene.Student.Id);
            detailNext.Period!.DailyStart.Should().Be("13:00");
            detailNext.Period.DailyEnd.Should().Be("18:00");
            detailNext.Period.CustomWorkHours.Should().BeTrue();

            var today = (await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>())!;
            today.Window.Start.Should().Be("13:00");
            today.Window.CheckoutAt.Should().Be("18:00");

            var marker = "Soat" + Guid.NewGuid().ToString("N")[..8];
            await Factory.WithDbAsync(db => db.Users.Where(u => u.Id == scene.Student.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.FullName, marker)));
            var adminToday = (await (await adminClient.GetAsync($"/api/admin/today?q={marker}"))
                .Content.ReadAsync<TodayResponse>())!;
            adminToday.Rows.Items.Single(r => r.StudentId == scene.Student.Id).Status
                .Should().Be(AttendanceStatus.Pending, "talabaning oynasi hali ochilmagan");

            // 11:00 da check-in — oyna ochilmagan → rad.
            var early = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Company.CheckInQrPayload));
            early.StatusCode.Should().NotBe(HttpStatusCode.OK, await early.Content.ReadAsStringAsync());

            // 13:20 — kechikish chegarasi (13:15) dan keyin → late.
            fixture.Clock.Set(PracticeTime.At(next, new TimeOnly(13, 20)));
            var checkIn = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Company.CheckInQrPayload));
            checkIn.StatusCode.Should().Be(HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
            (await checkIn.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Late);

            // 18:30 — davr soatlarida (17:00 + 60 daq) kun allaqachon avtomatik yopilgan bo'lardi; admin belgilagan
            // soatlar bo'yicha auto-close 19:00 da — check-out qabul qilinadi.
            fixture.Clock.Set(PracticeTime.At(next, new TimeOnly(18, 30)));
            var checkOut = await scene.Client.PostJsonAsync("/api/student/checkout", Factory.Geo(qr: scene.Company.CheckInQrPayload));
            checkOut.StatusCode.Should().Be(HttpStatusCode.OK, await checkOut.Content.ReadAsStringAsync());
            (await checkOut.Content.ReadAsync<TodayDto>())!.Checkin.AutoClosed.Should().BeFalse();
        }
        finally
        {
            fixture.Clock.Reset();
        }

        await Factory.WithDbAsync(async db =>
        {
            var log = await db.AuditLogs.AsNoTracking()
                .SingleAsync(l => l.Action == AuditAction.StudentWorkHoursChanged && l.UserId == scene.Admin.Id);
            log.Changes.Should().Contain("13:00").And.Contain("18:00").And.Contain(scene.Student.Id.ToString());
        });
    }

    [Fact]
    public async Task Bekor_IkkalasiNull_DavrSoatlarigaQaytadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var adminClient = await Factory.LoginAsync(scene.Admin);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(8, 0)));
            (await adminClient.PutAsJsonAsync(Url(scene.Student.Id), new { start = "08:00", end = "12:00" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var reset = await adminClient.PutAsJsonAsync(Url(scene.Student.Id), new { start = (string?)null, end = (string?)null });
            var body = await reset.Content.ReadAsStringAsync();
            reset.StatusCode.Should().Be(HttpStatusCode.OK, body);
            var dto = JsonSerializer.Deserialize<AdminStudentDetail>(body, JsonDefaults.Options)!.WorkHours;
            dto.Start.Should().BeNull();
            dto.End.Should().BeNull();
            dto.TodayStart.Should().Be("09:00");

            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("workHours").GetProperty("start").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Validatsiya_400()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var adminClient = await Factory.LoginAsync(scene.Admin);

        async Task<JsonElement> Bad(object body)
        {
            var response = await adminClient.PutAsJsonAsync(Url(scene.Student.Id), body);
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
            using var json = JsonDocument.Parse(text);
            return json.RootElement.GetProperty("errors").Clone();
        }

        (await Bad(new { start = "9-00", end = "17:00" })).GetProperty("Start")[0].GetString()
            .Should().Be(StudentWorkHoursRules.TimeFormatMessage);
        (await Bad(new { start = "09:00", end = "25:00" })).GetProperty("End")[0].GetString()
            .Should().Be(StudentWorkHoursRules.TimeFormatMessage);
        (await Bad(new { start = "09:00", end = (string?)null })).GetProperty("End")[0].GetString()
            .Should().Be(StudentWorkHoursRules.EndRequiredMessage);
        (await Bad(new { start = (string?)null, end = "17:00" })).GetProperty("Start")[0].GetString()
            .Should().Be(StudentWorkHoursRules.StartRequiredMessage);
        (await Bad(new { start = "17:00", end = "09:00" })).GetProperty("End")[0].GetString()
            .Should().Be(StudentWorkHoursRules.EndBeforeStartMessage);
        (await Bad(new { start = "09:00", end = "09:45" })).GetProperty("End")[0].GetString()
            .Should().Be(StudentWorkHoursRules.TooShortMessage);

        await Factory.WithDbAsync(async db =>
            (await db.StudentProfiles.AsNoTracking().SingleAsync(p => p.UserId == scene.Student.Id))
                .WorkHoursEffectiveFrom.Should().BeNull("rad etilgan so'rovlar hech narsa saqlamaydi"));
    }

    [Fact]
    public async Task NomalumTalaba_404()
    {
        var adminClient = await Factory.LoginAsAdminAsync();

        var response = await adminClient.PutAsJsonAsync(Url(Guid.NewGuid()), new { start = "09:00", end = "17:00" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Talaba_OzgartiraOlmaydi_EskiYolYoq_AdminYoli403()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        var removed = await scene.Client.PutAsJsonAsync(RemovedStudentUrl, new { start = "13:00", end = "18:00" });
        removed.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        var admin = await scene.Client.PutAsJsonAsync(Url(scene.Student.Id), new { start = "13:00", end = "18:00" });
        admin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Factory.WithDbAsync(async db =>
            (await db.StudentProfiles.AsNoTracking().SingleAsync(p => p.UserId == scene.Student.Id))
                .WorkHoursEffectiveFrom.Should().BeNull("talaba ish vaqtini o'zgartira olmaydi"));
    }

    private static async Task<StudentProfileDto> ProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/student/profile");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<StudentProfileDto>(body, JsonDefaults.Options)!;
    }

    private static async Task<AdminStudentDetail> DetailAsync(HttpClient client, Guid studentId)
        => (await client.GetFromJsonAsync<AdminStudentDetail>($"/api/admin/students/{studentId}", JsonDefaults.Options))!;

    private Task<DateOnly> NextWorkDayAfterAsync(DateOnly day) =>
        Factory.WithDbAsync(async db =>
        {
            var holidays = await db.Holidays.AsNoTracking().ToListAsync();
            var next = day.AddDays(1);
            while (holidays.Any(h => h.AppliesTo(next)))
                next = next.AddDays(1);
            return next;
        });
}
