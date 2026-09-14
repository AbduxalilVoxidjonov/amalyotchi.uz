using System.Net;
using System.Text.Json;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Student;

/// <summary><c>GET /api/student/portfolio</c>: statistika, GradeCalculator tarkibi, yakuniy baho.
/// Statistika <see cref="MutableClock"/> bilan muzlatilgan kunga nisbatan hisoblanadi.</summary>
[Collection(ApiCollection.Name)]
public sealed class PortfolioTests(ApiFixture fixture)
{
    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Portfolio_200_StatistikaVaBahoTarkibi()
    {
        var today = await Factory.NextWorkDayAsync();
        var scene = await Factory.CreateSceneAsync(today);
        await Factory.WithDbAsync(async db =>
        {
            // 3 kun keldi (bittasi kech), 1 kun sababli; 2 ta baholangan kundalik (4 va 5).
            for (var i = 1; i <= 3; i++)
            {
                var date = today.AddDays(-i);
                db.DailyAttendances.Add(DailyAttendance.CheckIn(
                    scene.Student.Id, scene.Period.Id, date, PracticeTime.At(date, new TimeOnly(9, 0)), 10, 8, CheckInVerdict.Accept(isLate: i == 1)));
                if (i <= 2)
                {
                    var entry = DiaryEntry.Create(scene.Student.Id, scene.Period.Id, date, StudentTestData.LongText(), null, PracticeTime.At(date, new TimeOnly(18, 0)));
                    entry.Approve(scene.Tutor.Id, i == 1 ? 5 : 4, null, PracticeTime.At(date, new TimeOnly(20, 0)));
                    db.DiaryEntries.Add(entry);
                }
            }

            var leave = Domain.Leave.LeaveRequest.Create(scene.Student.Id, scene.Period.Id, today.AddDays(-4), today.AddDays(-4), "Shifokor ko'rigi — ma'lumotnoma");
            leave.Approve(scene.Tutor.Id, null, PracticeTime.At(today.AddDays(-5), new TimeOnly(12, 0)));
            db.LeaveRequests.Add(leave);

            var grade = PracticeGrade.Create(scene.Student.Id, scene.Period.Id);
            grade.SetTutorPoints(18);
            grade.SetReferencePoints(9);
            grade.Finalize("Amaliyotni muvaffaqiyatli o'tadi.", scene.Tutor.Id, PracticeTime.At(today.AddDays(-1), new TimeOnly(18, 0)));
            db.PracticeGrades.Add(grade);
            await db.SaveChangesAsync();
        });

        PortfolioDto dto;
        string body;
        try
        {
            // 09:05 — bugungi oyna ochiq: bugun hali DaysTotal'ga kirmaydi.
            fixture.Clock.Set(PracticeTime.At(today, new TimeOnly(9, 5)));
            var response = await scene.Client.GetAsync("/api/student/portfolio");

            body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            dto = (await response.Content.ReadAsync<PortfolioDto>())!;
        }
        finally
        {
            fixture.Clock.Reset();
        }

        dto.Student.Should().Be(scene.Student.FullName);
        dto.Group.Should().Be(scene.Group.GroupName);
        dto.Company.Should().Be(scene.Company.Name);
        dto.PracticeTitle.Should().Be(scene.Period.Name);
        dto.PeriodFrom.Should().Be(scene.Period.StartDate);
        dto.PeriodTo.Should().Be(scene.Period.EndDate);

        // Kutilgan qiymatlar bayramlarga bog'liq (seed'dagi O'zbekiston bayramlari ish kuni emas).
        var holidays = await Factory.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.Holidays));
        bool IsWorkDay(DateOnly d) => !holidays.Any(h => h.AppliesTo(d));
        var expectedTotal = Enumerable.Range(1, 14).Select(i => today.AddDays(-i)).Count(IsWorkDay);
        var expectedExcused = IsWorkDay(today.AddDays(-4)) ? 1 : 0;
        var expectedPresent = 3; // yozuv bor kun bayram bo'lsa ham hisobga olinadi (qator ustun)
        expectedTotal += Enumerable.Range(1, 3).Select(i => today.AddDays(-i)).Count(d => !IsWorkDay(d));

        dto.Stats.DaysPresent.Should().Be(expectedPresent);
        dto.Stats.Late.Should().Be(1);
        dto.Stats.Excused.Should().Be(expectedExcused);
        dto.Stats.DaysTotal.Should().Be(expectedTotal, "davr 14 kun oldin boshlangan, bugun hali hisobga olinmaydi");
        dto.Stats.AttendancePct.Should().BeApproximately(expectedPresent * 100d / (expectedTotal - expectedExcused), 0.1, "sababli kun maxrajdan chiqariladi");
        dto.Stats.Reports.Should().Be(2);
        dto.Stats.AvgScore.Should().Be(4.5);

        dto.Score.Select(s => s.Key).Should().Equal("attendance", "reports", "tutor", "reference");
        dto.Score.Select(s => s.WeightPct).Should().Equal(40, 30, 20, 10);
        dto.Score.Single(s => s.Key == "tutor").Points.Should().Be(18);
        dto.Score.Single(s => s.Key == "reference").Points.Should().Be(9);
        dto.Score.Single(s => s.Key == "reports").Points.Should().Be(27, "4.5/5 × 30");
        dto.Total.Should().BeApproximately(dto.Score.Sum(s => s.Points), 0.11);
        dto.Grade.Should().BeNull("davomat 70% dan past — qayta topshiradi");
        dto.Finalized.Should().BeTrue();
        dto.Conclusion.Should().NotBeNull();
        dto.Conclusion!.Author.Should().Be(scene.Tutor.FullName);
        dto.PdfUrl.Should().BeNull();

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("grade").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("score")[0].GetProperty("key").GetString().Should().Be("attendance");
    }

    [Fact]
    public async Task Portfolio_BoshDavr_200_NolStatistika()
    {
        var scene = await Factory.CreateSceneAsync(approve: false);

        var dto = await (await scene.Client.GetAsync("/api/student/portfolio")).Content.ReadAsync<PortfolioDto>();

        dto!.Company.Should().BeNull();
        dto.Stats.DaysPresent.Should().Be(0);
        dto.Stats.Reports.Should().Be(0);
        dto.Stats.AvgScore.Should().Be(0);
        dto.Finalized.Should().BeFalse();
        dto.Conclusion.Should().BeNull();
    }

    [Fact]
    public async Task Portfolio_FaolDavrYoq_404()
    {
        var student = await Factory.CreateStudentAsync();
        var client = await Factory.LoginAsStudentAsync(student);

        (await client.GetAsync("/api/student/portfolio")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tyutor_403()
    {
        var tutor = await Factory.LoginAsTutorAsync();
        (await tutor.GetAsync("/api/student/portfolio")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
