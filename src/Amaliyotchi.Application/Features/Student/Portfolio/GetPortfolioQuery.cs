using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Time;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Grading;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Portfolio;

/// <summary><c>GET /api/student/portfolio</c>: davomat/kundalik statistikasi + <see cref="GradeCalculator"/> natijasi
/// (tyutor/tavsifnoma ballari <see cref="PracticeGrade"/> dan). Faol davr yo'q → 404. <c>pdfUrl</c> hozircha null.</summary>
public sealed record GetPortfolioQuery : IRequest<PortfolioDto>;

internal sealed class GetPortfolioQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<GetPortfolioQuery, PortfolioDto>
{
    public async Task<PortfolioDto> Handle(GetPortfolioQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");
        var today = clock.LocalToday();
        var localNow = clock.LocalTime();

        var practice = await db.LoadStudentPracticeAsync(userId, today, cancellationToken);
        var period = practice.Period ?? throw new NotFoundException("Faol amaliyot davri topilmadi.");

        var rows = await db.AttendanceInPeriodAsync(userId, period.Id, cancellationToken);
        var leaves = await db.ApprovedLeavesAsync(userId, period.Id, cancellationToken);
        var attendance = AttendanceCalendar.ComputeStats(practice, today, localNow, rows, leaves);

        var diaries = await db.DiaryEntries
            .AsNoTracking()
            .Where(d => d.StudentUserId == userId && d.PeriodId == period.Id)
            .ToListAsync(cancellationToken);
        var diary = AttendanceCalendar.ComputeDiaryStats(diaries);

        var grade = await db.PracticeGrades
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.StudentUserId == userId && g.PeriodId == period.Id, cancellationToken);

        var result = GradeCalculator.Compute(
            attendance.AttendancePct, diary.Count, diary.AvgScore, grade?.TutorPoints, grade?.ReferencePoints);

        PortfolioConclusionDto? conclusion = null;
        if (grade is { IsFinalized: true, Conclusion: not null })
        {
            var author = grade.FinalizedByUserId is { } byId
                ? await db.Users.AsNoTracking().Where(u => u.Id == byId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
                : null;
            conclusion = new PortfolioConclusionDto(grade.Conclusion, author ?? "Tyutor", grade.FinalizedAt!.Value);
        }

        return new PortfolioDto(
            practice.Student.User.FullName,
            practice.Student.Group.Name,
            period.Name,
            practice.Company?.Name,
            period.StartDate,
            period.EndDate,
            new PortfolioStatsDto(
                attendance.AttendancePct, attendance.DaysPresent, attendance.DaysTotal,
                attendance.Late, attendance.Excused, diary.Count, diary.AvgScore),
            [
                new PortfolioScoreDto("attendance", GradeThresholds.AttendanceWeight, result.AttendancePoints),
                new PortfolioScoreDto("reports", GradeThresholds.ReportWeight, result.ReportPoints),
                new PortfolioScoreDto("tutor", GradeThresholds.TutorWeight, result.TutorPoints),
                new PortfolioScoreDto("reference", GradeThresholds.ReferenceWeight, result.ReferencePoints)
            ],
            result.Total,
            result.Grade,
            grade?.IsFinalized ?? false,
            conclusion,
            PdfUrl: null);
    }
}
