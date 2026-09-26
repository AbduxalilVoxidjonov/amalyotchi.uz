using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET /api/student/today</c> va <c>GET /api/student/place</c> shakli (kontrakt v2).
/// Bugungi holat <see cref="MutableClock"/> bilan muzlatilgan soatda tekshiriladi.</summary>
[Collection(ApiCollection.Name)]
public sealed class TodayAndPlaceTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Today_200_KontraktShakli()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);

        string body;
        try
        {
            // 09:05 — check-in oynasi ochiq.
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.GetAsync("/api/student/today");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            fixture.Clock.Reset();
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("date").GetString().Should().Be(day.ToString("yyyy-MM-dd"));

        var window = root.GetProperty("window");
        window.GetProperty("start").GetString().Should().MatchRegex(@"^\d{2}:\d{2}$");
        window.GetProperty("end").GetString().Should().MatchRegex(@"^\d{2}:\d{2}$");
        window.GetProperty("checkoutAt").GetString().Should().MatchRegex(@"^\d{2}:\d{2}$");
        window.GetProperty("isOpen").GetBoolean().Should().BeTrue("oyna ochiq, ariza tasdiqlangan");

        var checkin = root.GetProperty("checkin");
        checkin.GetProperty("status").GetString().Should().Be("pending", "enum camelCase string");
        checkin.GetProperty("checkInAt").ValueKind.Should().Be(JsonValueKind.Null);
        checkin.GetProperty("checkOutAt").ValueKind.Should().Be(JsonValueKind.Null);
        checkin.GetProperty("distanceM").ValueKind.Should().Be(JsonValueKind.Null);
        checkin.GetProperty("gpsAccuracyM").ValueKind.Should().Be(JsonValueKind.Null);
        checkin.GetProperty("suspicious").GetBoolean().Should().BeFalse();
        checkin.GetProperty("autoClosed").GetBoolean().Should().BeFalse();

        var place = root.GetProperty("place");
        place.GetProperty("company").GetString().Should().Be(scene.Company.Name);
        place.GetProperty("address").GetString().Should().Be(scene.Company.Address);
        place.GetProperty("radiusM").GetInt32().Should().Be(scene.Company.RadiusM);
        place.GetProperty("attendancePct").GetDouble().Should().BeInRange(0, 100);
        place.GetProperty("daysTotal").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        place.GetProperty("reports").GetInt32().Should().Be(0);

        var diary = root.GetProperty("diary");
        diary.GetProperty("submittedToday").GetBoolean().Should().BeFalse();
        diary.GetProperty("minChars").GetInt32().Should().Be(150);
        diary.GetProperty("maxFiles").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Today_FaolDavrYoq_200_PendingVaIzoh()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var response = await client.GetAsync("/api/student/today");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var today = await response.Content.ReadAsync<TodayDto>();
        today!.Place.Should().BeNull();
        today.Checkin.Status.Should().Be(AttendanceStatus.Pending);
        today.Checkin.Note.Should().Contain("davri yo'q");
        today.Window.IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Today_TasdiqlanganRuxsat_Excused()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        await Factory.WithDbAsync(async db =>
        {
            var leave = Domain.Leave.LeaveRequest.Create(scene.Student.Id, scene.Period.Id, day, day, "Shifokor ko'rigi, ma'lumotnoma bor");
            leave.Approve(scene.Tutor.Id, null, PracticeTime.At(day.AddDays(-1), new TimeOnly(12, 0)));
            db.LeaveRequests.Add(leave);
            await db.SaveChangesAsync();
        });

        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var dto = await (await scene.Client.GetAsync("/api/student/today")).Content.ReadAsync<TodayDto>();

            dto!.Checkin.Status.Should().Be(AttendanceStatus.Excused);
            dto.Window.IsOpen.Should().BeFalse();
            dto.Checkin.Note.Should().Contain("ruxsat");

            await using (await Factory.WithoutPhotoRequirementAsync())
            {
                var response = await scene.Client.PostJsonAsync("/api/student/checkin", Factory.Geo(qr: scene.Qr()));
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                (await response.Content.ReadAsStringAsync()).Should().Contain("ruxsat");
            }
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Place_200_KorxonaVaAriza()
    {
        var scene = await Factory.CreateSceneAsync();

        var response = await scene.Client.GetAsync("/api/student/place");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var place = await response.Content.ReadAsync<PracticePlaceDto>();
        place!.Status.Should().Be(ApplicationStatus.Approved);
        place.Company.Should().Be(scene.Company.Name);
        place.Tin.Should().Be(scene.Company.Tin);
        place.Address.Should().Be(scene.Company.Address);
        place.SupervisorName.Should().Be(scene.Company.SupervisorName);
        place.SupervisorPhone.Should().StartWith("+998");
        place.RadiusM.Should().Be(scene.Company.RadiusM);
        place.Lat.Should().BeApproximately(StudentTestData.CompanyLat, 1e-6);
        place.Lng.Should().BeApproximately(StudentTestData.CompanyLng, 1e-6);
        place.PeriodFrom.Should().Be(scene.Period.StartDate);
        place.PeriodTo.Should().Be(scene.Period.EndDate);
        place.Contract.Should().BeNull("test arizasida shartnoma fayli yo'q");
        place.Comment.Should().Be("OK");
        place.PeriodId.Should().Be(scene.Period.Id);
        place.PeriodName.Should().Be(scene.Period.Name);
        place.IsPast.Should().BeFalse("faol davrdagi tasdiqlangan ariza — hozirgi korxona");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().Should().Be("approved");
        json.RootElement.GetProperty("periodId").GetString().Should().Be(scene.Period.Id.ToString());
        json.RootElement.GetProperty("periodName").GetString().Should().Be(scene.Period.Name);
        json.RootElement.GetProperty("isPast").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Place_YopilganDavr_SanasiOtmagan_IsPastTrue()
    {
        var scene = await Factory.CreateSceneAsync();
        await Factory.WithDbAsync(async db =>
        {
            var period = await db.PracticePeriods.FirstAsync(p => p.Id == scene.Period.Id);
            period.Close();
            await db.SaveChangesAsync();
        });

        var response = await scene.Client.GetAsync("/api/student/place");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var place = await response.Content.ReadAsync<PracticePlaceDto>();
        place!.Company.Should().Be(scene.Company.Name, "yopilgan davr korxonasi oxirgi tugagan davr sifatida qaytadi");
        place.PeriodId.Should().Be(scene.Period.Id);
        place.PeriodName.Should().Be(scene.Period.Name);
        place.PeriodTo.Should().BeAfter(Factory.LocalToday(), "sanasi hali o'tmagan");
        place.IsPast.Should().BeTrue("davr yopilgan — talaba hozir bu korxonaga biriktirilmagan");
    }

    [Fact]
    public async Task Place_TugaganDavr_IsPastTrue()
    {
        var today = Factory.LocalToday();
        var scene = await Factory.CreateSceneAsync(
            period: (group, tutorId) => Factory.CreatePeriodAtAsync(group, tutorId, today, startDaysAgo: 40, endDaysAhead: -10));

        var response = await scene.Client.GetAsync("/api/student/place");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var place = await response.Content.ReadAsync<PracticePlaceDto>();
        place!.Company.Should().Be(scene.Company.Name);
        place.PeriodId.Should().Be(scene.Period.Id);
        place.PeriodName.Should().Be(scene.Period.Name);
        place.PeriodTo.Should().BeBefore(today);
        place.IsPast.Should().BeTrue("davr tugagan (EndDate < bugun)");
    }

    [Fact]
    public async Task Place_ShartnomaFayli_BorBolsa()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var company = await Factory.CreateCompanyAsync();
        var period = await Factory.CreateActivePeriodAsync(group, tutor.Id);
        var file = await Factory.CreateStoredFileAsync(student.Id);
        await Factory.WithDbAsync(async db =>
        {
            var application = PracticeApplication.Create(student.Id, period.Id, company.Id, company.RadiusM, file.Id, DateTimeOffset.UtcNow.AddDays(-2));
            db.PracticeApplications.Add(application);
            await db.SaveChangesAsync();
        });
        var client = await Factory.LoginAsStudentAsync(student);

        var place = await (await client.GetAsync("/api/student/place")).Content.ReadAsync<PracticePlaceDto>();

        place!.Status.Should().Be(ApplicationStatus.Submitted);
        place.Contract.Should().NotBeNull();
        place.Contract!.FileName.Should().Be("shartnoma.pdf");
        place.Contract.ApprovedAt.Should().BeNull();
        (await client.GetAsync($"/api/files/{place.Contract.FileId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Place_ArizaYoq_404()
    {
        var scene = await Factory.CreateSceneAsync(approve: false);

        var response = await scene.Client.GetAsync("/api/student/place");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("biriktirilmagan");
    }

    [Fact]
    public async Task Place_Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/place")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
