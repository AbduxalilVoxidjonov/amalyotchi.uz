using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Application.Features.Admin.Dashboard;
using Amaliyotchi.Application.Features.Admin.Nav;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.IntegrationTests.Admin;

/// <summary>Dashboard ko'rsatkichlari loyiha qoidalariga mosligi: yopilgan davr talabalari bugungi/kechagi statistikaga
/// kirmaydi, ariza/shartnomalar faqat ochiq davrlar bo'yicha, <c>Transferred</c> sanalmaydi, talabalar/fakultetlar soni
/// sidebar nav bilan teng. Baza umumiy — global sonlar "oldin/keyin" farqi (delta) bilan tekshiriladi, soat muzlatilgan.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdminDashboardRulesTests(ApiFixture fixture)
{
    /// <summary>Chorshanba, bayram emas.</summary>
    private static readonly DateOnly Day = new(2026, 11, 18);

    private ApiFactory Factory => fixture.Factory;

    [Fact]
    public async Task Dashboard_YopilganDavr_BugunVaKechaKirmaydi_OchiqDavr_ToGriSanaladi()
    {
        var client = await Factory.LoginAsAdminAsync();
        fixture.Clock.Set(PracticeTime.At(Day, new TimeOnly(10, 0)));
        try
        {
            var before = await GetDashboardAsync(client);

            // --- Ochiq (davom etayotgan) davr: 5 talaba ---
            var openGroup = await Factory.CreateGroupAsync();
            var openTutor = await Factory.CreateTutorAsync(openGroup, "Ochiq Tyutor");
            var openPeriod = await CreatePeriodAsync(openGroup, openTutor.Id, closed: false);
            var companyA = await Factory.CreateCompanyAsync();
            var companyB = await Factory.CreateCompanyAsync();
            var companyOld = await Factory.CreateCompanyAsync();

            var present = await Factory.CreateStudentAsync(group: openGroup, fullName: "Keldi");
            var late = await Factory.CreateStudentAsync(group: openGroup, fullName: "Kech");
            var excused = await Factory.CreateStudentAsync(group: openGroup, fullName: "Sababli");
            var transferred = await Factory.CreateStudentAsync(group: openGroup, fullName: "O'tkazilgan");
            var noApplication = await Factory.CreateStudentAsync(group: openGroup, fullName: "Arizasiz");

            await Factory.CreateApprovedApplicationAsync(present, openPeriod, companyA, openTutor.Id);
            await Factory.CreateApprovedApplicationAsync(late, openPeriod, companyA, openTutor.Id);
            await Factory.CreatePendingApplicationAsync(excused, openPeriod, companyA, TimeSpan.FromHours(72));
            await TransferThenApproveAsync(transferred, openPeriod, companyOld, companyB, openTutor.Id);

            await Factory.CheckInAsync(present, openPeriod, Day);
            await Factory.CheckInAsync(late, openPeriod, Day, late: true);
            await Factory.CheckInAsync(present, openPeriod, Day.AddDays(-1));
            await ApprovedLeaveAsync(excused, openPeriod, Day.AddDays(-1), Day, openTutor.Id);
            await DiaryAsync(present, openPeriod, Day);

            // --- Yopilgan davr (sanalari hali tugamagan): 2 talaba, bugun ham yozuvi bor ---
            var closedGroup = await Factory.CreateGroupAsync();
            var closedTutor = await Factory.CreateTutorAsync(closedGroup, "Yopiq Tyutor");
            var closedPeriod = await CreatePeriodAsync(closedGroup, closedTutor.Id, closed: true);
            var companyClosed = await Factory.CreateCompanyAsync();
            var closedStudent = await Factory.CreateStudentAsync(group: closedGroup, fullName: "Yopiq Keldi");
            var closedPending = await Factory.CreateStudentAsync(group: closedGroup, fullName: "Yopiq Ariza");
            await Factory.CreateApprovedApplicationAsync(closedStudent, closedPeriod, companyClosed, closedTutor.Id);
            await Factory.CreatePendingApplicationAsync(closedPending, closedPeriod, companyClosed, TimeSpan.FromHours(72));
            await Factory.CheckInAsync(closedStudent, closedPeriod, Day);
            await Factory.CheckInAsync(closedStudent, closedPeriod, Day.AddDays(-1));

            var after = await GetDashboardAsync(client);
            var d = Delta(before.Stats, after.Stats);

            // Bugun: faqat ochiq davr talabalari (5). Yopilgan davrdagi 2 talaba "kelmadi" bo'lib sanalmaydi.
            d.ExpectedToday.Should().Be(5);
            d.PresentToday.Should().Be(1);
            d.LateToday.Should().Be(1);
            d.ExcusedToday.Should().Be(1, "yozuvi yo'q + tasdiqlangan ruxsat");
            d.AbsentToday.Should().Be(2);
            d.NoDiaryToday.Should().Be(1, "kech kelgan talaba kundalik yozmagan");
            d.ExpectedYesterday.Should().Be(5);
            d.OngoingPeriods.Should().Be(1);

            // Ariza/shartnomalar — faqat ochiq davr; Transferred hech qayerda.
            d.ApplicationsPending.Should().Be(1);
            d.ApplicationsOverdue.Should().Be(1);
            d.ContractsApproved.Should().Be(3, "keldi, kech va o'tkazilgandan keyingi yangi tasdiqlangan ariza");
            d.ContractsRevision.Should().Be(0);
            d.ContractsRejected.Should().Be(0);
            d.ContractsMissing.Should().Be(1, "faqat arizasiz talaba (yopilgan davr sanalmaydi)");

            // Korxonalar: katalogda faol — 4 ta yangi; bugun amaliyotchisi bor — A va B (yopilgan davr korxonasi emas).
            d.CompaniesActive.Should().Be(4);
            d.CompaniesWithInterns.Should().Be(2);

            // Fakultet kesimi — har guruh o'z fakultetida.
            var openFaculty = after.Faculties.Single(f => f.Id == openGroup.FacultyId);
            openFaculty.StudentCount.Should().Be(5);
            openFaculty.ExpectedToday.Should().Be(5);
            openFaculty.AttendedToday.Should().Be(2);
            openFaculty.AttendancePct.Should().Be(50, "2 / (5 − 1 sababli)");

            var closedFaculty = after.Faculties.Single(f => f.Id == closedGroup.FacultyId);
            closedFaculty.StudentCount.Should().Be(2);
            closedFaculty.ExpectedToday.Should().Be(0);
            closedFaculty.AttendedToday.Should().Be(0);
            closedFaculty.AttendancePct.Should().Be(0);

            // Tyutorlar: kutilayotgan — faqat ochiq davr arizalari.
            var openTutorRow = after.Tutors.Single(t => t.Id == openTutor.Id);
            openTutorRow.PendingCount.Should().Be(1);
            openTutorRow.Status.Should().Be(TutorStatus.Late);
            var closedTutorRow = after.Tutors.Single(t => t.Id == closedTutor.Id);
            closedTutorRow.PendingCount.Should().Be(0, "yopilgan davr arizasi tyutor navbatida emas");
            closedTutorRow.OldestPendingAt.Should().BeNull();
            closedTutorRow.Status.Should().Be(TutorStatus.Active);

            // Fakultetlar ro'yxati bilan bir xil foiz.
            var facultiesPage = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/faculties?pageSize=100&q={openFaculty.Code}");
            facultiesPage.GetProperty("items").EnumerateArray()
                .Single(f => f.GetProperty("id").GetGuid() == openFaculty.Id)
                .GetProperty("attendancePct").GetInt32().Should().Be(50);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Dashboard_KechagiFoiz_SabablilarMaxrajdanChiqadi()
    {
        // Boshqa testlar davrlari yetib bormaydigan uzoq sana — umumiy statistika faqat shu test ma'lumoti.
        var day = new DateOnly(2031, 3, 12);
        var client = await Factory.LoginAsAdminAsync();
        fixture.Clock.Set(PracticeTime.At(day, new TimeOnly(10, 0)));
        try
        {
            var group = await Factory.CreateGroupAsync();
            var tutor = await Factory.CreateTutorAsync(group, "Kecha Tyutor");
            var period = await CreatePeriodAsync(group, tutor.Id, closed: false, day);
            var a = await Factory.CreateStudentAsync(group: group);
            var b = await Factory.CreateStudentAsync(group: group);
            await Factory.CreateStudentAsync(group: group);
            await Factory.CheckInAsync(a, period, day.AddDays(-1));
            await ApprovedLeaveAsync(b, period, day.AddDays(-1), day.AddDays(-1), tutor.Id);

            var dto = await GetDashboardAsync(client);

            dto.Stats.OngoingPeriods.Should().Be(1);
            dto.Stats.ExpectedYesterday.Should().Be(3);
            dto.Stats.AttendanceYesterdayPct.Should().Be(50, "1 keldi / (3 − 1 sababli)");
            dto.Stats.ExpectedToday.Should().Be(3);
            dto.Stats.AbsentToday.Should().Be(3, "bugun hali hech kim belgilanmagan");
            dto.Stats.AttendanceTodayPct.Should().Be(0);
        }
        finally
        {
            fixture.Clock.Reset();
        }
    }

    [Fact]
    public async Task Dashboard_TalabalarVaFakultetlar_NavVaRoyxatlarBilanTeng()
    {
        var client = await Factory.LoginAsAdminAsync();

        // Guruhi o'chirilgan talaba — ro'yxat/nav'da ko'rinmaydi, dashboard ham sanamasligi kerak.
        var group = await Factory.CreateGroupAsync();
        await Factory.CreateStudentAsync(group: group, fullName: "O'chirilgan guruh talabasi");
        await Factory.CreateStudentAsync(fullName: "Oddiy talaba", linkTelegram: false);
        await Factory.WithDbAsync(async db =>
        {
            var entity = await db.StudentGroups.FirstAsync(g => g.Id == group.GroupId);
            entity.IsDeleted = true;
            entity.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return 0;
        });

        var nav = (await client.GetFromJsonAsync<AdminNavDto>("/api/admin/nav"))!;
        var dto = await GetDashboardAsync(client);
        var students = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin/students");
        var groups = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin/groups");

        dto.Stats.StudentsTotal.Should().Be(nav.Counts.Students);
        dto.Stats.StudentsTotal.Should().Be(students.GetProperty("total").GetInt32());
        dto.Stats.Faculties.Should().Be(nav.Counts.Faculties);
        dto.Stats.Faculties.Should().Be(dto.Faculties.Count);
        dto.Stats.Groups.Should().Be(groups.GetProperty("total").GetInt32());
        dto.Stats.StudentsLinked.Should().BeGreaterThanOrEqualTo(0);
        dto.Stats.StudentsUnlinked.Should().Be(dto.Stats.StudentsTotal - dto.Stats.StudentsLinked);
        dto.Stats.StudentsUnlinked.Should().BeGreaterThanOrEqualTo(1);
        dto.Faculties.Sum(f => f.StudentCount).Should().Be(dto.Stats.StudentsTotal);
    }

    private async Task<AdminDashboardDto> GetDashboardAsync(HttpClient client)
        => (await client.GetFromJsonAsync<AdminDashboardDto>("/api/admin/dashboard"))!;

    private static DashboardStatsDto Delta(DashboardStatsDto a, DashboardStatsDto b) => new(
        b.StudentsTotal - a.StudentsTotal,
        b.StudentsLinked - a.StudentsLinked,
        b.StudentsUnlinked - a.StudentsUnlinked,
        b.Faculties - a.Faculties,
        b.Groups - a.Groups,
        b.CompaniesActive - a.CompaniesActive,
        b.CompaniesWithInterns - a.CompaniesWithInterns,
        b.ApplicationsPending - a.ApplicationsPending,
        b.ApplicationsOverdue - a.ApplicationsOverdue,
        b.ContractsApproved - a.ContractsApproved,
        b.ContractsRevision - a.ContractsRevision,
        b.ContractsRejected - a.ContractsRejected,
        b.ContractsMissing - a.ContractsMissing,
        b.OngoingPeriods - a.OngoingPeriods,
        b.ExpectedToday - a.ExpectedToday,
        b.PresentToday - a.PresentToday,
        b.LateToday - a.LateToday,
        b.AbsentToday - a.AbsentToday,
        b.ExcusedToday - a.ExcusedToday,
        b.NoDiaryToday - a.NoDiaryToday,
        b.AttendanceTodayPct - a.AttendanceTodayPct,
        b.ExpectedYesterday - a.ExpectedYesterday,
        b.AttendanceYesterdayPct - a.AttendanceYesterdayPct);

    /// <summary>Davr: <see cref="Day"/> −14..+30, har kuni ish kuni; <paramref name="closed"/> — sanalari tugamagan, lekin yopilgan.</summary>
    private Task<PracticePeriod> CreatePeriodAsync(TestGroup group, Guid createdBy, bool closed, DateOnly? day = null)
        => Factory.WithDbAsync(async db =>
        {
            var d = day ?? Day;
            var period = PracticePeriod.Create(
                $"Dash {Guid.NewGuid():N}"[..20], group.AcademicYearId, d.AddDays(-14), d.AddDays(30), createdBy,
                CheckInRules.Default, WorkDays.MondayToSaturday | WorkDays.Sunday, 36, dailyReportRequired: true);
            period.AttachGroup(group.GroupId);
            period.Activate();
            if (closed)
                period.Close();
            db.PracticePeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        });

    private Task<int> TransferThenApproveAsync(TestUser student, PracticePeriod period, Company from, Company to, Guid tutorId)
        => Factory.WithDbAsync(async db =>
        {
            var now = Factory.Now();
            var first = PracticeApplication.Create(student.Id, period.Id, from.Id, from.RadiusM, null, now.AddDays(-10));
            first.Approve(tutorId, from.RadiusM, Enumerable.Range(0, PracticeApplication.ChecklistItemCount), "OK", now.AddDays(-9));
            db.PracticeApplications.Add(first);
            await db.SaveChangesAsync();
            first.Transfer(tutorId, "boshqa korxona", now.AddDays(-5));
            var second = PracticeApplication.Create(student.Id, period.Id, to.Id, to.RadiusM, null, now.AddDays(-5));
            second.Approve(tutorId, to.RadiusM, Enumerable.Range(0, PracticeApplication.ChecklistItemCount), "OK", now.AddDays(-5));
            db.PracticeApplications.Add(second);
            await db.SaveChangesAsync();
            return 0;
        });

    private Task<int> ApprovedLeaveAsync(TestUser student, PracticePeriod period, DateOnly from, DateOnly to, Guid tutorId)
        => Factory.WithDbAsync(async db =>
        {
            var leave = LeaveRequest.Create(student.Id, period.Id, from, to, "Kasallik sababli kelolmayman");
            leave.Approve(tutorId, null, Factory.Now());
            db.LeaveRequests.Add(leave);
            await db.SaveChangesAsync();
            return 0;
        });

    private Task<int> DiaryAsync(TestUser student, PracticePeriod period, DateOnly date)
        => Factory.WithDbAsync(async db =>
        {
            db.DiaryEntries.Add(DiaryEntry.Create(
                student.Id, period.Id, date, new string('a', DiaryEntry.DefaultMinTextLength), null, Factory.Now()));
            await db.SaveChangesAsync();
            return 0;
        });
}
