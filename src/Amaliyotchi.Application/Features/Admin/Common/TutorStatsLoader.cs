using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Practice;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary>Bitta tyutor bo'yicha hisoblangan ko'rsatkichlar (ro'yxat sahifasi va dashboard uchun bir xil).</summary>
public sealed record TutorStats(
    IReadOnlyList<string> Groups,
    int StudentCount,
    int PendingCount,
    DateTimeOffset? OldestPendingAt,
    double? AvgDecisionHours,
    DateTimeOffset? LastAuditAt)
{
    public static TutorStats Empty { get; } = new([], 0, 0, null, null, null);

    /// <summary>Kutilayotgan eng eski ariza chegaradan uzoq javobsiz → <c>late</c>.</summary>
    public TutorStatus Status(DateTimeOffset now)
        => OldestPendingAt is { } oldest && now - oldest > AdminThresholds.PendingApplicationLateAfter
            ? TutorStatus.Late
            : TutorStatus.Active;
}

/// <summary>Berilgan tyutorlar (bir sahifa) uchun 5 ta guruhlangan so'rov bilan statistika — N+1 emas:
/// faol biriktiruvlar → guruhlardagi talabalar soni → kutilayotgan arizalar (guruh bo'yicha) →
/// so'nggi qarorlar tezligi → so'nggi audit vaqti.</summary>
public static class TutorStatsLoader
{
    public static async Task<IReadOnlyDictionary<Guid, TutorStats>> LoadAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> tutorIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, TutorStats>();
        if (tutorIds.Count == 0)
            return result;

        var assignments = await db.TutorAssignments
            .AsNoTracking()
            .Where(a => a.IsActive && tutorIds.Contains(a.TutorUserId))
            .Select(a => new { a.TutorUserId, a.StudentGroupId, GroupName = a.Group.Name })
            .ToListAsync(cancellationToken);

        var groupIds = assignments.Select(a => a.StudentGroupId).Distinct().ToList();

        var studentsByGroup = groupIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.StudentProfiles
                .AsNoTracking()
                .Where(p => groupIds.Contains(p.StudentGroupId))
                .GroupBy(p => p.StudentGroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        var pendingByGroup = groupIds.Count == 0
            ? new Dictionary<Guid, (int Count, DateTimeOffset Oldest)>()
            : await (from app in db.PracticeApplications.AsNoTracking()
                     join p in db.StudentProfiles on app.StudentUserId equals p.UserId
                     where app.Status == ApplicationStatus.Submitted && groupIds.Contains(p.StudentGroupId)
                     group app by p.StudentGroupId into g
                     select new { GroupId = g.Key, Count = g.Count(), Oldest = g.Min(a => a.SubmittedAt) })
                .ToDictionaryAsync(x => x.GroupId, x => (x.Count, x.Oldest), cancellationToken);

        var decisionsSince = now - AdminThresholds.DecisionSpeedWindow;
        var decisions = await db.PracticeApplications
            .AsNoTracking()
            .Where(a => a.DecidedByUserId != null && tutorIds.Contains(a.DecidedByUserId.Value)
                        && a.DecidedAt != null && a.DecidedAt >= decisionsSince)
            .Select(a => new { TutorId = a.DecidedByUserId!.Value, a.SubmittedAt, DecidedAt = a.DecidedAt!.Value })
            .ToListAsync(cancellationToken);

        var avgHours = decisions
            .GroupBy(d => d.TutorId)
            .ToDictionary(g => g.Key, g => g.Average(d => (d.DecidedAt - d.SubmittedAt).TotalHours));

        var lastAudit = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId != null && tutorIds.Contains(a.UserId.Value))
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { TutorId = g.Key, Last = g.Max(a => a.OccurredAt) })
            .ToDictionaryAsync(x => x.TutorId, x => x.Last, cancellationToken);

        foreach (var tutorId in tutorIds)
        {
            var own = assignments.Where(a => a.TutorUserId == tutorId).ToList();
            var ownGroupIds = own.Select(a => a.StudentGroupId).Distinct().ToList();

            var pending = ownGroupIds
                .Select(id => pendingByGroup.GetValueOrDefault(id))
                .Where(p => p.Count > 0)
                .ToList();

            result[tutorId] = new TutorStats(
                own.Select(a => a.GroupName).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                ownGroupIds.Sum(id => studentsByGroup.GetValueOrDefault(id)),
                pending.Sum(p => p.Count),
                pending.Count == 0 ? null : pending.Min(p => p.Oldest),
                avgHours.TryGetValue(tutorId, out var hours) ? Math.Round(hours, 1) : null,
                lastAudit.TryGetValue(tutorId, out var last) ? last : null);
        }

        return result;
    }
}
