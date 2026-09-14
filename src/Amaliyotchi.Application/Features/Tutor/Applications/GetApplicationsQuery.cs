using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Scoping;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Tutor.Applications;

/// <summary><c>GET /api/tutor/applications?status=</c>. Yangi (submitted) arizalar birinchi — eng uzoq kutgani tepada.</summary>
public sealed record GetApplicationsQuery(ApplicationStatus? Status) : IRequest<ApplicationListResponse>;

internal sealed class GetApplicationsQueryHandler(IApplicationDbContext db, IScopeResolver scopeResolver)
    : IRequestHandler<GetApplicationsQuery, ApplicationListResponse>
{
    public async Task<ApplicationListResponse> Handle(GetApplicationsQuery request, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(cancellationToken);
        var scoped = db.PracticeApplications.AsNoTracking().InScope(scope);

        var countsByStatus = await scoped
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var counts = new ApplicationCounts(
            countsByStatus.GetValueOrDefault(ApplicationStatus.Submitted),
            countsByStatus.GetValueOrDefault(ApplicationStatus.RevisionNeeded),
            countsByStatus.GetValueOrDefault(ApplicationStatus.Approved),
            countsByStatus.GetValueOrDefault(ApplicationStatus.Rejected));

        var query = scoped;
        if (request.Status is { } status)
            query = query.Where(a => a.Status == status);

        var items = await query
            .Join(db.StudentProfiles, a => a.StudentUserId, p => p.UserId, (a, p) => new { a, p })
            .OrderBy(x => x.a.Status == ApplicationStatus.Submitted ? 0 : 1)
            .ThenBy(x => x.a.Status == ApplicationStatus.Submitted ? x.a.SubmittedAt : DateTimeOffset.MaxValue)
            .ThenByDescending(x => x.a.DecidedAt)
            .ThenByDescending(x => x.a.SubmittedAt)
            .Select(x => new ApplicationSummary(
                x.a.Id, x.a.StudentUserId, x.p.User.FullName, x.p.Group.Name, x.p.Group.Course, x.p.HemisId,
                x.a.Company.Name, x.a.Status, x.a.SubmittedAt, x.a.DecidedAt))
            .ToListAsync(cancellationToken);

        return new ApplicationListResponse(counts, items);
    }
}
