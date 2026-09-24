using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.PeriodDays;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET /api/student/period-days?periodId=</c>: davrning har bir kuni, holat talaba kalendari bilan bir xil,
/// check-in/out vaqtlari, kundalik, davr tanlash, ruxsatlar. Davomat DB seed bilan yaratiladi (QR/selfi talablarisiz).</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentPeriodDaysTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task SukutDavr_HarKun_HolatlarVaqtlarKundalik()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var holidays = await Factory.WithDbAsync(db => db.Holidays.AsNoTracking().ToListAsync());
        var past = Enumerable.Range(1, 13).Select(i => day.AddDays(-i))
            .Where(d => !holidays.Any(h => h.AppliesTo(d))).Take(5).ToList();
        var (lateDay, presentDay, manualDay, absentDay, excusedDay) = (past[0], past[1], past[2], past[3], past[4]);

        var diaryId = await Factory.WithDbAsync(async db =>
        {
            var late = DailyAttendance.CheckIn(scene.Student.Id, scene.Period.Id, lateDay,
                PracticeTime.At(lateDay, new TimeOnly(9, 20)), 20, 10, CheckInVerdict.Accept(true));
            late.CheckOut(PracticeTime.At(lateDay, new TimeOnly(17, 2)), 15);
            var present = DailyAttendance.CheckIn(scene.Student.Id, scene.Period.Id, presentDay,
                PracticeTime.At(presentDay, new TimeOnly(9, 5)), 20, 10, CheckInVerdict.Accept(false));
            present.MarkSuspicious("Joylashuv shubhali");
            present.AutoClose(PracticeTime.At(presentDay, new TimeOnly(18, 0)));
            var manual = DailyAttendance.Manual(scene.Student.Id, scene.Period.Id, manualDay, AttendanceStatus.Present,
                scene.Tutor.Id, "Telefon buzilgan", PracticeTime.At(manualDay, new TimeOnly(9, 0)));
            db.DailyAttendances.AddRange(late, present, manual);

            var leave = LeaveRequest.Create(scene.Student.Id, scene.Period.Id, excusedDay, excusedDay, "Shifokor ko'rigi, ma'lumotnoma bor");
            leave.Approve(scene.Tutor.Id, null, PracticeTime.At(excusedDay, new TimeOnly(8, 0)));
            db.LeaveRequests.Add(leave);

            var diary = DiaryEntry.Create(scene.Student.Id, scene.Period.Id, lateDay, StudentTestData.LongText(), null,
                PracticeTime.At(lateDay, new TimeOnly(18, 0)));
            diary.Approve(scene.Tutor.Id, 5, null, PracticeTime.At(day, new TimeOnly(8, 0)));
            db.DiaryEntries.Add(diary);
            await db.SaveChangesAsync();
            return diary.Id;
        });

        StudentPeriodDaysDto dto;
        CalendarMonthDto calendar;
        string body;
        try
        {
            fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(9, 5)));
            var response = await scene.Client.GetAsync("/api/student/period-days");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body = await response.Content.ReadAsStringAsync();
            dto = (await response.Content.ReadAsync<StudentPeriodDaysDto>())!;
            calendar = (await (await scene.Client.GetAsync($"/api/student/calendar?month={day:yyyy-MM}"))
                .Content.ReadAsync<CalendarMonthDto>())!;
        }
        finally
        {
            fixture.Clock.Reset();
        }

        dto.Today.Should().Be(day);
        dto.Period!.Id.Should().Be(scene.Period.Id);
        dto.Period.Status.Should().Be(PracticePeriodStatus.Active);
        dto.Period.StartDate.Should().Be(scene.Period.StartDate);
        dto.Period.EndDate.Should().Be(scene.Period.EndDate);
        dto.Period.RequiredDays.Should().Be(scene.Period.RequiredDays);
        var expectedElapsed = Enumerable.Range(0, day.DayNumber - scene.Period.StartDate.DayNumber)
            .Select(i => scene.Period.StartDate.AddDays(i))
            .Count(d => !holidays.Any(h => h.AppliesTo(d))) - 1; // −1: sababli kun maxrajdan chiqadi
        dto.Period.ElapsedWorkDays.Should().Be(expectedElapsed);

        dto.Periods.Should().ContainSingle(p => p.Id == scene.Period.Id && p.IsDefault);

        var totalDays = scene.Period.EndDate.DayNumber - scene.Period.StartDate.DayNumber + 1;
        dto.Days.Should().HaveCount(totalDays);
        dto.Days.Select(d => d.Date).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        dto.Days[0].Date.Should().Be(scene.Period.StartDate);
        dto.Days[^1].Date.Should().Be(scene.Period.EndDate);

        var byDate = dto.Days.ToDictionary(d => d.Date);
        byDate[day].Status.Should().Be(CalendarDayStatus.Pending, "bugun oyna ochiq, hali belgilanmagan");

        byDate[lateDay].Status.Should().Be(CalendarDayStatus.Late);
        byDate[lateDay].CheckInAt.Should().Be("09:20");
        byDate[lateDay].CheckOutAt.Should().Be("17:02");
        byDate[lateDay].Diary.Should().Be(new StudentPeriodDayDiaryDto(diaryId, DiaryStatus.Approved, 5));

        byDate[presentDay].Status.Should().Be(CalendarDayStatus.Present);
        byDate[presentDay].CheckInAt.Should().Be("09:05");
        byDate[presentDay].CheckOutAt.Should().Be("18:00");
        byDate[presentDay].AutoClosed.Should().BeTrue();
        byDate[presentDay].Suspicious.Should().BeTrue();
        byDate[presentDay].Manual.Should().BeFalse();
        byDate[presentDay].Diary.Should().BeNull();

        byDate[manualDay].Status.Should().Be(CalendarDayStatus.Present);
        byDate[manualDay].Manual.Should().BeTrue();

        byDate[absentDay].Status.Should().Be(CalendarDayStatus.Absent);
        byDate[absentDay].CheckInAt.Should().BeNull();
        byDate[excusedDay].Status.Should().Be(CalendarDayStatus.Excused);

        dto.Days.Where(d => d.Date > day).Should().OnlyContain(d =>
            d.Status == (d.IsWorkDay ? CalendarDayStatus.Future : CalendarDayStatus.DayOff));
        dto.Days.Should().OnlyContain(d => d.Weekday == (d.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.Date.DayOfWeek));

        // Talaba kalendari bilan AYNAN bir xil holat (shu oy kunlari uchun).
        foreach (var cell in calendar.Days.Where(c => byDate.ContainsKey(c.Date)))
            byDate[cell.Date].Status.Should().Be(cell.Status, $"{cell.Date} — kalendar bilan bir xil bo'lishi kerak");

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("today").GetString().Should().Be(day.ToString("yyyy-MM-dd"));
        root.GetProperty("period").GetProperty("status").GetString().Should().Be("active");
        var lateJson = root.GetProperty("days").EnumerateArray()
            .Single(d => d.GetProperty("date").GetString() == lateDay.ToString("yyyy-MM-dd"));
        lateJson.GetProperty("status").GetString().Should().Be("late");
        lateJson.GetProperty("checkInAt").GetString().Should().Be("09:20");
        lateJson.GetProperty("isWorkDay").GetBoolean().Should().BeTrue();
        lateJson.GetProperty("holiday").ValueKind.Should().Be(JsonValueKind.Null);
        lateJson.GetProperty("diary").GetProperty("status").GetString().Should().Be("approved");
        lateJson.GetProperty("diary").GetProperty("score").GetInt32().Should().Be(5);
        root.GetProperty("days").EnumerateArray()
            .Single(d => d.GetProperty("date").GetString() == day.ToString("yyyy-MM-dd"))
            .GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task PeriodId_BoshqaDavr_DamOlishVaBayram()
    {
        var day = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(day);
        var holiday = await Factory.WithDbAsync(db => db.Holidays.AsNoTracking().OrderBy(h => h.Date).FirstAsync());
        var holidayDate = new DateOnly(day.Year - 1, holiday.Date.Month, holiday.Date.Day);

        // O'tgan yilgi davr (bayram atrofida, faqat Du–Ju) — talaba guruhiga biriktirilgan.
        var old = await Factory.WithDbAsync(async db =>
        {
            var period = PracticePeriod.Create(
                $"Eski {Guid.NewGuid():N}"[..16], scene.Group.AcademicYearId, holidayDate.AddDays(-7), holidayDate.AddDays(7),
                scene.Tutor.Id, CheckInRules.Default, WorkDays.MondayToFriday, 10, dailyReportRequired: true);
            period.AttachGroup(scene.Group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

        var response = await scene.Client.GetAsync($"/api/student/period-days?periodId={old.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadAsync<StudentPeriodDaysDto>())!;
        dto.Period!.Id.Should().Be(old.Id);
        dto.Days.Should().HaveCount(15);
        dto.Periods.Select(p => p.Id).Should().Contain([scene.Period.Id, old.Id]);
        dto.Periods.Select(p => p.StartDate).Should().BeInDescendingOrder();
        dto.Periods.Single(p => p.Id == scene.Period.Id).IsDefault.Should().BeTrue();
        dto.Periods.Single(p => p.Id == old.Id).IsDefault.Should().BeFalse();

        var h = dto.Days.Single(d => d.Date == holidayDate);
        h.Holiday.Should().Be(holiday.Name);
        h.IsWorkDay.Should().BeFalse();
        h.Status.Should().Be(CalendarDayStatus.DayOff);

        dto.Days.Where(d => d.Weekday >= 6).Should().NotBeEmpty()
            .And.OnlyContain(d => !d.IsWorkDay && d.Status == CalendarDayStatus.DayOff);
        dto.Days.Where(d => d.IsWorkDay).Should().NotBeEmpty()
            .And.OnlyContain(d => d.Status == CalendarDayStatus.Absent && d.Holiday == null, "o'tgan ish kunlari, yozuv yo'q");
        dto.Period.ElapsedWorkDays.Should().Be(dto.Days.Count(d => d.IsWorkDay));

        // periodId'siz — sukut (today ko'rsatadigan) davr.
        var fallback = (await (await scene.Client.GetAsync("/api/student/period-days")).Content.ReadAsync<StudentPeriodDaysDto>())!;
        fallback.Period!.Id.Should().Be(scene.Period.Id);
    }

    [Fact]
    public async Task BegonaPeriodId_404()
    {
        var scene = await Factory.CreateSceneAsync();
        var otherGroup = await Factory.CreateGroupAsync();
        var foreign = await Factory.CreatePeriodAtAsync(otherGroup, scene.Tutor.Id, Factory.LocalToday());

        (await scene.Client.GetAsync($"/api/student/period-days?periodId={foreign.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scene.Client.GetAsync($"/api/student/period-days?periodId={Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DavrsizTalaba_200_Bosh()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var response = await client.GetAsync("/api/student/period-days");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("period").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("periods").GetArrayLength().Should().Be(0);
        json.RootElement.GetProperty("days").GetArrayLength().Should().Be(0);
        json.RootElement.GetProperty("today").GetString().Should().Be(Factory.LocalToday().ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task TyutorVaAdmin_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/period-days")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetAsync("/api/student/period-days")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
