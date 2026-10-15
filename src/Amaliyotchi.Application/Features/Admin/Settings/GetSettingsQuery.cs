using Amaliyotchi.Application.Common.Caching;
using Amaliyotchi.Application.Common.Interfaces;
using MediatR;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary><c>GET /api/admin/settings</c>.</summary>
public sealed record GetSettingsQuery : IRequest<AdminSettingsDto>;

internal sealed class GetSettingsQueryHandler(IApplicationDbContext db, IAppCache cache)
    : IRequestHandler<GetSettingsQuery, AdminSettingsDto>
{
    private static readonly string[] Tags = [CacheTags.Settings];

    /// <summary>Keshlanadi (5 daq.); sozlama/bayram/shablon yozuvi <see cref="CacheTags.Settings"/> ni darhol bekor qiladi.</summary>
    public async Task<AdminSettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync(
            CacheKeys.AdminSettings,
            ct => new ValueTask<AdminSettingsDto>(SettingsQueries.LoadAsync(db, ct)),
            CacheKeys.SettingsTtl, Tags, cancellationToken);
}
