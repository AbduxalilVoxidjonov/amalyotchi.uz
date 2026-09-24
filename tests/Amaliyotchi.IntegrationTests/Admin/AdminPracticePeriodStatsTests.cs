using System.Net;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Tutor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary><c>/api/admin/practice-periods/{id}/stats</c> va <c>/{id}/groups/{groupId}/students</c>: faqat shu davr
/// yozuvlari bo'yicha davomat/ariza/kundalik/baho ko'rsatkichlari, bo'sh guruh, 404/403.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminPracticePeriodStatsTests(ApiFixture fixture)
{
    private const string Url = "/api/admin/practice-periods";

    private ApiFactory Factory => fixture.Factory;

    /// <summary>Davrni to'g'ridan-to'g'ri bazaga yozadi (sanalar erkin — o'tmishdagi davr ham).</summary>
    private Task<PracticePeriod> CreatePeriodAsync(TestGroup yearSource, Guid adminId, DateOnly start, DateOnly end, params Guid[] groupIds)
        => Factory.WithDbAsync(async db =>
        {
            var period = PracticePeriod.Create(
                $"Stat {Guid.NewGuid():N}"[..13], yearSource.AcademicYearId, start, end, adminId,
                CheckInRules.Default, WorkDays.MondayToSaturday, 20, dailyReportRequired: true);
            foreach (var groupId in groupIds)
                period.AttachGroup(groupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    private async Task<List<DateOnly>> WorkDaysAsync(PracticePeriod period)
    {
        var holidays = await Factory.LoadHolidaysAsync();
        var days = new List<DateOnly>();
        for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
        {
            if (period.IsWorkDay(date, holidays))
                days.Add(date);
        }

        return days;
    }

    [Fact]
    public async Task Stats_VaTalabalar_FaqatShuDavr_MetrikalarTogri()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);
        var today = Factory.Today();
        var group = await Factory.CreateGroupAsync(course: 4);
        var empty = await Factory.CreateGroupAsync();

        // Butunlay o'tgan davr — bugungi check-in oynasi natijaga ta'sir qilmaydi.
        var period = await CreatePeriodAsync(group, admin.Id, today.AddDays(-24), today.AddDays(-1), group.GroupId, empty.GroupId);
        var other = await CreatePeriodAsync(group, admin.Id, today.AddDays(-80), today.AddDays(-60), group.GroupId);

        var a = await Factory.CreateStudentAsync(group: group, fullName: "Aaa Statov");
        var b = await Factory.CreateStudentAsync(group: group, fullName: "Bbb Statov");
        var suspended = await Factory.CreateStudentAsync(group: group, fullName: "Ccc Statov");
        await Factory.WithDbAsync(async db =>
        {
            (await db.StudentProfiles.SingleAsync(p => p.UserId == suspended.Id)).Suspend();
            await db.SaveChangesAsync();
        });

        var days = await WorkDaysAsync(period);
        days.Count.Should().BeGreaterThan(5);
        var otherDays = await WorkDaysAsync(other);

        // A: 2 keldi (+1 kech), 1 shubhali, 1 sababli (tasdiqlangan ruxsat), qolgani kelmagan.
        await Factory.AddAttendanceAsync(a, period, days[0]);
        await Factory.AddAttendanceAsync(a, period, days[1], late: true);
        await Factory.AddAttendanceAsync(a, period, days[2], suspiciousReason: "Radius chetida");
        var leave = await Factory.AddLeaveRequestAsync(a, period, days[3], days[3]);
        await Factory.WithDbAsync(async db =>
        {
            (await db.LeaveRequests.SingleAsync(l => l.Id == leave.Id)).Approve(admin.Id, null, Factory.Now());
            await db.SaveChangesAsync();
        });
        await Factory.AddDiaryAsync(a, period, days[0], admin.Id, score: 4);
        await Factory.AddDiaryAsync(a, period, days[1]);
        await Factory.WithDbAsync(async db =>
        {
            var grade = PracticeGrade.Create(a.Id, period.Id);
            grade.SetTutorPoints(15);
            grade.SetReferencePoints(8);
            grade.Finalize("Yaxshi ishladi", admin.Id, Factory.Now());
            db.PracticeGrades.Add(grade);
            await db.SaveChangesAsync();
        });
        var company = await Factory.CreateCompanyAsync(name: $"Stat korxona {Guid.NewGuid():N}"[..24]);
        await Factory.CreateApprovedApplicationAsync(a, period, company, admin.Id);

        // B: faqat ko'rib chiqilayotgan ariza.
        await Factory.CreateSubmittedApplicationAsync(b, period, company);

        // Boshqa davr yozuvlari — aralashmasligi kerak.
        await Factory.AddAttendanceAsync(a, other, otherDays[0], suspiciousReason: "Boshqa davr");
        await Factory.AddAttendanceAsync(b, other, otherDays[0]);
        await Factory.AddDiaryAsync(a, other, otherDays[0], admin.Id, score: 5);
        await Factory.AddDiaryAsync(b, other, otherDays[1], admin.Id, score: 5);
        await Factory.CreateApprovedApplicationAsync(b, other, await Factory.CreateCompanyAsync(), admin.Id);
        await Factory.WithDbAsync(async db =>
        {
            var grade = PracticeGrade.Create(b.Id, other.Id);
            grade.SetTutorPoints(20);
            grade.Finalize("Boshqa davr", admin.Id, Factory.Now());
            db.PracticeGrades.Add(grade);
            await db.SaveChangesAsync();
        });

        // Kutilgan qiymatlar — tyutor baholash jadvali bilan bir xil formula.
        var elapsed = days.Count;
        var totalDays = elapsed - 1;
        var pctA = Math.Round(3 * 100d / totalDays, 1, MidpointRounding.AwayFromZero);
        var resultA = GradeCalculator.Compute(pctA, 1, 4.0, 15, 8);
        var resultB = GradeCalculator.Compute(0, 0, 0, null, null);

        // ---- /stats ----
        var statsResponse = await client.GetAsync($"{Url}/{period.Id}/stats");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.OK, await statsResponse.Content.ReadAsStringAsync());
        var stats = (await statsResponse.Content.ReadAsync<PracticePeriodStats>())!;

        stats.PeriodId.Should().Be(period.Id);
        stats.ElapsedWorkDays.Should().Be(elapsed);
        stats.RequiredDays.Should().Be(20);
        stats.Groups.Select(g => g.GroupId).Should().BeEquivalentTo([group.GroupId, empty.GroupId]);
        stats.Groups.Select(g => g.Code).Should().BeInAscendingOrder(StringComparer.Ordinal);

        var g1 = stats.Groups.Single(g => g.GroupId == group.GroupId);
        g1.Code.Should().Be(group.GroupName);
        g1.Course.Should().Be(4);
        g1.DirectionName.Should().NotBeNullOrEmpty();
        g1.StudentsCount.Should().Be(2);
        g1.AttendancePct.Should().Be((int)Math.Round(pctA / 2, MidpointRounding.AwayFromZero));
        g1.LowAttendanceCount.Should().Be(1 + (pctA < GradeThresholds.MinAttendancePct ? 1 : 0));
        g1.SuspiciousDays.Should().Be(1);
        g1.WithCompanyCount.Should().Be(1);
        g1.PendingApplicationsCount.Should().Be(1);
        g1.DiaryCount.Should().Be(2);
        g1.DiaryApprovedCount.Should().Be(1);
        g1.DiaryAvgScore.Should().Be(4.0);
        g1.AvgTotal.Should().Be(Math.Round((resultA.Total + resultB.Total) / 2, 1, MidpointRounding.AwayFromZero));
        g1.FinalizedCount.Should().Be(1);
        var grades = new[] { resultA.Grade, resultB.Grade };
        g1.Grades.Should().Be(new GradeDistribution(
            grades.Count(x => x == 5), grades.Count(x => x == 4), grades.Count(x => x == 3),
            grades.Count(x => x == 2), grades.Count(x => x is null)));

        var g2 = stats.Groups.Single(g => g.GroupId == empty.GroupId);
        g2.StudentsCount.Should().Be(0);
        g2.AttendancePct.Should().Be(0);
        g2.AvgTotal.Should().BeNull();
        g2.DiaryAvgScore.Should().Be(0);
        g2.Grades.Should().Be(GradeDistribution.Empty);

        // Jami = barcha guruhlar talabalari (bo'sh guruh ta'sir qilmaydi).
        stats.Totals.Should().BeEquivalentTo(new GroupMetrics(
            g1.StudentsCount, g1.AttendancePct, g1.LowAttendanceCount, g1.SuspiciousDays, g1.WithCompanyCount,
            g1.PendingApplicationsCount, g1.DiaryCount, g1.DiaryApprovedCount, g1.DiaryAvgScore, g1.AvgTotal,
            g1.FinalizedCount, g1.Grades));

        // JSON shakli: guruh qatori bir tekis, baholar obyekt.
        var raw = await (await client.GetAsync($"{Url}/{period.Id}/stats")).Content.ReadAsStringAsync();
        raw.Should().Contain("\"elapsedWorkDays\":").And.Contain("\"totals\":{").And.Contain("\"retake\":")
            .And.Contain($"\"groupId\":\"{group.GroupId}\",\"code\":");

        // ---- /groups/{groupId}/students ----
        var studentsResponse = await client.GetAsync($"{Url}/{period.Id}/groups/{group.GroupId}/students");
        studentsResponse.StatusCode.Should().Be(HttpStatusCode.OK, await studentsResponse.Content.ReadAsStringAsync());
        var body = (await studentsResponse.Content.ReadAsync<PeriodGroupStudents>())!;

        body.Period.Id.Should().Be(period.Id);
        body.Period.Name.Should().Be(period.Name);
        body.Period.Status.Should().Be(PracticePeriodStatus.Active);
        body.Period.StartDate.Should().Be(period.StartDate);
        body.Period.EndDate.Should().Be(period.EndDate);
        body.Group.Id.Should().Be(group.GroupId);
        body.Group.Code.Should().Be(group.GroupName);
        body.Group.Course.Should().Be(4);
        body.Group.DirectionName.Should().Be(g1.DirectionName);
        body.Group.FacultyName.Should().NotBeNullOrEmpty();
        body.ElapsedWorkDays.Should().Be(elapsed);
        body.Metrics.Should().BeEquivalentTo(stats.Totals);
        body.Students.Select(s => s.FullName).Should().Equal("Aaa Statov", "Bbb Statov");

        var rowA = body.Students[0];
        rowA.Id.Should().Be(a.Id);
        rowA.HemisId.Should().NotBeNullOrEmpty();
        rowA.Company.Should().Be(company.Name);
        rowA.ApplicationStatus.Should().Be(ApplicationStatus.Approved);
        rowA.AttendancePct.Should().Be(pctA);
        rowA.PresentDays.Should().Be(2);
        rowA.LateDays.Should().Be(1);
        rowA.ExcusedDays.Should().Be(1);
        rowA.AbsentDays.Should().Be(totalDays - 3);
        rowA.SuspiciousDays.Should().Be(1);
        rowA.DiaryCount.Should().Be(2);
        rowA.DiaryAvg.Should().Be(4.0);
        rowA.AttendancePoints.Should().Be(resultA.AttendancePoints);
        rowA.ReportPoints.Should().Be(resultA.ReportPoints);
        rowA.TutorPoints.Should().Be(15);
        rowA.ReferencePoints.Should().Be(8);
        rowA.Total.Should().Be(resultA.Total);
        rowA.Grade.Should().Be(resultA.Grade);
        rowA.Finalized.Should().BeTrue();

        var rowB = body.Students[1];
        rowB.Company.Should().BeNull();
        rowB.ApplicationStatus.Should().Be(ApplicationStatus.Submitted);
        rowB.AttendancePct.Should().Be(0);
        rowB.PresentDays.Should().Be(0);
        rowB.AbsentDays.Should().Be(elapsed);
        rowB.DiaryCount.Should().Be(0);
        rowB.TutorPoints.Should().BeNull();
        rowB.ReferencePoints.Should().BeNull();
        rowB.Total.Should().Be(0);
        rowB.Grade.Should().BeNull();
        rowB.Finalized.Should().BeFalse();

        var rawStudents = await (await client.GetAsync($"{Url}/{period.Id}/groups/{group.GroupId}/students")).Content.ReadAsStringAsync();
        rawStudents.Should().Contain("\"applicationStatus\":\"submitted\"").And.Contain("\"status\":\"active\"")
            .And.Contain("\"tutorPoints\":null");

        // Bo'sh guruh — 200, talabalar yo'q.
        var emptyBody = (await (await client.GetAsync($"{Url}/{period.Id}/groups/{empty.GroupId}/students"))
            .Content.ReadAsync<PeriodGroupStudents>())!;
        emptyBody.Students.Should().BeEmpty();
        emptyBody.Metrics.StudentsCount.Should().Be(0);
        emptyBody.Metrics.AvgTotal.Should().BeNull();

        // Boshqa davr — o'z yozuvlari bilan (B tasdiqlangan, A ning boshqa davrdagi shubhali kuni).
        var otherStats = (await (await client.GetAsync($"{Url}/{other.Id}/stats")).Content.ReadAsync<PracticePeriodStats>())!;
        otherStats.Totals.WithCompanyCount.Should().Be(1);
        otherStats.Totals.PendingApplicationsCount.Should().Be(0);
        otherStats.Totals.SuspiciousDays.Should().Be(1);
        otherStats.Totals.DiaryCount.Should().Be(2);
        otherStats.Totals.DiaryAvgScore.Should().Be(5.0);
        otherStats.Totals.FinalizedCount.Should().Be(1);
    }

    [Fact]
    public async Task BoshlanmaganDavr_ElapsedNol_PastDavomatHisoblanmaydi()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);
        var today = Factory.Today();
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateStudentAsync(group: group);
        var period = await CreatePeriodAsync(group, admin.Id, today.AddDays(10), today.AddDays(30), group.GroupId);

        var stats = (await (await client.GetAsync($"{Url}/{period.Id}/stats")).Content.ReadAsync<PracticePeriodStats>())!;

        stats.ElapsedWorkDays.Should().Be(0);
        stats.Totals.StudentsCount.Should().Be(1);
        stats.Totals.LowAttendanceCount.Should().Be(0);
        stats.Totals.AttendancePct.Should().Be(0);

        var body = (await (await client.GetAsync($"{Url}/{period.Id}/groups/{group.GroupId}/students"))
            .Content.ReadAsync<PeriodGroupStudents>())!;
        body.Period.Status.Should().Be(PracticePeriodStatus.Planned);
        body.Students.Should().ContainSingle();
    }

    [Fact]
    public async Task TopilmadiVaBiriktirilmaganGuruh_404()
    {
        var admin = await Factory.CreateAdminAsync();
        var client = await Factory.LoginAsync(admin);
        var today = Factory.Today();
        var group = await Factory.CreateGroupAsync();
        var foreign = await Factory.CreateGroupAsync();
        var period = await CreatePeriodAsync(group, admin.Id, today.AddDays(5), today.AddDays(15), group.GroupId);

        (await client.GetAsync($"{Url}/{Guid.NewGuid()}/stats")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Url}/{Guid.NewGuid()}/groups/{group.GroupId}/students")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var notAttached = await client.GetAsync($"{Url}/{period.Id}/groups/{foreign.GroupId}/students");
        notAttached.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await notAttached.Content.ReadAsStringAsync()).Should().Contain("biriktirilmagan");
        (await client.GetAsync($"{Url}/{period.Id}/groups/{Guid.NewGuid()}/students")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // O'chirilgan davr → 404.
        (await client.DeleteAsync($"{Url}/{period.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{Url}/{period.Id}/stats")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Url}/{period.Id}/groups/{group.GroupId}/students")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Auth_AdminBolmagan403_Anonim401()
    {
        var group = await Factory.CreateGroupAsync();
        var admin = await Factory.CreateAdminAsync();
        var period = await Factory.CreateActivePeriodAsync(group, admin.Id);
        var tutor = await Factory.LoginAsTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var studentClient = await Factory.LoginAsStudentAsync(student);

        var stats = $"{Url}/{period.Id}/stats";
        var students = $"{Url}/{period.Id}/groups/{group.GroupId}/students";

        (await tutor.GetAsync(stats)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync(students)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await studentClient.GetAsync(stats)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync(students)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
