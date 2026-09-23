using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Practice;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Students;

/// <summary><c>GET /api/tutor/students/{id}?periodId=</c> — talaba profili: akademik ma'lumot, korxona, ariza,
/// amaliyot davri, davomat/kundalik statistikasi va baho — hammasi TANLANGAN davr bo'yicha.
/// <c>periodId</c> berilmasa — sukut bo'yicha davr (<see cref="PeriodPurpose.Default"/>: davom etayotgan → oxirgi
/// tugagan → eng yaqin kelgusi). Ko'lamdan tashqari talaba yoki talabaga tegishli bo'lmagan <c>periodId</c> → 404.</summary>
public sealed record GetTutorStudentDetailQuery(Guid Id, Guid? PeriodId = null) : IRequest<TutorStudentDetail>;

internal sealed class GetTutorStudentDetailQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver, IClock clock)
    : IRequestHandler<GetTutorStudentDetailQuery, TutorStudentDetail>
{
    public async Task<TutorStudentDetail> Handle(GetTutorStudentDetailQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        // Ko'lamdan tashqari (yoki umuman yo'q) talaba → NotFoundException (404).
        var profile = await db.GetScopedStudentAsync(scope, request.Id, cancellationToken);

        var periodSet = await db.LoadStudentPeriodsAsync(profile.UserId, profile.StudentGroupId, cancellationToken);
        var defaultPeriod = periodSet.Default(today);
        var selected = periodSet.Resolve(request.PeriodId, today);
        var period = selected is null ? null : await PeriodLookup.ContextAsync(db, selected, cancellationToken);
        var periodId = selected?.Id;

        var org = await db.StudentGroups.AsNoTracking()
            .Where(g => g.Id == profile.StudentGroupId)
            .Join(db.Directions, g => g.DirectionId, d => d.Id, (g, d) => new { Direction = d.Name, d.DepartmentId })
            .Join(db.Departments, x => x.DepartmentId, dep => dep.Id, (x, dep) => new { x.Direction, dep.FacultyId })
            .Join(db.Faculties, x => x.FacultyId, f => f.Id, (x, f) => new { x.Direction, Faculty = f.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var attendance = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.Id && a.PeriodId == periodId)
            .SelectSnapshot()
            .ToListAsync(cancellationToken);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.StudentUserId == request.Id && l.Status == LeaveRequestStatus.Approved && l.PeriodId == periodId)
                .Select(l => new { l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .Select(l => (l.DateFrom, l.DateTo))
            .ToList();

        var diaryScores = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(d => d.StudentUserId == request.Id && d.PeriodId == periodId)
            .Select(d => d.Score)
            .ToListAsync(cancellationToken);

        var applications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.Id && a.PeriodId == periodId)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new
            {
                a.Id,
                a.PeriodId,
                a.Status,
                a.SubmittedAt,
                a.DecidedAt,
                a.DecisionComment,
                a.ContractFileId,
                Company = new StudentCompany(
                    a.Company.Id, a.Company.Name, a.Company.Tin, a.Company.Activity, a.Company.Address,
                    a.Company.SupervisorName, a.Company.SupervisorPhone, a.Company.MentorName, a.Company.MentorPhone,
                    a.Company.Location.Latitude, a.Company.Location.Longitude, a.Company.RadiusM)
            })
            .ToListAsync(cancellationToken);

        // Tanlangan davrdagi ariza: tasdiqlangani (yoki yakunlangani) ustun, bo'lmasa eng oxirgisi.
        // Korxona faqat tasdiqlangan arizadan ko'rsatiladi.
        var placement = applications.FirstOrDefault(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.Completed);
        var current = placement ?? applications.FirstOrDefault();

        ApplicationContract? contract = null;
        if (current?.ContractFileId is { } fileId)
        {
            var file = await db.StoredFiles.AsNoTracking()
                .Where(f => f.Id == fileId)
                .Select(f => new { f.FileName, f.Pages, f.SizeBytes })
                .FirstOrDefaultAsync(cancellationToken);
            if (file is not null)
                contract = new ApplicationContract(file.FileName, file.Pages, file.SizeBytes, FileUrls.For(fileId));
        }

        var stats = StudentStatsCalculator.ComputeAttendance(period, attendance, leaves, today, localNow);
        var diary = StudentStatsCalculator.ComputeDiary(diaryScores);

        // Baho — faqat boshlangan davr uchun (rejalashtirilgan davrda hisoblanadigan narsa yo'q).
        StudentGrade? grade = null;
        if (period is not null && period.Period.StartDate <= today)
        {
            var points = await db.PracticeGrades.AsNoTracking().InScope(scope)
                .Where(g => g.StudentUserId == request.Id && g.PeriodId == period.Period.Id)
                .Select(g => new { g.TutorPoints, g.ReferencePoints })
                .FirstOrDefaultAsync(cancellationToken);

            var computed = GradeCalculator.Compute(
                stats.AttendancePct, diary.ScoredCount, diary.Avg, points?.TutorPoints, points?.ReferencePoints);
            grade = new StudentGrade(computed.Total, computed.Grade);
        }

        return new TutorStudentDetail(
            profile.UserId,
            profile.User.FullName,
            profile.HemisId,
            profile.Group.Name,
            profile.Group.Course,
            org?.Faculty ?? string.Empty,
            org?.Direction ?? string.Empty,
            profile.Status,
            profile.User.PhoneNumber,
            StudentStateRule.For(stats),
            stats.SuspiciousCount,
            placement?.Company,
            current is null
                ? null
                : new StudentApplication(current.Id, current.Status, current.SubmittedAt, current.DecidedAt, current.DecisionComment, contract),
            period is null ? null : ToPeriod(period.Period),
            new AttendanceSummary(
                stats.TotalDays,
                stats.AttendedDays,
                stats.LateDays,
                stats.ExcusedDays,
                stats.TotalDays - stats.AttendedDays,
                stats.SuspiciousCount,
                stats.AttendancePct),
            new DiarySummary(diary.Count, diary.ScoredCount, diary.Avg),
            grade,
            periodSet.Options(today, defaultPeriod?.Id),
            periodId);
    }

    private static StudentPeriod ToPeriod(PracticePeriod period)
        => new(
            period.Id,
            period.Name,
            period.StartDate,
            period.EndDate,
            PracticeTime.Hm(period.DailyStart),
            PracticeTime.Hm(period.DailyEnd),
            WorkDayNumbers.Of(period.WorkDays),
            period.RequiredDays);
}
