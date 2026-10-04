using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Nav;
using Amaliyotchi.Application.Features.Tutor.Students;
using Amaliyotchi.Application.Features.Tutor.Today;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Tutor;

[Collection(ApiCollection.Name)]
public sealed class TutorNavTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Nav_SonlarSahifalarBilanTeng_BoshqaTyutorKirmaydi()
    {
        // Ko'lam: 2 talaba, 1 yuborilgan ariza (+1 tasdiqlangan), 1 tekshirilmagan kundalik (+1 tasdiqlangan).
        var s = await Factory.CreateTutorScenarioAsync(approved: false);
        var second = await Factory.CreateStudentAsync(group: s.Group);
        await Factory.CreateApprovedApplicationAsync(second, s.Period, s.Company, s.Tutor.Id);
        var days = await Factory.PastWorkDaysAsync(s.Period, 2);
        await Factory.AddDiaryAsync(second, s.Period, days[0]);
        await Factory.AddDiaryAsync(second, s.Period, days[1], s.Tutor.Id, score: 5);

        // Begona tyutor: o'z guruhi, talabalari, arizasi va kundaligi — birinchi tyutor soniga kirmasligi kerak.
        var other = await Factory.CreateTutorScenarioAsync(approved: false);
        var otherStudent = await Factory.CreateStudentAsync(group: other.Group);
        await Factory.AddDiaryAsync(otherStudent, other.Period, (await Factory.PastWorkDaysAsync(other.Period, 1))[0]);

        var response = await s.Client.GetAsync("/api/tutor/nav");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(body))
        {
            var counts = json.RootElement.GetProperty("counts");
            foreach (var key in new[] { "today", "applications", "students", "diaries" })
                counts.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Number, key);
            var context = json.RootElement.GetProperty("context");
            context.GetProperty("groups").ValueKind.Should().Be(JsonValueKind.Array);
            context.GetProperty("periodName").ValueKind.Should().Be(JsonValueKind.String);
        }

        var nav = JsonSerializer.Deserialize<TutorNavDto>(body, JsonDefaults.Options)!;
        nav.Counts.Should().Be(new TutorNavCounts(Today: 2, Applications: 1, Students: 2, Diaries: 1, PendingFaceEnrollments: 0));
        nav.Context.Groups.Should().Equal(s.Group.GroupName);
        nav.Context.PeriodName.Should().Be(s.Period.Name);

        // Har son — tegishli sahifaning sukut holati bilan aynan teng.
        var today = (await (await s.Client.GetAsync("/api/tutor/today")).Content.ReadAsync<TodayResponse>())!;
        nav.Counts.Today.Should().Be(today.Stats.Total).And.Be(today.Rows.Total);
        var applications = (await (await s.Client.GetAsync("/api/tutor/applications")).Content.ReadAsync<ApplicationListResponse>())!;
        nav.Counts.Applications.Should().Be(applications.Counts.Submitted);
        var students = (await (await s.Client.GetAsync("/api/tutor/students")).Content.ReadAsync<List<TutorStudent>>())!;
        nav.Counts.Students.Should().Be(students.Count);
        var diaries = (await (await s.Client.GetAsync("/api/tutor/diaries")).Content.ReadAsync<List<TutorDiaryEntry>>())!;
        nav.Counts.Diaries.Should().Be(diaries.Count(d => d.Status is DiaryStatus.Submitted or DiaryStatus.Seen));

        // Begona tyutor o'zinikini ko'radi (2 talaba, 1 ariza, 1 kundalik).
        var otherNav = (await (await other.Client.GetAsync("/api/tutor/nav")).Content.ReadAsync<TutorNavDto>())!;
        otherNav.Counts.Should().Be(new TutorNavCounts(2, 1, 2, 1, 0));
        otherNav.Context.Groups.Should().Equal(other.Group.GroupName);
    }

    [Fact]
    public async Task Nav_BirNechtaGuruh_TartiblanganVaDavomEtayotganDavrUstun()
    {
        var groupA = await Factory.CreateGroupAsync();
        var groupB = await Factory.CreateGroupAsync(groupA.FacultyId);
        var tutor = await Factory.CreateTutorAsync(groupA, "Nav Ko'p guruh", groupB.GroupId);
        var client = await Factory.LoginAsync(tutor);

        // A — faqat tugagan davr; B — davom etayotgan davr → ongoing ustun.
        var ended = await CreatePeriodAsync(groupA, tutor.Id, startOffset: -90, endOffset: -60);
        var ongoing = await Factory.CreateActivePeriodAsync(groupB, tutor.Id);

        var nav = (await (await client.GetAsync("/api/tutor/nav")).Content.ReadAsync<TutorNavDto>())!;

        nav.Context.Groups.Should().Equal(new[] { groupA.GroupName, groupB.GroupName }.Order(StringComparer.Ordinal));
        nav.Context.PeriodName.Should().Be(ongoing.Name).And.NotBe(ended.Name);
        nav.Counts.Should().Be(new TutorNavCounts(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Nav_GuruhsizTyutor_NolBoshRoyxatNull()
    {
        var tutor = await Factory.CreateTutorAsync();
        await Factory.WithDbAsync(db => db.TutorAssignments.Where(a => a.TutorUserId == tutor.Id).ExecuteDeleteAsync());
        var client = await Factory.LoginAsync(tutor);

        var response = await client.GetAsync("/api/tutor/nav");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("context").GetProperty("periodName").ValueKind.Should().Be(JsonValueKind.Null);
        var nav = JsonSerializer.Deserialize<TutorNavDto>(body, JsonDefaults.Options)!;
        nav.Counts.Should().Be(new TutorNavCounts(0, 0, 0, 0, 0));
        nav.Context.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Nav_Admin_403_Anonim_401()
    {
        var admin = await Factory.LoginAsAdminAsync();
        (await admin.GetAsync("/api/tutor/nav")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Factory.CreateClient().GetAsync("/api/tutor/nav")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<PracticePeriod> CreatePeriodAsync(TestGroup group, Guid createdBy, int startOffset, int endOffset) =>
        Factory.WithDbAsync(async db =>
        {
            var today = PracticeTime.LocalDate(Factory.Services.GetRequiredService<IClock>().UtcNow);
            var period = PracticePeriod.Create(
                $"Tugagan {Guid.NewGuid():N}"[..20], group.AcademicYearId, today.AddDays(startOffset), today.AddDays(endOffset),
                createdBy, Amaliyotchi.Domain.Attendance.CheckInRules.Default, WorkDays.MondayToSaturday, 20,
                dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });
}
