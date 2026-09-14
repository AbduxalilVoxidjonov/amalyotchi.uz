using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Application.Features.Tutor.Common;
using Amaliyotchi.Domain.Attendance;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.LeaveRequests;

/// <summary><c>POST /api/tutor/leave-requests/{id}/decision</c>. Tasdiqlashda oraliqdagi har ish kuni "sababli" bo'ladi:
/// qator bo'lsa <c>MarkExcused</c>, bo'lmasa <c>DailyAttendance.Excuse</c>. Hal qilingan → 409 (Domain).</summary>
public sealed record DecideLeaveRequestCommand(Guid LeaveRequestId, LeaveDecision Decision, string? Comment)
    : IRequest<TutorLeaveRequest>;

internal sealed class DecideLeaveRequestCommandHandler(
    IApplicationDbContext db,
    IScopeResolver scopeResolver,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IClock clock)
    : IRequestHandler<DecideLeaveRequestCommand, TutorLeaveRequest>
{
    public async Task<TutorLeaveRequest> Handle(DecideLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var userId = currentUser.UserId ?? throw new ForbiddenException();
        var now = clock.UtcNow;

        var leave = await db.LeaveRequests
            .InScope(scope)
            .FirstOrDefaultAsync(l => l.Id == request.LeaveRequestId, cancellationToken)
            ?? throw new NotFoundException("Ruxsat so'rovi", request.LeaveRequestId);

        if (request.Decision == LeaveDecision.Approve)
        {
            leave.Approve(userId, request.Comment, now);
            await ExcuseDaysAsync(leave, cancellationToken);
            await audit.WriteAsync(
                AuditAction.LeaveApproved, nameof(LeaveRequest), leave.Id.ToString(),
                reason: request.Comment, cancellationToken: cancellationToken);
        }
        else
        {
            leave.Reject(userId, request.Comment, now);
            await audit.WriteAsync(
                AuditAction.LeaveRejected, nameof(LeaveRequest), leave.Id.ToString(),
                reason: request.Comment, cancellationToken: cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.LeaveRequests.AsNoTracking()
            .Where(l => l.Id == leave.Id)
            .SelectTutorRequestsAsync(db, cancellationToken);
        return updated.Single();
    }

    private async Task ExcuseDaysAsync(LeaveRequest leave, CancellationToken cancellationToken)
    {
        var period = await PeriodLookup.LoadPeriodAsync(db, leave.PeriodId, cancellationToken);
        if (period is null)
            return;

        var existing = await db.DailyAttendances
            .Where(a => a.StudentUserId == leave.StudentUserId && a.Date >= leave.DateFrom && a.Date <= leave.DateTo)
            .ToDictionaryAsync(a => a.Date, cancellationToken);

        foreach (var date in period.WorkDays(leave.DateFrom, leave.DateTo))
        {
            if (existing.TryGetValue(date, out var row))
                row.MarkExcused(leave.Id);
            else
                db.DailyAttendances.Add(DailyAttendance.Excuse(leave.StudentUserId, leave.PeriodId, date, leave.Id));
        }
    }
}
