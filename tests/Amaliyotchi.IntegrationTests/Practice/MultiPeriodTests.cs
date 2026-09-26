using System.Net;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Groups;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.IntegrationTests.Admin;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Amaliyotchi.IntegrationTests.Student;
using Amaliyotchi.IntegrationTests.Tutor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Practice;

/// <summary>Bir guruhda bir o'quv yilida ikki davr: kuzgi davr tugagan va yopilgan (davomat, kundalik, baho bor),
/// bahorgi davr rejalashtirilgan. "Bugun" — ikki davr oralig'i (haqiqiy sana; kuzgi −70..−20, bahorgi +20..+60 kun).
/// Qoida — <see cref="PeriodSelection"/>: statistika/profil sukut bo'yicha oxirgi tugagan davrda qoladi, kelajakdagi bo'sh
/// davrga o'tib ketmaydi; check-in faqat davom etayotgan davrda; ariza — davom etayotgan yoki kelgusi davrga.</summary>
[Collection(ApiCollection.Name)]
public sealed class MultiPeriodTests(ApiFixture fixture)
{
    private const int AttendedDays = 3;

    private ApiFactory Factory => fixture.Factory;

    private sealed record Scene(
        TestGroup Group,
        TestUser Tutor,
        TestUser Student,
        Company Company,
        PracticePeriod Autumn,
        PracticePeriod Spring,
        HttpClient Admin,
        HttpClient TutorClient,
        HttpClient StudentClient);

    private async Task<Scene> CreateSceneAsync()
    {
        var today = Factory.LocalToday();
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var company = await Factory.CreateCompanyAsync(StudentTestData.CompanyLat, StudentTestData.CompanyLng);

        var autumn = await CreatePeriodAsync(group, tutor.Id, "Kuzgi", today.AddDays(-70), today.AddDays(-20));
        var spring = await CreatePeriodAsync(group, tutor.Id, "Bahorgi", today.AddDays(20), today.AddDays(60));

        await Factory.CreateApprovedApplicationAsync(student, autumn, company, tutor.Id);
        for (var i = 1; i <= AttendedDays; i++)
            await Factory.AddAttendanceAsync(student, autumn, autumn.StartDate.AddDays(i));
        await Factory.AddDiaryAsync(student, autumn, autumn.StartDate.AddDays(1), tutor.Id, score: 5);
        await Factory.WithDbAsync(async db =>
        {
            var grade = PracticeGrade.Create(student.Id, autumn.Id);
            grade.SetTutorPoints(15);
            db.PracticeGrades.Add(grade);

            // Kuzgi davr tugab, admin tomonidan yopilgan — tarix va statistika uchun baribir yuklanadi.
            var tracked = await db.PracticePeriods.SingleAsync(p => p.Id == autumn.Id);
            tracked.Close();
            await db.SaveChangesAsync();
        });

        var admin = await Factory.LoginAsAdminAsync();
        var tutorClient = await Factory.LoginAsync(tutor);
        var studentClient = await Factory.LoginAsStudentAsync(student);
        return new Scene(group, tutor, student, company, autumn, spring, admin, tutorClient, studentClient);
    }

    /// <summary>Admin API yaratgandek: saqlanadigan holat ochiq (Active), ko'rinadigani sanadan hisoblanadi.</summary>
    private Task<PracticePeriod> CreatePeriodAsync(TestGroup group, Guid createdBy, string name, DateOnly start, DateOnly end) =>
        Factory.WithDbAsync(async db =>
        {
            var period = PracticePeriod.Create(
                $"{name} {Guid.NewGuid():N}"[..20], group.AcademicYearId, start, end, createdBy, CheckInRules.Default,
                WorkDays.MondayToSaturday | WorkDays.Sunday, 30, dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    [Fact]
    public async Task A_Oraliqda_AdminVaTyutorProfili_SukutBoyicha_KuzgiDavr_IkkiDavrRoyxati()
    {
        var s = await CreateSceneAsync();

        var admin = await GetOkAsync<AdminStudentDetail>(s.Admin, $"/api/admin/students/{s.Student.Id}");
        admin.SelectedPeriodId.Should().Be(s.Autumn.Id);
        admin.Period!.Id.Should().Be(s.Autumn.Id);
        admin.Periods.Select(p => p.Id).Should().Equal(s.Spring.Id, s.Autumn.Id); // startDate kamayish tartibida
        admin.Periods.Single(p => p.IsDefault).Id.Should().Be(s.Autumn.Id);
        admin.Periods.Single(p => p.Id == s.Autumn.Id).Status.Should().Be(PracticePeriodStatus.Closed);
        admin.Periods.Single(p => p.Id == s.Spring.Id).Status.Should().Be(PracticePeriodStatus.Planned);
        admin.Attendance.AttendedDays.Should().Be(AttendedDays);
        admin.Diary.Count.Should().Be(1);
        admin.Company!.Id.Should().Be(s.Company.Id); // davrga bog'liq (tanlangan kuzgi davr tarixi)
        admin.ActiveCompany.Should().BeNull("davrlar oralig'ida hozir amaliyot o'tayotgan korxona yo'q");
        admin.Application.Should().NotBeNull();
        admin.Grade.Should().NotBeNull();

        var tutor = await GetOkAsync<TutorStudentDetail>(s.TutorClient, $"/api/tutor/students/{s.Student.Id}");
        tutor.SelectedPeriodId.Should().Be(s.Autumn.Id);
        tutor.Periods.Should().HaveCount(2);
        tutor.ActiveCompany.Should().BeNull();
        tutor.Attendance.AttendedDays.Should().Be(AttendedDays);

        // Alohida endpoint'lar ham sukut bo'yicha kuzgi davr.
        var days = await GetOkAsync<List<StudentAttendanceDay>>(s.TutorClient, $"/api/tutor/students/{s.Student.Id}/attendance");
        days.First().Date.Should().Be(s.Autumn.StartDate);
        days.Last().Date.Should().Be(s.Autumn.EndDate);
        days.Count(d => d.Status == AttendanceStatus.Present).Should().Be(AttendedDays);
        var diaries = await GetOkAsync<List<TutorDiaryEntry>>(s.Admin, $"/api/admin/students/{s.Student.Id}/diaries");
        diaries.Should().HaveCount(1);

        // Tyutor ro'yxati statistikasi kuzgi davr bo'yicha (kelgusi bo'sh davrga o'tib ketmaydi); korxona ustuni esa
        // faqat aktiv korxona — kuzgi davr yopilgan, bahorgi boshlanmagan → null (oldingi korxonaga fallback yo'q).
        var list = await GetOkAsync<List<TutorStudent>>(s.TutorClient, "/api/tutor/students");
        list.Single(r => r.Id == s.Student.Id).AttendedDays.Should().Be(AttendedDays);
        list.Single(r => r.Id == s.Student.Id).Company.Should().BeNull();
    }

    [Fact]
    public async Task B_BahorgiDavrTanlansa_BoshMalumot()
    {
        var s = await CreateSceneAsync();

        foreach (var url in new[]
                 {
                     $"/api/admin/students/{s.Student.Id}?periodId={s.Spring.Id}",
                     $"/api/tutor/students/{s.Student.Id}?periodId={s.Spring.Id}"
                 })
        {
            var client = url.StartsWith("/api/admin", StringComparison.Ordinal) ? s.Admin : s.TutorClient;
            var detail = await GetOkAsync<TutorStudentDetail>(client, url);
            detail.SelectedPeriodId.Should().Be(s.Spring.Id);
            detail.Period!.Id.Should().Be(s.Spring.Id);
            detail.Periods.Single(p => p.IsDefault).Id.Should().Be(s.Autumn.Id, "sukut o'zgarmaydi");
            detail.Attendance.TotalDays.Should().Be(0);
            detail.Attendance.AttendedDays.Should().Be(0);
            detail.Diary.Count.Should().Be(0);
            detail.Application.Should().BeNull();
            detail.Company.Should().BeNull();
            detail.Grade.Should().BeNull("davr hali boshlanmagan");
        }

        (await GetOkAsync<List<StudentAttendanceDay>>(s.Admin, $"/api/admin/students/{s.Student.Id}/attendance?periodId={s.Spring.Id}"))
            .Should().BeEmpty();
        (await GetOkAsync<List<TutorDiaryEntry>>(s.TutorClient, $"/api/tutor/students/{s.Student.Id}/diaries?periodId={s.Spring.Id}"))
            .Should().BeEmpty();

        // Oy navigatsiyasi davr chegarasidan chiqmaydi: kuzgi davr uchun so'ralgan kengroq oraliq qisiladi.
        var from = s.Autumn.StartDate.AddDays(-10).ToString("yyyy-MM-dd");
        var to = s.Autumn.StartDate.AddDays(5).ToString("yyyy-MM-dd");
        var clamped = await GetOkAsync<List<StudentAttendanceDay>>(
            s.Admin, $"/api/admin/students/{s.Student.Id}/attendance?periodId={s.Autumn.Id}&from={from}&to={to}");
        clamped.First().Date.Should().Be(s.Autumn.StartDate);
        clamped.Should().HaveCount(6);
    }

    [Fact]
    public async Task C_Oraliqda_Checkin_RadEtiladi_Today_DavrHaliBoshlanmagan()
    {
        var s = await CreateSceneAsync();

        var today = await GetOkAsync<TodayDto>(s.StudentClient, "/api/student/today");
        today.Period.Should().NotBeNull();
        today.Period!.Id.Should().Be(s.Spring.Id);
        today.Period.Status.Should().Be(PracticePeriodStatus.Planned);
        today.Window.IsOpen.Should().BeFalse();
        today.Checkin.Note.Should().Contain("hali boshlanmagan").And.Contain(s.Spring.Name)
            .And.Contain(s.Spring.StartDate.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture));

        // Selfi talabi o'chiriladi (JSON yo'li), QR yuboriladi — rad sababi aynan davr bo'lishi uchun.
        await using (await Factory.UseSettingAsync(SettingKeys.CheckInPhotoRequired, "false"))
        {
            var response = await s.StudentClient.PostJsonAsync(
                "/api/student/checkin", Factory.Geo(qr: s.Company.CheckInQrPayload));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("hali boshlanmagan");
        }

        var events = await Factory.WithDbAsync(db => db.AttendanceEvents.CountAsync(e => e.StudentUserId == s.Student.Id));
        events.Should().Be(0, "davr yo'q — urinish hodisasi yozilmaydi");
    }

    [Fact]
    public async Task D_Oraliqda_Talaba_BahorgiDavrga_Ariza_Topshiradi()
    {
        var s = await CreateSceneAsync();
        var springCompany = await Factory.CreateCompanyAsync(name: "Bahorgi korxona");

        var response = await s.StudentClient.PostJsonAsync("/api/student/place", new { tin = springCompany.Tin });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var place = (await response.Content.ReadAsync<PracticePlaceDto>())!;
        place.Status.Should().Be(ApplicationStatus.Submitted);
        place.Company.Should().Be("Bahorgi korxona");
        place.PeriodFrom.Should().Be(s.Spring.StartDate);

        var applications = await Factory.WithDbAsync(db =>
            db.PracticeApplications.Where(a => a.StudentUserId == s.Student.Id).ToListAsync());
        applications.Should().HaveCount(2);
        applications.Single(a => a.CompanyId == springCompany.Id).PeriodId.Should().Be(s.Spring.Id);
        applications.Single(a => a.CompanyId == s.Company.Id).PeriodId.Should().Be(s.Autumn.Id, "kuzgi ariza o'zgarmaydi");
    }

    [Fact]
    public async Task E_BahorgiBoshlangach_Sukut_Bahorgi_KuzgiMalumot_PeriodId_Bilan_Saqlanadi()
    {
        var s = await CreateSceneAsync();
        try
        {
            fixture.Clock.Set(PracticeTime.At(s.Spring.StartDate.AddDays(2), new TimeOnly(12, 0)));

            var detail = await GetOkAsync<AdminStudentDetail>(s.Admin, $"/api/admin/students/{s.Student.Id}");
            detail.SelectedPeriodId.Should().Be(s.Spring.Id);
            detail.Periods.Single(p => p.IsDefault).Id.Should().Be(s.Spring.Id);
            detail.Periods.Single(p => p.Id == s.Spring.Id).Status.Should().Be(PracticePeriodStatus.Active);
            detail.Attendance.AttendedDays.Should().Be(0);
            detail.Diary.Count.Should().Be(0);

            var autumn = await GetOkAsync<AdminStudentDetail>(s.Admin, $"/api/admin/students/{s.Student.Id}?periodId={s.Autumn.Id}");
            autumn.SelectedPeriodId.Should().Be(s.Autumn.Id);
            autumn.Attendance.AttendedDays.Should().Be(AttendedDays);
            autumn.Diary.Count.Should().Be(1);
            autumn.Company!.Id.Should().Be(s.Company.Id);
            autumn.Grade.Should().NotBeNull();

            var portfolio = await GetOkAsync<PortfolioDto>(s.StudentClient, $"/api/student/portfolio?periodId={s.Autumn.Id}");
            portfolio.PeriodId.Should().Be(s.Autumn.Id);
            portfolio.Stats.DaysPresent.Should().Be(AttendedDays);
            portfolio.Periods.Should().HaveCount(2);
            portfolio.Periods.Single(p => p.IsDefault).Id.Should().Be(s.Spring.Id);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task F_GuruhDavomatFoizi_Oraliqda_KuzgiDavrBoyicha()
    {
        var s = await CreateSceneAsync();
        var holidays = await Factory.LoadHolidaysAsync();
        var elapsed = PracticePeriod.CountWorkDays(
            s.Autumn.StartDate, s.Autumn.EndDate, s.Autumn.WorkDays, d => holidays.Any(h => h.AppliesTo(d)));

        var page = await s.Admin.GetPagedAsync<GroupRow>($"/api/admin/groups?q={s.Group.GroupName}");

        var row = page.Items.Single();
        row.Period!.Id.Should().Be(s.Autumn.Id);
        row.Period.Status.Should().Be(PracticePeriodStatus.Closed);
        row.AttendancePct.Should().Be(PracticeCalendar.AttendancePct(AttendedDays, elapsed, 0)).And.BeGreaterThan(0);

        // Talaba portfoliosi va TWA tarix ham kuzgi davr bo'yicha (sukut), barcha davrlar ro'yxati bilan.
        var portfolio = await GetOkAsync<PortfolioDto>(s.StudentClient, "/api/student/portfolio");
        portfolio.PeriodId.Should().Be(s.Autumn.Id);
        portfolio.Stats.DaysPresent.Should().Be(AttendedDays);
        portfolio.Periods.Select(p => p.Id).Should().Equal(s.Spring.Id, s.Autumn.Id);

        var diary = await GetOkAsync<List<DiaryEntryDto>>(s.StudentClient, "/api/student/diary");
        diary.Single().PeriodId.Should().Be(s.Autumn.Id);
        diary.Single().PeriodName.Should().Be(s.Autumn.Name);
    }

    [Fact]
    public async Task G_BegonaPeriodId_404()
    {
        var s = await CreateSceneAsync();
        var otherGroup = await Factory.CreateGroupAsync();
        var foreign = await CreatePeriodAsync(otherGroup, s.Tutor.Id, "Begona", Factory.LocalToday().AddDays(-5), Factory.LocalToday().AddDays(5));

        foreach (var periodId in new[] { Guid.NewGuid(), foreign.Id })
        {
            (await s.Admin.GetAsync($"/api/admin/students/{s.Student.Id}?periodId={periodId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
            (await s.TutorClient.GetAsync($"/api/tutor/students/{s.Student.Id}?periodId={periodId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
            (await s.Admin.GetAsync($"/api/admin/students/{s.Student.Id}/attendance?periodId={periodId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
            (await s.TutorClient.GetAsync($"/api/tutor/students/{s.Student.Id}/diaries?periodId={periodId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
            (await s.StudentClient.GetAsync($"/api/student/portfolio?periodId={periodId}")).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
        }
    }

    private static async Task<T> GetOkAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadAsync<T>())!;
    }
}
