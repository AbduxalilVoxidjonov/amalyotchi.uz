using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.LeaveRequests;

/// <summary><c>GET /api/student/leave-requests</c> — faqat o'z so'rovlari, yangisi birinchi.</summary>
public sealed record GetStudentLeaveRequestsQuery : IRequest<IReadOnlyList<LeaveRequestDto>>;

internal sealed class GetStudentLeaveRequestsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetStudentLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetStudentLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("Avtorizatsiya talab qilinadi.");

        var items = await db.LeaveRequests
            .AsNoTracking()
            .Where(l => l.StudentUserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(LeaveRequestMapping.ToDto).ToList();
    }
}

internal static class LeaveRequestMapping
{
    public static LeaveRequestDto ToDto(LeaveRequest request)
    {
        LeaveDocumentDto? document = null;
        if (request.DocumentFileId is { } fileId)
            document = new LeaveDocumentDto(request.AttachmentName ?? "hujjat", StudentQueries.FileUrl(fileId));
        else if (request.AttachmentName is not null)
            document = new LeaveDocumentDto(request.AttachmentName, null);

        return new LeaveRequestDto(
            request.Id,
            request.DateFrom,
            request.DateTo,
            request.Reason,
            request.Status,
            request.DecisionComment,
            document,
            request.CreatedAt);
    }
}
