using System.Text.Json;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Domain.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary><c>PUT /api/admin/settings</c>: <c>{ values: { key: "qiymat" } }</c>. Kalitlar va qiymatlar
/// <see cref="UpdateSettingsCommandValidator"/> da <see cref="SettingDefinition.Validate"/> orqali tekshiriladi (400 <c>errors.&lt;key&gt;</c>).
/// Haqiqatan o'zgargan kalitlar audit'ga <see cref="AuditAction.SettingsChanged"/> sifatida (<c>{key:{old,new}}</c>) yoziladi.</summary>
public sealed record UpdateSettingsCommand(IReadOnlyDictionary<string, string?> Values) : IRequest<AdminSettingsDto>;

internal sealed class UpdateSettingsCommandHandler(
    IApplicationDbContext db,
    IAuditWriter audit,
    IClock clock,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateSettingsCommand, AdminSettingsDto>
{
    public async Task<AdminSettingsDto> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var keys = request.Values.Keys.ToList();

        var existing = await db.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, cancellationToken);

        var changes = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (key, value) in request.Values)
        {
            if (existing.TryGetValue(key, out var setting))
            {
                var old = setting.Value;
                if (setting.Update(value!, now, currentUser.UserId))
                    changes[key] = new { old, @new = setting.Value };
                continue;
            }

            // Seed hali yaratmagan kalit — default o'rniga darhol kiritilgan qiymat bilan yaratiladi.
            var created = AppSetting.Create(key, value!, now, currentUser.UserId);
            db.AppSettings.Add(created);
            changes[key] = new { old = SettingKeys.Get(key).DefaultValue, @new = created.Value };
        }

        if (changes.Count > 0)
        {
            await audit.WriteAsync(
                AuditAction.SettingsChanged, nameof(AppSetting),
                changes: JsonSerializer.Serialize(changes),
                cancellationToken: cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
        }

        return await SettingsQueries.LoadAsync(db, cancellationToken);
    }
}
