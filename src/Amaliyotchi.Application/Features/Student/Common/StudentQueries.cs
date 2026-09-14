using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Leave;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Talabaning o'z yozuvlari bo'yicha takrorlanadigan so'rovlar (hammasi <c>studentUserId</c> bilan cheklangan).</summary>
internal static class StudentQueries
{
    public static Task<List<DailyAttendance>> AttendanceInPeriodAsync(
        this IApplicationDbContext db, Guid studentUserId, Guid periodId, CancellationToken cancellationToken)
        => db.DailyAttendances
            .AsNoTracking()
            .Where(a => a.StudentUserId == studentUserId && a.PeriodId == periodId)
            .ToListAsync(cancellationToken);

    public static Task<List<LeaveRequest>> ApprovedLeavesAsync(
        this IApplicationDbContext db, Guid studentUserId, Guid periodId, CancellationToken cancellationToken)
        => db.LeaveRequests
            .AsNoTracking()
            .Where(l => l.StudentUserId == studentUserId && l.PeriodId == periodId && l.Status == LeaveRequestStatus.Approved)
            .ToListAsync(cancellationToken);

    public static Task<DailyAttendance?> AttendanceOnAsync(
        this IApplicationDbContext db, Guid studentUserId, DateOnly date, bool track, CancellationToken cancellationToken)
    {
        IQueryable<DailyAttendance> query = db.DailyAttendances;
        if (!track)
            query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(a => a.StudentUserId == studentUserId && a.Date == date, cancellationToken);
    }

    public static string FileUrl(Guid fileId) => $"/api/files/{fileId}";
}
