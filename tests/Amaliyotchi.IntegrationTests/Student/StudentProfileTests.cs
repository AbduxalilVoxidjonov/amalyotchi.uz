using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student.PeriodDays;
using Amaliyotchi.Application.Features.Student.Profile;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET /api/student/profile</c>: akademik ma'lumot, tyutor, parol/Telegram holati va sukut bo'yicha
/// davr xulosasi — ko'rsatkichlar tyutor profilidagi (<c>GET /api/tutor/students/{id}</c>) bilan AYNAN bir xil.</summary>
[Collection(ApiCollection.Name)]
public sealed class StudentProfileTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Profil_200_DavrKorxonaTyutorBall_TyutorProfiliBilanBirXil()
    {
        var today = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(today);
        await Factory.WithDbAsync(async db =>
        {
            for (var i = 1; i <= 3; i++)
            {
                var date = today.AddDays(-i);
                db.DailyAttendances.Add(DailyAttendance.CheckIn(
                    scene.Student.Id, scene.Period.Id, date, PracticeTime.At(date, new TimeOnly(9, 0)), 10, 8, CheckInVerdict.Accept(isLate: false)));
            }

            var grade = PracticeGrade.Create(scene.Student.Id, scene.Period.Id);
            grade.SetTutorPoints(18);
            grade.SetReferencePoints(9);
            grade.Finalize("Yaxshi.", scene.Tutor.Id, PracticeTime.At(today.AddDays(-1), new TimeOnly(18, 0)));
            db.PracticeGrades.Add(grade);
            await db.SaveChangesAsync();
        });
        var tutorClient = await Factory.LoginAsync(scene.Tutor);

        string body;
        TutorStudentDetail expected;
        try
        {
            fixture.Clock.Set(PracticeTime.At(today, new TimeOnly(9, 5)));
            var response = await scene.Client.GetAsync("/api/student/profile");
            body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            expected = (await tutorClient.GetFromJsonAsync<TutorStudentDetail>($"/api/tutor/students/{scene.Student.Id}", JsonDefaults.Options))!;
        }
        finally
        {
            fixture.Clock.Reset();
        }

        var dto = JsonSerializer.Deserialize<StudentProfileDto>(body, JsonDefaults.Options)!;
        var hemisId = await Factory.WithDbAsync(db =>
            db.StudentProfiles.Where(p => p.UserId == scene.Student.Id).Select(p => p.HemisId).SingleAsync());

        dto.Id.Should().Be(scene.Student.Id);
        dto.FullName.Should().Be(scene.Student.FullName);
        dto.HemisId.Should().Be(hemisId);
        dto.PhoneNumber.Should().Be(scene.Student.PhoneNumber);
        dto.Group.Should().Be(scene.Group.GroupName);
        dto.Course.Should().Be(scene.Group.Course);
        dto.Faculty.Should().Be(expected.Faculty).And.NotBeEmpty();
        dto.Direction.Should().Be(expected.Direction).And.NotBeEmpty();
        dto.Department.Should().StartWith("Kafedra ");
        dto.Tutor.Should().Be(new StudentProfileTutorDto(scene.Tutor.FullName, scene.Tutor.PhoneNumber));
        dto.TelegramLinked.Should().BeTrue();
        dto.HasPassword.Should().BeFalse();
        dto.MustChangePassword.Should().BeFalse();

        var practice = dto.Practice!;
        practice.Period.Should().Be(new StudentProfilePeriodDto(
            scene.Period.Id, scene.Period.Name, PracticePeriodStatus.Active, scene.Period.StartDate, scene.Period.EndDate));
        practice.Company.Should().Be(new StudentProfileCompanyDto(scene.Company.Id, scene.Company.Name, scene.Company.Address));
        practice.ElapsedWorkDays.Should().Be(expected.Attendance.TotalDays + expected.Attendance.ExcusedDays).And.BePositive();
        practice.AttendancePct.Should().Be(expected.Attendance.AttendancePct).And.BePositive();
        practice.SuspiciousDays.Should().Be(expected.Attendance.SuspiciousDays);
        practice.Total.Should().Be(expected.Grade!.Total).And.BePositive();
        practice.Grade.Should().Be(expected.Grade.Grade);
        practice.Finalized.Should().BeTrue();

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.GetProperty("practice").GetProperty("period").GetProperty("status").GetString().Should().Be("active");
        root.GetProperty("mustChangePassword").ValueKind.Should().Be(JsonValueKind.False);
        root.GetProperty("tutor").GetProperty("fullName").GetString().Should().Be(scene.Tutor.FullName);
    }

    [Fact]
    public async Task Profil_DavrYoq_PracticeNull_TyutorNull()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var response = await client.GetAsync("/api/student/profile");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var dto = JsonSerializer.Deserialize<StudentProfileDto>(body, JsonDefaults.Options)!;
        dto.Practice.Should().BeNull();
        dto.Tutor.Should().BeNull("guruhga tyutor biriktirilmagan");
        dto.TelegramLinked.Should().BeTrue();

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("practice").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("tutor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Profil_ArizasizRejalashtirilganDavr_KorxonaNull_BallNol()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var today = Factory.LocalToday();
        var period = await Factory.CreatePeriodAtAsync(group, tutor.Id, today, startDaysAgo: -10, endDaysAhead: 40);
        var client = await Factory.LoginAsStudentAsync(student);

        var dto = await (await client.GetAsync("/api/student/profile")).Content.ReadAsync<StudentProfileDto>();

        dto!.Practice.Should().NotBeNull();
        dto.Practice!.Period.Id.Should().Be(period.Id);
        dto.Practice.Period.Status.Should().Be(PracticePeriodStatus.Planned);
        dto.Practice.Company.Should().BeNull();
        dto.Practice.ElapsedWorkDays.Should().Be(0);
        dto.Practice.Total.Should().Be(0);
        dto.Practice.Grade.Should().BeNull();
        dto.Practice.Finalized.Should().BeFalse();
    }

    [Fact]
    public async Task Profil_ParolOrnatilganTalaba_HasPassword_MustChange()
    {
        var student = await Factory.CreateStudentAsync();
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.PostJsonAsync($"/api/admin/students/{student.Id}/password", new { password = "Vaqtinchalik-1" }))
            .EnsureSuccessStatusCode();
        var client = await Factory.LoginAsStudentAsync(student);

        var dto = await (await client.GetAsync("/api/student/profile")).Content.ReadAsync<StudentProfileDto>();

        dto!.HasPassword.Should().BeTrue();
        dto.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Profil_AdminVaTyutor_403_Tokensiz_401()
    {
        var admin = await Factory.LoginAsAdminAsync();
        var tutor = await Factory.LoginAsTutorAsync();

        (await admin.GetAsync("/api/student/profile")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tutor.GetAsync("/api/student/profile")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/student/profile")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Guruhga biriktirilgan, faollashtirilgan davr (<paramref name="from"/>..<paramref name="to"/> kun bugunga nisbatan).</summary>
    private Task<PracticePeriod> PeriodAsync(TestGroup group, Guid createdBy, int from, int to, bool close = false) =>
        Factory.WithDbAsync(async db =>
        {
            var today = Factory.LocalToday();
            var period = PracticePeriod.Create(
                $"Davr {Guid.NewGuid():N}"[..16], group.AcademicYearId, today.AddDays(from), today.AddDays(to),
                createdBy, CheckInRules.Default, WorkDays.MondayToSaturday, 20, dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            if (close)
                period.Close();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    private Task<Company> CompanyAsync(string name) => Factory.CreateCompanyAsync(name: $"{name} {Guid.NewGuid():N}"[..30]);

    [Fact]
    public async Task Practices_IkkiDavr_HarBiriOzKorxonasiVaDavomati_PeriodDaysBilanBirXil()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var currentCompany = await CompanyAsync("Joriy Korxona");
        var oldCompany = await CompanyAsync("Eski Korxona");

        var current = await PeriodAsync(group, tutor.Id, -10, 20);
        var old = await PeriodAsync(group, tutor.Id, -80, -40, close: true);
        await Factory.CreateApprovedApplicationAsync(student, current, currentCompany, tutor.Id);
        await Factory.CreateApprovedApplicationAsync(student, old, oldCompany, tutor.Id);

        // Eski davrda 3 ta check-in (yakshanbasiz), joriy davrda — 1 ta.
        await Factory.WithDbAsync(async db =>
        {
            var oldDays = Enumerable.Range(1, 10).Select(i => old.StartDate.AddDays(i))
                .Where(d => d.DayOfWeek != DayOfWeek.Sunday).Take(3);
            var currentDay = Enumerable.Range(1, 5).Select(i => Factory.LocalToday().AddDays(-i))
                .First(d => d.DayOfWeek != DayOfWeek.Sunday);
            var records = oldDays.Select(d => (PeriodId: old.Id, Date: d)).Append((PeriodId: current.Id, Date: currentDay));
            foreach (var (periodId, date) in records)
                db.DailyAttendances.Add(DailyAttendance.CheckIn(
                    student.Id, periodId, date, PracticeTime.At(date, new TimeOnly(9, 0)), 10, 8, CheckInVerdict.Accept(isLate: false)));
            await db.SaveChangesAsync();
        });

        var client = await Factory.LoginAsStudentAsync(student);
        var tutorClient = await Factory.LoginAsync(tutor);

        var response = await client.GetAsync("/api/student/profile");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var dto = JsonSerializer.Deserialize<StudentProfileDto>(body, JsonDefaults.Options)!;
        var periodDays = (await (await client.GetAsync("/api/student/period-days")).Content.ReadAsync<StudentPeriodDaysDto>())!;

        dto.Practices.Should().HaveCount(2);
        dto.Practices.Select(p => p.Period.Id).Should().BeEquivalentTo(periodDays.Periods.Select(p => p.Id));
        dto.Practices.Select(p => p.Period.Id).Should().Equal(current.Id, old.Id);

        // `practice` — sukut davri, o'zgarmagan va practices dagi o'z elementi bilan bir xil.
        dto.Practice!.Period.Id.Should().Be(current.Id);
        dto.Practices[0].Should().BeEquivalentTo(dto.Practice);

        dto.Practices[0].Company!.Id.Should().Be(currentCompany.Id);
        dto.Practices[1].Company!.Id.Should().Be(oldCompany.Id);
        dto.Practices[0].Period.Status.Should().Be(PracticePeriodStatus.Active);
        dto.Practices[1].Period.Status.Should().Be(PracticePeriodStatus.Closed);

        foreach (var practice in dto.Practices)
        {
            var expected = (await tutorClient.GetFromJsonAsync<TutorStudentDetail>(
                $"/api/tutor/students/{student.Id}?periodId={practice.Period.Id}", JsonDefaults.Options))!;
            practice.ElapsedWorkDays.Should().Be(expected.Attendance.TotalDays + expected.Attendance.ExcusedDays);
            practice.AttendancePct.Should().Be(expected.Attendance.AttendancePct);
            practice.SuspiciousDays.Should().Be(expected.Attendance.SuspiciousDays);
            practice.Total.Should().Be(expected.Grade?.Total ?? 0);
            practice.Grade.Should().Be(expected.Grade?.Grade);
            practice.Finalized.Should().BeFalse();
        }
        dto.Practices[1].ElapsedWorkDays.Should().BeGreaterThan(dto.Practices[0].ElapsedWorkDays, "eski davr to'liq o'tgan");

        using var json = JsonDocument.Parse(body);
        var items = json.RootElement.GetProperty("practices");
        items.GetArrayLength().Should().Be(2);
        items[1].GetProperty("period").GetProperty("status").GetString().Should().Be("closed");
    }

    [Fact]
    public async Task Practices_DavomEtayotganDavrBirinchi_KeyinStartDateKamayish()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var ongoing = await PeriodAsync(group, tutor.Id, -5, 20);
        var upcoming = await PeriodAsync(group, tutor.Id, 30, 60);
        var past = await PeriodAsync(group, tutor.Id, -90, -60, close: true);
        var client = await Factory.LoginAsStudentAsync(student);

        var dto = (await (await client.GetAsync("/api/student/profile")).Content.ReadAsync<StudentProfileDto>())!;

        dto.Practices.Select(p => p.Period.Id).Should().Equal(ongoing.Id, upcoming.Id, past.Id);
        dto.Practices.Select(p => p.Period.Status)
            .Should().Equal(PracticePeriodStatus.Active, PracticePeriodStatus.Planned, PracticePeriodStatus.Closed);
        dto.Practices.Should().OnlyContain(p => p.Company == null && !p.Finalized);
        dto.Practice!.Period.Id.Should().Be(ongoing.Id);
    }

    [Fact]
    public async Task Practices_DavrYoq_BoshRoyxat()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        var body = await (await client.GetAsync("/api/student/profile")).Content.ReadAsStringAsync();

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("practices").ValueKind.Should().Be(JsonValueKind.Array);
        json.RootElement.GetProperty("practices").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Practices_OtkazilganTalaba_YangiKorxona()
    {
        var group = await Factory.CreateGroupAsync();
        var tutor = await Factory.CreateTutorAsync(group);
        var student = await Factory.CreateStudentAsync(group: group);
        var oldCompany = await CompanyAsync("Eski Otkazish");
        var newCompany = await CompanyAsync("Yangi Otkazish");
        var period = await PeriodAsync(group, tutor.Id, -5, 20);
        await Factory.CreateApprovedApplicationAsync(student, period, oldCompany, tutor.Id);

        var admin = await Factory.LoginAsAdminAsync();
        var transfer = await admin.PostJsonAsync($"/api/admin/students/{student.Id}/company", new { companyId = newCompany.Id });
        transfer.StatusCode.Should().Be(HttpStatusCode.OK, await transfer.Content.ReadAsStringAsync());

        var client = await Factory.LoginAsStudentAsync(student);
        var dto = (await (await client.GetAsync("/api/student/profile")).Content.ReadAsync<StudentProfileDto>())!;

        dto.Practices.Should().ContainSingle();
        dto.Practices[0].Company!.Id.Should().Be(newCompany.Id, "o'tkazilgan (Transferred) ariza hisobga olinmaydi");
        dto.Practice!.Company!.Id.Should().Be(newCompany.Id);
    }
}
