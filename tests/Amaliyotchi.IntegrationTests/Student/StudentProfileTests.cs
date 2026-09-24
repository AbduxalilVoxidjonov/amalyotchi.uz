using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student.Profile;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
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
}
