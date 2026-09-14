using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Leave;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.LeaveRequests;

public enum LeaveDecision
{
    Approve = 1,
    Reject = 2
}

/// <summary>Hujjat: fayl yuklangan bo'lsa <c>url</c>, aks holda faqat nom (<c>url: null</c>).</summary>
public sealed record LeaveDocument(string Name, string? Url);

public sealed record TutorLeaveRequest(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Group,
    DateOnly DateFrom,
    DateOnly DateTo,
    string Reason,
    LeaveDocument? Document,
    LeaveRequestStatus Status,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt);

internal static class LeaveRequestQueries
{
    public static async Task<List<TutorLeaveRequest>> SelectTutorRequestsAsync(
        this IQueryable<LeaveRequest> requests, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var rows = await requests
            .Join(db.StudentProfiles, l => l.StudentUserId, p => p.UserId, (l, p) => new
            {
                l.Id,
                l.StudentUserId,
                StudentName = p.User.FullName,
                GroupName = p.Group.Name,
                l.DateFrom,
                l.DateTo,
                l.Reason,
                l.AttachmentName,
                l.DocumentFileId,
                l.Status,
                l.DecisionComment,
                l.CreatedAt,
                l.DecidedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TutorLeaveRequest(
                r.Id, r.StudentUserId, r.StudentName, r.GroupName, r.DateFrom, r.DateTo, r.Reason,
                r.AttachmentName is null && r.DocumentFileId is null
                    ? null
                    : new LeaveDocument(r.AttachmentName ?? "hujjat", r.DocumentFileId is { } fileId ? FileUrls.For(fileId) : null),
                r.Status, r.DecisionComment, r.CreatedAt, r.DecidedAt))
            .ToList();
    }
}
