using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Diary;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Grading;
using Amaliyotchi.Domain.Leave;
using Amaliyotchi.Domain.Practice;
using Amaliyotchi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>So'rovlarni ko'lamga toraytiruvchi kengaytmalar. Global query filter EMAS — ko'lam
/// bir nechta jadval orqali aniqlanadi; har handler o'zi <c>.InScope(scope)</c> ni chaqiradi.
/// Ko'lamdan tashqaridagi yozuv "yo'q" ko'rinadi (404, 403 emas — mavjudligi oshkor qilinmaydi).</summary>
public static class ScopeQueries
{
    public static IQueryable<StudentProfile> InScope(this IQueryable<StudentProfile> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(p => scope.StudentGroupIds.Contains(p.StudentGroupId)),
            ScopeKind.Self => query.Where(p => p.UserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<DailyAttendance> InScope(this IQueryable<DailyAttendance> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(a => scope.StudentUserIds.Contains(a.StudentUserId)),
            ScopeKind.Self => query.Where(a => a.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<AttendanceEvent> InScope(this IQueryable<AttendanceEvent> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(e => scope.StudentUserIds.Contains(e.StudentUserId)),
            ScopeKind.Self => query.Where(e => e.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<DiaryEntry> InScope(this IQueryable<DiaryEntry> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(d => scope.StudentUserIds.Contains(d.StudentUserId)),
            ScopeKind.Self => query.Where(d => d.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<LeaveRequest> InScope(this IQueryable<LeaveRequest> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(l => scope.StudentUserIds.Contains(l.StudentUserId)),
            ScopeKind.Self => query.Where(l => l.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<PracticeGrade> InScope(this IQueryable<PracticeGrade> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(g => scope.StudentUserIds.Contains(g.StudentUserId)),
            ScopeKind.Self => query.Where(g => g.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    public static IQueryable<PracticeApplication> InScope(this IQueryable<PracticeApplication> query, DataScope scope)
        => scope.Kind switch
        {
            ScopeKind.Unrestricted => query,
            ScopeKind.Groups => query.Where(a => scope.StudentUserIds.Contains(a.StudentUserId)),
            ScopeKind.Self => query.Where(a => a.StudentUserId == scope.UserId),
            _ => query.Where(_ => false)
        };

    /// <summary>Ko'lamdagi talaba profili (User va Group bilan). Topilmasa — <see cref="NotFoundException"/>.</summary>
    public static async Task<StudentProfile> GetScopedStudentAsync(
        this IApplicationDbContext db, DataScope scope, Guid studentUserId, CancellationToken cancellationToken = default)
        => await db.StudentProfiles
               .Include(p => p.User)
               .Include(p => p.Group)
               .InScope(scope)
               .FirstOrDefaultAsync(p => p.UserId == studentUserId, cancellationToken)
           ?? throw new NotFoundException("Talaba", studentUserId);
}
