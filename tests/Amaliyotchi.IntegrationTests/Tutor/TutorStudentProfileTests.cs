using System.Globalization;
using System.Net;
using System.Text;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Files;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Tutor;

/// <summary>Tyutor — talaba profili: <c>GET /api/tutor/students/{id}</c>, <c>.../attendance</c>, <c>.../diaries</c>.</summary>
[Collection(ApiCollection.Name)]
public sealed class TutorStudentProfileTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Profil_TolaMalumot_KorxonaArizaDavrStatistika()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        days.Should().HaveCount(3, "davr 14 kun oldin boshlangan");
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[0]);
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[1], late: true);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 5);

        var response = await s.Client.GetAsync($"/api/tutor/students/{s.Student.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadAsync<TutorStudentDetail>())!;

        detail.Id.Should().Be(s.Student.Id);
        detail.Name.Should().Be(s.Student.FullName);
        detail.HemisId.Should().NotBeNullOrEmpty();
        detail.Group.Should().Be(s.Group.GroupName);
        detail.Course.Should().Be(s.Group.Course);
        detail.Faculty.Should().NotBeNullOrEmpty();
        detail.Direction.Should().NotBeNullOrEmpty();
        detail.Status.Should().Be(StudentStatus.Active);
        detail.Phone.Should().NotBeNullOrEmpty();

        detail.Company.Should().NotBeNull();
        detail.Company!.Id.Should().Be(s.Company.Id);
        detail.Company.Name.Should().Be(s.Company.Name);
        detail.Company.Tin.Should().Be(s.Company.Tin);
        detail.Company.RadiusM.Should().Be(s.Company.RadiusM);
        detail.Company.Lat.Should().BeApproximately(41.3111, 0.0001);
        detail.Company.Lng.Should().BeApproximately(69.2797, 0.0001);

        detail.Application.Should().NotBeNull();
        detail.Application!.Id.Should().Be(s.Application.Id);
        detail.Application.Status.Should().Be(ApplicationStatus.Approved);
        detail.Application.DecidedAt.Should().NotBeNull();

        detail.Period.Should().NotBeNull();
        detail.Period!.Id.Should().Be(s.Period.Id);
        detail.Period.Name.Should().Be(s.Period.Name);
        detail.Period.StartDate.Should().Be(s.Period.StartDate);
        detail.Period.EndDate.Should().Be(s.Period.EndDate);
        detail.Period.DailyStart.Should().Be("09:00");
        detail.Period.DailyEnd.Should().Be("17:00");
        detail.Period.WorkDays.Should().Equal(1, 2, 3, 4, 5, 6);
        detail.Period.RequiredDays.Should().Be(36);

        detail.Attendance.AttendedDays.Should().Be(2);
        detail.Attendance.LateDays.Should().Be(1);
        detail.Attendance.ExcusedDays.Should().Be(0);
        detail.Attendance.SuspiciousDays.Should().Be(0);
        detail.Attendance.TotalDays.Should().BeGreaterThanOrEqualTo(3);
        detail.Attendance.AbsentDays.Should().Be(detail.Attendance.TotalDays - 2);
        detail.Attendance.AttendancePct.Should()
            .BeApproximately(Math.Round(200d / detail.Attendance.TotalDays, 1), 0.11);

        detail.Diary.Count.Should().Be(1);
        detail.Diary.ScoredCount.Should().Be(1);
        detail.Diary.Avg.Should().Be(5);

        detail.Grade.Should().NotBeNull();
        detail.Grade!.Total.Should().BeGreaterThan(0);
        detail.State.Should().Be(StudentState.RedFlag, "ikki kunlik davomat 70% dan past");
    }

    [Fact]
    public async Task Profil_FaolDavrYoq_DavrVaBahoNull_DavomatBosh()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var client = await Factory.LoginAsync(tutor);
        var student = await Factory.CreateStudentAsync(group: group);

        var detail = (await (await client.GetAsync($"/api/tutor/students/{student.Id}")).Content
            .ReadAsync<TutorStudentDetail>())!;

        detail.Period.Should().BeNull();
        detail.Grade.Should().BeNull();
        detail.Company.Should().BeNull();
        detail.Application.Should().BeNull();
        detail.Attendance.TotalDays.Should().Be(0);
        detail.Attendance.AttendancePct.Should().Be(0);

        var attendance = await client.GetAsync($"/api/tutor/students/{student.Id}/attendance");
        attendance.StatusCode.Should().Be(HttpStatusCode.OK);
        (await attendance.Content.ReadAsync<List<StudentAttendanceDay>>())!.Should().BeEmpty("davr yo'q — xato emas");
    }

    [Fact]
    public async Task Profil_KolamdanTashqariTalaba_404_TalabaOzi_403()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var stranger = await Factory.CreateStudentAsync();

        foreach (var url in new[]
                 {
                     $"/api/tutor/students/{stranger.Id}",
                     $"/api/tutor/students/{stranger.Id}/attendance",
                     $"/api/tutor/students/{stranger.Id}/diaries"
                 })
        {
            (await s.Client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound, "{0} — mavjudligi oshkor qilinmaydi", url);
        }

        (await s.Client.GetAsync($"/api/tutor/students/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var studentClient = await Factory.LoginAsStudentAsync(s.Student);
        (await studentClient.GetAsync($"/api/tutor/students/{s.Student.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Davomat_KunBakun_HolatKoordinataRasmVaUrinishlar()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var today = Factory.Today();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        days.Should().HaveCount(3);
        var dayOff = FirstNonWorkDay(s.Period, today);

        var photo = await Factory.CreateStoredFileAsync(
            s.Student.Id, StoredFileKind.CheckInPhoto, "selfie.jpg", "image/jpeg", Encoding.UTF8.GetBytes("jpeg-bytes"));

        // 1-kun: qabul qilingan check-in + selfie + kundalik.
        await AddAttendanceWithPhotoAsync(s.Student, s.Period, days[0], photo.Id);
        await Factory.AddCheckInEventAsync(
            s.Student, s.Company, days[0], distanceM: 25, accepted: true, lat: 41.3120, lng: 69.2800, at: new TimeOnly(8, 55));
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 4);

        // 2-kun: radius tashqarisidagi check-in + bitta rad etilgan urinish.
        await Factory.AddAttendanceAsync(s.Student, s.Period, days[1], distanceM: 900);
        await Factory.AddCheckInEventAsync(
            s.Student, s.Company, days[1], distanceM: 900, accepted: true, lat: 41.2900, lng: 69.2500, at: new TimeOnly(8, 55));
        await Factory.AddCheckInEventAsync(
            s.Student, s.Company, days[1], distanceM: 3400, accepted: false, lat: 41.2800, lng: 69.2200, at: new TimeOnly(9, 5));

        // 3-kun: hech narsa yo'q → kelmadi.

        fixture.Clock.Set(PracticeTime.At(today, new TimeOnly(12, 0)));
        try
        {
            var response = await s.Client.GetAsync($"/api/tutor/students/{s.Student.Id}/attendance");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = (await response.Content.ReadAsync<List<StudentAttendanceDay>>())!;

            body.Should().NotBeEmpty();
            body.Select(d => d.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
            body.First().Date.Should().Be(s.Period.StartDate, "berilmagan 'from' — davr boshlanishi");
            body.Last().Date.Should().Be(today, "berilmagan 'to' — bugun (davr oxiridan oldin)");

            var first = body.Single(d => d.Date == days[0]);
            first.Status.Should().Be(AttendanceStatus.Present);
            first.IsWorkDay.Should().BeTrue();
            first.CheckIn.Should().NotBeNull();
            first.CheckIn!.At.Should().Be("08:55");
            first.CheckIn.AtIso.Should().Be(PracticeTime.At(days[0], new TimeOnly(8, 55)));
            first.CheckIn.DistanceM.Should().Be(25);
            first.CheckIn.AccuracyM.Should().Be(12);
            first.CheckIn.Lat.Should().BeApproximately(41.3120, 0.0001);
            first.CheckIn.Lng.Should().BeApproximately(69.2800, 0.0001);
            first.CheckIn.PhotoUrl.Should().Be($"/api/files/{photo.Id}");
            first.CheckIn.OutOfRadius.Should().BeFalse();
            first.CheckOut.Should().NotBeNull();
            first.CheckOut!.At.Should().Be("17:05");
            first.AutoClosed.Should().BeFalse();
            first.Attempts.Should().Be(1);
            first.RejectedAttempts.Should().Be(0);
            first.Diary.Should().NotBeNull();
            first.Diary!.Score.Should().Be(4);

            var second = body.Single(d => d.Date == days[1]);
            second.CheckIn.Should().NotBeNull();
            second.CheckIn!.OutOfRadius.Should().BeTrue("900 m > radius 150 m");
            second.CheckIn.DistanceM.Should().Be(900);
            second.CheckIn.Lat.Should().BeApproximately(41.2900, 0.0001);
            second.CheckIn.PhotoUrl.Should().BeNull();
            second.Attempts.Should().Be(2);
            second.RejectedAttempts.Should().Be(1);
            second.Diary.Should().BeNull();

            var third = body.Single(d => d.Date == days[2]);
            third.Status.Should().Be(AttendanceStatus.Absent, "o'tgan ish kuni, qatori yo'q");
            third.IsWorkDay.Should().BeTrue();
            third.CheckIn.Should().BeNull();
            third.Attempts.Should().Be(0);

            var off = body.Single(d => d.Date == dayOff);
            off.Status.Should().Be(AttendanceStatus.DayOff);
            off.IsWorkDay.Should().BeFalse();

            // Oraliq filtri.
            var range = (await (await s.Client.GetAsync(
                    $"/api/tutor/students/{s.Student.Id}/attendance?from={Iso(days[2])}&to={Iso(days[1])}"))
                .Content.ReadAsync<List<StudentAttendanceDay>>())!;
            range.Should().HaveCount(days[1].DayNumber - days[2].DayNumber + 1);
            range.Select(d => d.Date).Should().Contain(new[] { days[2], days[1] }).And.NotContain(days[0]);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Davomat_NotogriOraliq_400()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var url = $"/api/tutor/students/{s.Student.Id}/attendance";

        (await s.Client.GetAsync($"{url}?from=2026-05-10&to=2026-05-01")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "from > to");
        (await s.Client.GetAsync($"{url}?from=2024-01-01&to=2026-05-01")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "oraliq 400 kundan uzun");
        (await s.Client.GetAsync($"{url}?from=2000-01-01")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "bugungacha 400 kundan uzun");
        (await s.Client.GetAsync($"{url}?from=kecha")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "sana formati noto'g'ri");
    }

    [Fact]
    public async Task Kundaliklar_SanaBoyichaKamayish_FaqatOzTalabasi()
    {
        var s = await Factory.CreateTutorScenarioAsync();
        var days = await Factory.PastWorkDaysAsync(s.Period, 3);
        var other = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[2]);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[0], s.Tutor.Id, score: 5);
        await Factory.AddDiaryAsync(s.Student, s.Period, days[1]);
        await Factory.AddDiaryAsync(other, s.Period, days[0]);

        var response = await s.Client.GetAsync($"/api/tutor/students/{s.Student.Id}/diaries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = (await response.Content.ReadAsync<List<TutorDiaryEntry>>())!;
        entries.Should().HaveCount(3);
        entries.Should().OnlyContain(e => e.StudentId == s.Student.Id);
        entries.Select(e => e.Date).Should().Equal(days[0], days[1], days[2]);
        entries[0].Score.Should().Be(5);
        entries[0].StudentName.Should().Be(s.Student.FullName);
        entries[0].Group.Should().Be(s.Group.GroupName);
        entries[0].Text.Should().NotBeNullOrEmpty();
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Davr ichidagi eng yaqin ish kuni bo'lmagan sana (Du–Sha davrida — yakshanba).</summary>
    private static DateOnly FirstNonWorkDay(PracticePeriod period, DateOnly today)
    {
        for (var date = today; date >= period.StartDate; date = date.AddDays(-1))
        {
            if (!period.WorkDays.Includes(date))
                return date;
        }

        throw new InvalidOperationException("Davrda ish kuni bo'lmagan sana topilmadi.");
    }

    /// <summary>Selfie bilan qabul qilingan check-in + check-out (yordamchi <c>AddAttendanceAsync</c> rasmni bilmaydi).</summary>
    private Task<DailyAttendance> AddAttendanceWithPhotoAsync(
        TestUser student, PracticePeriod period, DateOnly date, Guid photoFileId) =>
        Factory.WithDbAsync(async db =>
        {
            var attendance = DailyAttendance.CheckIn(
                student.Id, period.Id, date, PracticeTime.At(date, new TimeOnly(8, 55)), 25, 12,
                CheckInVerdict.Accept(), photoFileId);
            attendance.CheckOut(PracticeTime.At(date, new TimeOnly(17, 5)), 25);
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        });
}
