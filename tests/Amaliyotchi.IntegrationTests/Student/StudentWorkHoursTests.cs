using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Profile;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Application.Features.Tutor.Today;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>PUT /api/student/profile/work-hours</c> va <c>GET /api/student/profile</c> dagi <c>workHours</c>:
/// talaba o'z kelish/ketish vaqtini belgilaydi, o'zgarish ERTADAN kuchga kiradi; keyin check-in, tyutor "bugun" va
/// tyutor profili shu soatlardan foydalanadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentWorkHoursTests(ApiFixture fixture) : IAsyncLifetime
{
    private const string Url = "/api/student/profile/work-hours";

    private ApiFactory Factory => fixture.Factory;

    private IAsyncDisposable? _photoSetting;

    public async Task InitializeAsync() => _photoSetting = await Factory.WithoutPhotoRequirementAsync();

    public async Task DisposeAsync()
    {
        if (_photoSetting is not null)
            await _photoSetting.DisposeAsync();
    }

    [Fact]
    public async Task Belgilash_200_ErtadanKuchgaKiradi_CheckInVaTyutorShuSoatlardan()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var tutorClient = await Factory.LoginAsync(scene.Tutor);
        var next = await NextWorkDayAfterAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));

            // Profil: hali o'z soatlari yo'q — davr soatlari.
            var before = await ProfileAsync(scene.Client);
            before.WorkHours.Should().Be(new StudentWorkHoursDto(null, null, null, "09:00", "17:00", "09:00", "17:00"));

            var response = await scene.Client.PutAsJsonAsync(Url, new { start = "13:00", end = "18:00" });
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            var dto = JsonSerializer.Deserialize<StudentWorkHoursDto>(body, JsonDefaults.Options)!;
            dto.Should().Be(new StudentWorkHoursDto("13:00", "18:00", day.AddDays(1), "09:00", "17:00", "09:00", "17:00"),
                "bugun hali davr soatlari amalda");

            using (var json = JsonDocument.Parse(body))
            {
                json.RootElement.GetProperty("start").GetString().Should().Be("13:00");
                json.RootElement.GetProperty("effectiveFrom").GetString().Should().Be(day.AddDays(1).ToString("yyyy-MM-dd"));
                json.RootElement.GetProperty("todayStart").GetString().Should().Be("09:00");
                json.RootElement.GetProperty("periodEnd").GetString().Should().Be("17:00");
            }

            // Profil GET ham xuddi shu blokni qaytaradi.
            (await ProfileAsync(scene.Client)).WorkHours.Should().Be(dto);

            // Bugun tyutor profili — davr soatlari.
            var detailToday = await DetailAsync(tutorClient, scene.Student.Id);
            detailToday.Period!.DailyStart.Should().Be("09:00");
            detailToday.Period.CustomWorkHours.Should().BeFalse();

            // Keyingi ish kuni 11:00 — davr oynasi (10:30) yopilgan, lekin talabaning o'z oynasi (13:00–14:30) hali kelmagan.
            fixture.Clock.Set(PracticeTime.At(next, new TimeOnly(11, 0)));
            var profileNext = await ProfileAsync(scene.Client);
            profileNext.WorkHours.TodayStart.Should().Be("13:00");
            profileNext.WorkHours.TodayEnd.Should().Be("18:00");
            profileNext.WorkHours.PeriodStart.Should().Be("09:00");

            var detail = await DetailAsync(tutorClient, scene.Student.Id);
            detail.Period!.DailyStart.Should().Be("13:00");
            detail.Period.DailyEnd.Should().Be("18:00");
            detail.Period.CustomWorkHours.Should().BeTrue();

            var tutorToday = (await (await tutorClient.GetAsync("/api/tutor/today")).Content.ReadAsync<TodayResponse>())!;
            tutorToday.Rows.Items.Single(r => r.StudentId == scene.Student.Id).Status
                .Should().Be(AttendanceStatus.Pending, "talabaning o'z oynasi hali ochilmagan");

            // 11:00 da check-in — o'z oynasi ochilmagan → rad.
            var early = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Company.CheckInQrPayload));
            early.StatusCode.Should().NotBe(HttpStatusCode.OK, await early.Content.ReadAsStringAsync());

            // 13:20 — o'z kechikish chegarasi (13:15) dan keyin → late.
            fixture.Clock.Set(PracticeTime.At(next, new TimeOnly(13, 20)));
            var checkIn = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Company.CheckInQrPayload));
            checkIn.StatusCode.Should().Be(HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
            (await checkIn.Content.ReadAsync<TodayDto>())!.Checkin.Status.Should().Be(AttendanceStatus.Late);
        }
        finally
        {
            fixture.Clock.Reset();
        }

        await Factory.WithDbAsync(async db =>
        {
            var log = await db.AuditLogs.AsNoTracking()
                .SingleAsync(l => l.Action == AuditAction.StudentWorkHoursChanged && l.UserId == scene.Student.Id);
            log.Changes.Should().Contain("13:00").And.Contain("18:00");
        });
    }

    [Fact]
    public async Task Bekor_IkkalasiNull_DavrSoatlarigaQaytadi()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(8, 0)));
            (await scene.Client.PutAsJsonAsync(Url, new { start = "08:00", end = "12:00" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var reset = await scene.Client.PutAsJsonAsync(Url, new { start = (string?)null, end = (string?)null });
            reset.StatusCode.Should().Be(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());
            var dto = (await reset.Content.ReadAsync<StudentWorkHoursDto>())!;
            dto.Start.Should().BeNull();
            dto.End.Should().BeNull();
            dto.TodayStart.Should().Be("09:00");

            using var json = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
            json.RootElement.GetProperty("start").ValueKind.Should().Be(JsonValueKind.Null);
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

        async Task<JsonElement> Bad(object body)
        {
            var response = await scene.Client.PutAsJsonAsync(Url, body);
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
            .Should().Be("Ketish vaqti kelish vaqtidan keyin bo'lishi kerak.");
        (await Bad(new { start = "09:00", end = "09:45" })).GetProperty("End")[0].GetString()
            .Should().Be("Ish vaqti kamida 1 soat bo'lishi kerak.");

        await Factory.WithDbAsync(async db =>
            (await db.StudentProfiles.AsNoTracking().SingleAsync(p => p.UserId == scene.Student.Id))
                .WorkHoursEffectiveFrom.Should().BeNull("rad etilgan so'rovlar hech narsa saqlamaydi"));
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var scene = await Factory.CreateSceneAsync();
        var tutorClient = await Factory.LoginAsync(scene.Tutor);

        var response = await tutorClient.PutAsJsonAsync(Url, new { start = "09:00", end = "17:00" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<StudentProfileDto> ProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/student/profile");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<StudentProfileDto>(body, JsonDefaults.Options)!;
    }

    private static async Task<TutorStudentDetail> DetailAsync(HttpClient client, Guid studentId)
        => (await client.GetFromJsonAsync<TutorStudentDetail>($"/api/tutor/students/{studentId}", JsonDefaults.Options))!;

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
