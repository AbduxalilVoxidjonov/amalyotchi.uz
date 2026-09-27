using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Leave;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Bir kunlik davomat kesimi. <paramref name="Expected"/> — shu kun davomat kutilgan talabalar (guruhining
/// davri shu kuni davom etayotgan — yopilmagan, sanalar ichida — va kun ish kuni, bayram emas);
/// <paramref name="Excused"/> — sababli (qo'lda <c>Excused</c> yozuvi yoki yozuv yo'q va tasdiqlangan ruxsat);
/// <paramref name="WithDiary"/> — kelganlardan shu kun kundalik yozganlar.</summary>
public sealed record DayTally(int Expected, int Present, int Late, int Excused, int WithDiary)
{
    public static DayTally Empty { get; } = new(0, 0, 0, 0, 0);

    public int Attended => Present + Late;

    /// <summary>Kutilgan, lekin kelmagan va sababli bo'lmaganlar (hali belgilanmaganlar ham shu yerda).</summary>
    public int Absent => Math.Max(0, Expected - Attended - Excused);

    public int NoDiary => Math.Max(0, Attended - WithDiary);

    /// <summary>Loyiha formulasi (<c>StudentStatsCalculator.ComputeAttendance</c>): keldi / (jami − sababli).</summary>
    public int Pct => PracticeCalendar.AttendancePct(Attended, Expected, Excused);

    public DayTally Add(DayTally other)
        => new(Expected + other.Expected, Present + other.Present, Late + other.Late,
            Excused + other.Excused, WithDiary + other.WithDiary);
}

/// <summary>Admin dashboard va fakultetlar ro'yxati uchun kunlik davomat — YAGONA hisob (ikki sahifa bir xil foiz ko'rsatadi).
/// Talabalar to'plami — talabalar ro'yxati manbai (<see cref="GetAdminStudentsQueryHandler.Source"/>) ∩ shu kun davomat
/// kutilgan guruhlar (<see cref="PracticeCalendar.GroupsExpectedToday"/> / <see cref="PracticeCalendar.GroupsExpectedYesterday"/>).
/// Yopilgan yoki tugagan davr guruhlari bu to'plamga umuman kirmaydi — ular "kelmadi" bo'lib sanalmaydi.
/// Davomat/kundalik/ruxsat yozuvlari faqat guruhning kalendardagi davriga tegishlilari olinadi (boshqa davr aralashmaydi).
/// 3 ta so'rov (talabalar, davomat, ruxsat) + ixtiyoriy kundalik — talabalar jadvali bo'yicha subquery, N+1 yo'q.</summary>
internal static class DailyAttendanceTally
{
    public static async Task<IReadOnlyDictionary<Guid, DayTally>> LoadByFacultyAsync(
        IApplicationDbContext db,
        PracticeCalendar calendar,
        DateOnly date,
        IReadOnlyList<Guid> expectedGroupIds,
        IReadOnlyCollection<Guid>? facultyIds,
        bool withDiary,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, DayTally>();
        if (expectedGroupIds.Count == 0)
            return result;

        var groupIds = expectedGroupIds.ToList();
        var scoped = GetAdminStudentsQueryHandler.Source(db).Where(x => groupIds.Contains(x.Profile.StudentGroupId));
        if (facultyIds is not null)
        {
            var ids = facultyIds.ToList();
            scoped = scoped.Where(x => ids.Contains(x.Faculty.Id));
        }

        var students = await scoped
            .Select(x => new { x.Profile.UserId, x.Profile.StudentGroupId, FacultyId = x.Faculty.Id })
            .ToListAsync(cancellationToken);
        if (students.Count == 0)
            return result;

        var studentIds = scoped.Select(x => x.Profile.UserId);

        // (talaba, sana) unikal — talabaga ko'pi bilan bitta qator.
        var attendance = (await db.DailyAttendances.AsNoTracking()
                .Where(a => a.Date == date && studentIds.Contains(a.StudentUserId))
                .Select(a => new { a.StudentUserId, a.PeriodId, a.Status })
                .ToListAsync(cancellationToken))
            .ToDictionary(a => a.StudentUserId);

        var leaves = (await db.LeaveRequests.AsNoTracking()
                .Where(l => l.Status == LeaveRequestStatus.Approved && l.DateFrom <= date && l.DateTo >= date
                            && studentIds.Contains(l.StudentUserId))
                .Select(l => new { l.StudentUserId, l.PeriodId })
                .ToListAsync(cancellationToken))
            .Select(l => (l.StudentUserId, l.PeriodId))
            .ToHashSet();

        HashSet<(Guid StudentUserId, Guid PeriodId)> diaries = withDiary
            ? (await db.DiaryEntries.AsNoTracking()
                    .Where(d => d.Date == date && studentIds.Contains(d.StudentUserId))
                    .Select(d => new { d.StudentUserId, d.PeriodId })
                    .ToListAsync(cancellationToken))
                .Select(d => (d.StudentUserId, d.PeriodId))
                .ToHashSet()
            : [];

        foreach (var s in students)
        {
            if (calendar.For(s.StudentGroupId) is not { } practice)
                continue;

            var periodId = practice.PeriodId;
            var status = attendance.TryGetValue(s.UserId, out var row) && row.PeriodId == periodId ? row.Status : (AttendanceStatus?)null;

            var present = status == AttendanceStatus.Present ? 1 : 0;
            var late = status == AttendanceStatus.Late ? 1 : 0;
            // StudentStatsCalculator.ComputeAttendance bilan bir xil: Excused yozuvi yoki yozuv yo'q + tasdiqlangan ruxsat.
            var excused = status == AttendanceStatus.Excused || (status is null && leaves.Contains((s.UserId, periodId))) ? 1 : 0;
            var diary = present + late > 0 && diaries.Contains((s.UserId, periodId)) ? 1 : 0;

            var tally = new DayTally(1, present, late, excused, diary);
            result[s.FacultyId] = result.TryGetValue(s.FacultyId, out var acc) ? acc.Add(tally) : tally;
        }

        return result;
    }
}
