using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Companies;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Settings;
using Amaliyotchi.Domain.ValueObjects;
using Amaliyotchi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amaliyotchi.IntegrationTests.Tutor;

/// <summary>Tyutor testlari uchun tayyor sahna: guruh + tyutor (kirgan klient) + talaba + faol davr + korxona + tasdiqlangan ariza.</summary>
public sealed record TutorScenario(
    TestGroup Group,
    TestUser Tutor,
    HttpClient Client,
    TestUser Student,
    PracticePeriod Period,
    Company Company,
    PracticeApplication Application);

public static class TutorTestHelpers
{
    /// <summary>150 belgidan uzun, kundalik uchun yaroqli matn.</summary>
    public const string DiaryText =
        "Bugun korxonada loyiha kodini o'rgandim, muhitni sozladim va mentor bilan kun rejasini kelishib oldim. " +
        "Kun davomida uchragan qiyinchiliklarni jamoa bilan muhokama qilib, yechimlarni hujjatlashtirdim va hisobot tayyorladim.";

    public static DateOnly Today(this ApiFactory factory)
        => PracticeTime.LocalDate(factory.Services.GetRequiredService<IClock>().UtcNow);

    public static DateTimeOffset UtcNow(this ApiFactory factory)
        => factory.Services.GetRequiredService<IClock>().UtcNow;

    public static async Task<TutorScenario> CreateTutorScenarioAsync(this ApiFactory factory, bool approved = true)
    {
        var group = await factory.CreateGroupAsync();
        var tutor = await factory.CreateTutorAsync(group);
        var client = await factory.LoginAsync(tutor);
        var student = await factory.CreateStudentAsync(group: group);
        var period = await factory.CreateActivePeriodAsync(group, tutor.Id);
        var company = await factory.CreateCompanyAsync();
        var application = approved
            ? await factory.CreateApprovedApplicationAsync(student, period, company, tutor.Id)
            : await factory.CreateSubmittedApplicationAsync(student, period, company);

        return new TutorScenario(group, tutor, client, student, period, company, application);
    }

    /// <summary>Tyutor qarorini kutayotgan ariza (Submitted).</summary>
    public static Task<PracticeApplication> CreateSubmittedApplicationAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, Company company) =>
        factory.WithDbAsync(async db =>
        {
            var application = PracticeApplication.Create(
                student.Id, period.Id, company.Id, company.RadiusM, null, factory.UtcNow().AddHours(-5));
            db.PracticeApplications.Add(application);
            await db.SaveChangesAsync();
            return application;
        });

    /// <summary>O'tgan ish kunlari (Du–Sha, bayram emas, davr ichida) — bugundan orqaga, eng yaqini birinchi.</summary>
    public static async Task<List<DateOnly>> PastWorkDaysAsync(this ApiFactory factory, PracticePeriod period, int count)
    {
        var holidays = await factory.WithDbAsync(db => db.Holidays.AsNoTracking().ToListAsync());
        var today = factory.Today();
        var result = new List<DateOnly>();
        for (var date = today.AddDays(-1); date >= period.StartDate && result.Count < count; date = date.AddDays(-1))
        {
            if (period.IsWorkDay(date, holidays.Any(h => h.AppliesTo(date))))
                result.Add(date);
        }

        return result;
    }

    public static bool IsWorkDay(this PracticePeriod period, DateOnly date, IEnumerable<Holiday> holidays)
        => period.IsWorkDay(date, holidays.Any(h => h.AppliesTo(date)));

    public static Task<List<Holiday>> LoadHolidaysAsync(this ApiFactory factory)
        => factory.WithDbAsync(db => db.Holidays.AsNoTracking().ToListAsync());

    public static Task<DailyAttendance> AddAttendanceAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, DateOnly date,
        bool late = false, double distanceM = 25, bool checkOut = true, string? suspiciousReason = null) =>
        factory.WithDbAsync(async db =>
        {
            var checkInAt = PracticeTime.At(date, new TimeOnly(late ? 9 : 8, late ? 40 : 55));
            var attendance = DailyAttendance.CheckIn(student.Id, period.Id, date, checkInAt, distanceM, 12, CheckInVerdict.Accept(late));
            if (checkOut)
                attendance.CheckOut(PracticeTime.At(date, new TimeOnly(17, 5)), distanceM);
            if (suspiciousReason is not null)
                attendance.MarkSuspicious(suspiciousReason);
            db.DailyAttendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        });

    public static Task<AttendanceEvent> AddCheckInEventAsync(
        this ApiFactory factory, TestUser student, Company company, DateOnly date, double distanceM, bool accepted,
        double lat = 41.3111, double lng = 69.2797, TimeOnly? at = null) =>
        factory.WithDbAsync(async db =>
        {
            var when = PracticeTime.At(date, at ?? new TimeOnly(9, 2));
            var verdict = accepted ? CheckInVerdict.Accept() : CheckInVerdict.Reject(CheckInRejectReason.OutOfRadius);
            var attendanceEvent = AttendanceEvent.Record(
                student.Id, company.Id, date, AttendanceEventKind.CheckIn, when, when,
                new GeoPoint(lat, lng), 10, distanceM, company.RadiusM, verdict);
            db.AttendanceEvents.Add(attendanceEvent);
            await db.SaveChangesAsync();
            return attendanceEvent;
        });

    public static Task<DiaryEntry> AddDiaryAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, DateOnly date, Guid? reviewerId = null, int? score = null) =>
        factory.WithDbAsync(async db =>
        {
            var entry = DiaryEntry.Create(student.Id, period.Id, date, DiaryText, "Git bilan ishlash", PracticeTime.At(date, new TimeOnly(18, 30)));
            if (score is not null && reviewerId is not null)
                entry.Approve(reviewerId.Value, score, null, PracticeTime.At(date.AddDays(1), new TimeOnly(11, 0)));
            db.DiaryEntries.Add(entry);
            await db.SaveChangesAsync();
            return entry;
        });

    public static Task<LeaveRequest> AddLeaveRequestAsync(
        this ApiFactory factory, TestUser student, PracticePeriod period, DateOnly from, DateOnly to,
        string reason = "Kasallik — poliklinika spravkasi", string? attachmentName = "spravka.pdf") =>
        factory.WithDbAsync(async db =>
        {
            var leave = LeaveRequest.Create(student.Id, period.Id, from, to, reason, attachmentName);
            db.LeaveRequests.Add(leave);
            await db.SaveChangesAsync();
            return leave;
        });
}
