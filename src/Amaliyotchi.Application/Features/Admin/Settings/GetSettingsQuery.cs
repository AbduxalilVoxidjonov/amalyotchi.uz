using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary><c>GET /api/admin/settings</c>.</summary>
public sealed record GetSettingsQuery : IRequest<AdminSettingsDto>;

internal sealed class GetSettingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSettingsQuery, AdminSettingsDto>
{
    public Task<AdminSettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
        => SettingsQueries.LoadAsync(db, cancellationToken);
}
