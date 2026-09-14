using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Features.Admin.Common;
using Amaliyotchi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Audit;

/// <summary><c>GET /api/admin/audit?q&amp;page&amp;pageSize&amp;action=</c> — <c>q</c>: entity nomi/id, sabab, foydalanuvchi ismi;
/// <c>action</c> — <see cref="AuditAction"/> (camelCase string, masalan <c>settingsChanged</c>). Yangisi birinchi.</summary>
public sealed record GetAuditLogQuery : PagedQuery, IRequest<Paged<AuditEntryDto>>
{
    public AuditAction? Action { get; init; }
}

internal sealed class GetAuditLogQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAuditLogQuery, Paged<AuditEntryDto>>
{
    public async Task<Paged<AuditEntryDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var logs = db.AuditLogs.AsNoTracking();

        if (request.Action is { } action)
            logs = logs.Where(a => a.Action == action);

        if (request.Q is { } q)
        {
            var pattern = AdminSearch.Pattern(q);
            logs = logs.Where(a =>
                EF.Functions.Like(a.EntityName.ToLower(), pattern, AdminSearch.Escape)
                || (a.EntityId != null && EF.Functions.Like(a.EntityId.ToLower(), pattern, AdminSearch.Escape))
                || (a.Reason != null && EF.Functions.Like(a.Reason.ToLower(), pattern, AdminSearch.Escape))
                || db.Users.Any(u => u.Id == a.UserId && EF.Functions.Like(u.FullName.ToLower(), pattern, AdminSearch.Escape)));
        }

        return await logs
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .SelectEntries(db)
            .ToPagedAsync(request, cancellationToken);
    }
}
