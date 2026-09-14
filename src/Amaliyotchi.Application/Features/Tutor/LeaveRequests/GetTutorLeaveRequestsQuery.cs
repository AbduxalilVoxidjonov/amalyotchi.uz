using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.LeaveRequests;

/// <summary><c>GET /api/tutor/leave-requests?status=</c> — kutilayotganlar birinchi, keyin yangisi tepada.</summary>
public sealed record GetTutorLeaveRequestsQuery(LeaveRequestStatus? Status) : IRequest<IReadOnlyList<TutorLeaveRequest>>;

internal sealed class GetTutorLeaveRequestsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetTutorLeaveRequestsQuery, IReadOnlyList<TutorLeaveRequest>>
{
    public async Task<IReadOnlyList<TutorLeaveRequest>> Handle(GetTutorLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);

        var query = db.LeaveRequests.AsNoTracking().InScope(scope);
        if (request.Status is { } status)
            query = query.Where(l => l.Status == status);

        return await query
            .OrderBy(l => l.Status == LeaveRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(l => l.CreatedAt)
            .SelectTutorRequestsAsync(db, cancellationToken);
    }
}
