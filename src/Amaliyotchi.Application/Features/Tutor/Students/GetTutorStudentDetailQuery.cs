using Amaliyotchi.Application.Common.Interfaces;
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

/// <summary><c>GET /api/tutor/students/{id}</c> — talaba profili: akademik ma'lumot, korxona, ariza,
/// amaliyot davri, davomat/kundalik statistikasi va joriy baho. Ko'lamdan tashqari talaba → 404.</summary>
public sealed record GetTutorStudentDetailQuery(Guid Id) : IRequest<TutorStudentDetail>;

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

        var periods = await PeriodLookup.LoadAsync(db, today, cancellationToken);
        var period = periods.ForGroup(profile.StudentGroupId);
        var periodIds = periods.PeriodIds;

        var org = await db.StudentGroups.AsNoTracking()
            .Where(g => g.Id == profile.StudentGroupId)
            .Join(db.Directions, g => g.DirectionId, d => d.Id, (g, d) => new { Direction = d.Name, d.DepartmentId })
            .Join(db.Departments, x => x.DepartmentId, dep => dep.Id, (x, dep) => new { x.Direction, dep.FacultyId })
            .Join(db.Faculties, x => x.FacultyId, f => f.Id, (x, f) => new { x.Direction, Faculty = f.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var attendance = await db.DailyAttendances.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.Id && periodIds.Contains(a.PeriodId))
            .SelectSnapshot()
            .ToListAsync(cancellationToken);

        var leaves = (await db.LeaveRequests.AsNoTracking().InScope(scope)
                .Where(l => l.StudentUserId == request.Id && l.Status == LeaveRequestStatus.Approved && periodIds.Contains(l.PeriodId))
                .Select(l => new { l.DateFrom, l.DateTo })
                .ToListAsync(cancellationToken))
            .Select(l => (l.DateFrom, l.DateTo))
            .ToList();

        var diaryScores = await db.DiaryEntries.AsNoTracking().InScope(scope)
            .Where(d => d.StudentUserId == request.Id && periodIds.Contains(d.PeriodId))
            .Select(d => d.Score)
            .ToListAsync(cancellationToken);

        var applications = await db.PracticeApplications.AsNoTracking().InScope(scope)
            .Where(a => a.StudentUserId == request.Id)
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

        // Joriy ariza: faol davrniki, bo'lmasa — eng oxirgisi. Korxona faqat tasdiqlangan arizadan ko'rsatiladi.
        var current = (period is not null ? applications.FirstOrDefault(a => a.PeriodId == period.Period.Id) : null)
                      ?? applications.FirstOrDefault();
        var placement = applications.FirstOrDefault(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.Completed);

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

        StudentGrade? grade = null;
        if (period is not null)
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
            grade);
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
