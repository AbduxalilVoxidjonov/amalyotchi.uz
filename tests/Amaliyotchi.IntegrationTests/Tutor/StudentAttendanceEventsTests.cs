using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.ValueObjects;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

/// <summary>Kundalik jadvalda kunning barcha check-in/check-out urinishlari (<c>events</c>) —
/// <c>GET /api/tutor/students/{id}/attendance</c> va <c>GET /api/admin/students/{id}/attendance</c>.
/// Urinishlar API orqali emas, bazaga to'g'ridan-to'g'ri yoziladi (check-in sozlamalariga bog'liq bo'lmasin).</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentAttendanceEventsTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Events_RadVaQabul_TartibSababRasm_TyutorVaAdmin()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        days.Should().HaveCount(3);
        var (day, emptyDay, otherDay) = (days[0], days[1], days[2]);

        var rejectedPhoto = await Factory.CreateStoredFileAsync(
            s.Student.Id, StoredFileKind.CheckInPhoto, "rad.jpg", "image/jpeg", Encoding.UTF8.GetBytes("rad"));
        var acceptedPhoto = await Factory.CreateStoredFileAsync(
            s.Student.Id, StoredFileKind.CheckInPhoto, "qabul.jpg", "image/jpeg", Encoding.UTF8.GetBytes("qabul"));

        // Tartibsiz yoziladi — javobda vaqt bo'yicha o'sish tartibida bo'lishi kerak.
        var checkOut = await AddEventAsync(s.Student, s.Company, day, AttendanceEventKind.CheckOut, new TimeOnly(17, 5),
            CheckInVerdict.Accept(), 30, 41.3115, 69.2799, photoId: null);
        var rejected = await AddEventAsync(s.Student, s.Company, day, AttendanceEventKind.CheckIn, new TimeOnly(8, 50),
            CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius), 3400, 41.2800, 69.2200, rejectedPhoto.Id);
        var accepted = await AddEventAsync(s.Student, s.Company, day, AttendanceEventKind.CheckIn, new TimeOnly(8, 58),
            CheckInVerdict.Accept(), 20, 41.3112, 69.2798, acceptedPhoto.Id);
        await Factory.AddAttendanceAsync(s.Student, s.Period, day, distanceM: 20);

        // Boshqa kun hodisasi aralashmasligi kerak.
        await AddEventAsync(s.Student, s.Company, otherDay, AttendanceEventKind.CheckIn, new TimeOnly(9, 1),
            CheckInVerdict.Accept(), 10, 41.3111, 69.2797, photoId: null);

        var admin = await Factory.LoginAsAdminAsync();
        foreach (var (client, url) in new[]
                 {
                     (s.Client, $"/api/tutor/students/{s.Student.Id}/attendance"),
                     (admin, $"/api/admin/students/{s.Student.Id}/attendance")
                 })
        {
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK, url);
            var body = (await response.Content.ReadAsync<List<StudentAttendanceDay>>())!;

            var target = body.Single(d => d.Date == day);
            target.Events.Should().HaveCount(3, url);
            target.Events.Select(e => e.Id).Should().Equal(rejected.Id, accepted.Id, checkOut.Id);
            target.Events.Select(e => e.AtIso).Should().BeInAscendingOrder();

            var r = target.Events[0];
            r.Kind.Should().Be(AttendanceEventKind.CheckIn);
            r.At.Should().Be("08:50");
            r.AtIso.Should().Be(PracticeTime.At(day, new TimeOnly(8, 50)));
            r.AtIso.Offset.Should().Be(PracticeTime.Offset);
            r.Accepted.Should().BeFalse();
            r.RejectReason.Should().Be(CheckInRejectReason.OutOfRadius);
            r.RejectMessage.Should().Be(CheckInRejectReason.OutOfRadius.Message());
            r.DistanceM.Should().Be(3400);
            r.AccuracyM.Should().Be(10);
            r.RadiusM.Should().Be(s.Company.RadiusM);
            r.Lat.Should().BeApproximately(41.2800, 0.0001);
            r.Lng.Should().BeApproximately(69.2200, 0.0001);
            r.PhotoUrl.Should().Be($"/api/files/{rejectedPhoto.Id}");

            var a = target.Events[1];
            a.Kind.Should().Be(AttendanceEventKind.CheckIn);
            a.At.Should().Be("08:58");
            a.Accepted.Should().BeTrue();
            a.RejectReason.Should().BeNull();
            a.RejectMessage.Should().BeNull();
            a.PhotoUrl.Should().Be($"/api/files/{acceptedPhoto.Id}");

            var o = target.Events[2];
            o.Kind.Should().Be(AttendanceEventKind.CheckOut);
            o.At.Should().Be("17:05");
            o.Accepted.Should().BeTrue();
            o.PhotoUrl.Should().BeNull();

            // Mavjud maydonlar o'zgarmagan.
            target.Attempts.Should().Be(2);
            target.RejectedAttempts.Should().Be(1);
            target.CheckIn.Should().NotBeNull();

            body.Single(d => d.Date == emptyDay).Events.Should().BeEmpty("hodisasiz kun — []");
            var other = body.Single(d => d.Date == otherDay);
            other.Events.Should().ContainSingle().Which.At.Should().Be("09:01");
            body.Where(d => d.Date != day && d.Date != otherDay).Should().OnlyContain(d => d.Events.Count == 0);
        }
    }

    [Fact]
    public async Task Events_Json_CamelCaseEnumVaNull()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var day = (await Factory.PastWorkDaysAsync(s.Period, 1)).Single();
        await AddEventAsync(s.Student, s.Company, day, AttendanceEventKind.CheckIn, new TimeOnly(8, 50),
            CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius), 3400, 41.28, 69.22, photoId: null);
        await AddEventAsync(s.Student, s.Company, day, AttendanceEventKind.CheckOut, new TimeOnly(17, 5),
            CheckInVerdict.Accept(), 30, 41.31, 69.28, photoId: null);

        var json = await s.Client.GetStringAsync(
            $"/api/tutor/students/{s.Student.Id}/attendance?from={Iso(day)}&to={Iso(day)}");
        using var doc = JsonDocument.Parse(json);
        var events = doc.RootElement[0].GetProperty("events");

        events.GetArrayLength().Should().Be(2);
        events[0].GetProperty("kind").GetString().Should().Be("checkIn");
        events[0].GetProperty("rejectReason").GetString().Should().Be("outOfRadius");
        events[0].GetProperty("rejectMessage").GetString().Should().NotBeNullOrEmpty();
        events[0].GetProperty("photoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        foreach (var name in new[] { "id", "at", "atIso", "accepted", "distanceM", "accuracyM", "radiusM", "lat", "lng" })
            events[0].TryGetProperty(name, out _).Should().BeTrue(name);
        events[1].GetProperty("kind").GetString().Should().Be("checkOut");
        events[1].GetProperty("rejectReason").ValueKind.Should().Be(JsonValueKind.Null);
        events[1].GetProperty("rejectMessage").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Events_BoshqaDavrHodisalari_Aralashmaydi()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var old = await Factory.WithDbAsync(async db =>
        {
            var period = PracticePeriod.Create(
                $"Eski {Guid.NewGuid():N}"[..20], s.Group.AcademicYearId, today.AddDays(-80), today.AddDays(-40),
                s.Tutor.Id, CheckInRules.Default, WorkDays.MondayToSaturday | WorkDays.Sunday, 30, dailyReportRequired: true);
            period.AttachGroup(s.Group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });
        var oldDay = old.StartDate.AddDays(3);
        var oldEvent = await AddEventAsync(s.Student, s.Company, oldDay, AttendanceEventKind.CheckIn, new TimeOnly(9, 0),
            CheckInVerdict.Accept(), 15, 41.3111, 69.2797, photoId: null);
        var currentDay = (await Factory.PastWorkDaysAsync(s.Period, 1)).Single();
        var currentEvent = await AddEventAsync(s.Student, s.Company, currentDay, AttendanceEventKind.CheckIn,
            new TimeOnly(9, 0), CheckInVerdict.Accept(), 15, 41.3111, 69.2797, photoId: null);

        var current = (await (await s.Client.GetAsync(
                $"/api/tutor/students/{s.Student.Id}/attendance?periodId={s.Period.Id}"))
            .Content.ReadAsync<List<StudentAttendanceDay>>())!;
        current.SelectMany(d => d.Events).Select(e => e.Id).Should().Equal(currentEvent.Id);

        var past = (await (await s.Client.GetAsync(
                $"/api/tutor/students/{s.Student.Id}/attendance?periodId={old.Id}"))
            .Content.ReadAsync<List<StudentAttendanceDay>>())!;
        past.SelectMany(d => d.Events).Select(e => e.Id).Should().Equal(oldEvent.Id);
        past.Single(d => d.Date == oldDay).Events.Should().ContainSingle();
    }

    private Task<AttendanceEvent> AddEventAsync(
        TestUser student, Company company, DateOnly date, AttendanceEventKind kind, TimeOnly at,
        CheckInVerdict verdict, double distanceM, double lat, double lng, Guid? photoId) =>
        Factory.WithDbAsync(async db =>
        {
            var when = PracticeTime.At(date, at);
            var attendanceEvent = AttendanceEvent.Record(
                student.Id, company.Id, date, kind, when, when, new GeoPoint(lat, lng), 10, distanceM,
                company.RadiusM, verdict, photoId);
            db.AttendanceEvents.Add(attendanceEvent);
            await db.SaveChangesAsync();
            return attendanceEvent;
        });

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
