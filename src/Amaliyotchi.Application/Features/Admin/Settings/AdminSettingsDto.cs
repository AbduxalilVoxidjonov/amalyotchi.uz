using Amaliyotchi.Domain.Settings;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary>Kontrakt v2 <c>AdminSettings</c>: sozlamalar (xom <c>value</c> + <c>type</c>/<c>unit</c>, formatlash frontend'da),
/// bayramlar (ISO sana + <c>isRecurring</c>), hujjat shablonlari (<c>url</c> — <c>GET /api/files/{id}</c>).</summary>
public sealed record AdminSettingsDto(
    IReadOnlyList<SettingDto> Settings,
    IReadOnlyList<HolidayDto> Holidays,
    IReadOnlyList<DocTemplateDto> Templates);

/// <summary><paramref name="Value"/> xom: <c>"200"</c>, <c>"true"</c>, <c>"1,2,3,4,5,6"</c>.
/// <paramref name="Min"/>/<paramref name="Max"/> faqat <c>int</c> turida.</summary>
public sealed record SettingDto(
    string Key,
    string Label,
    string Value,
    SettingType Type,
    string? Unit,
    string Note,
    int? Min,
    int? Max,
    DateTimeOffset? UpdatedAt);

public sealed record HolidayDto(Guid Id, DateOnly Date, string Name, bool IsRecurring);

public sealed record DocTemplateDto(Guid Id, string Name, DocumentTemplateKind Kind, string FileName, string Url);

/// <summary><c>PUT /api/admin/settings</c> tanasi: faqat o'zgargan kalitlar (<c>{ values: { geofenceRadius: "250" } }</c>).</summary>
public sealed record SettingsUpdateDto(IReadOnlyDictionary<string, string?> Values);
