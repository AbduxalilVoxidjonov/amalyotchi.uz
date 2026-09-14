using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Admin.Settings;

/// <summary>GET va PUT bir xil javob qaytaradi — yuklash bitta joyda.
/// Bazada yo'q kalit (yangi <c>SettingKeys</c> qo'shilib, seed hali ishlamagan bo'lsa) default qiymat bilan chiqadi.</summary>
internal static class SettingsQueries
{
    public const string FileUrlPrefix = "/api/files/";

    public static async Task<AdminSettingsDto> LoadAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var stored = await db.AppSettings
            .AsNoTracking()
            .Select(s => new { s.Key, s.Value, s.UpdatedAt })
            .ToDictionaryAsync(s => s.Key, cancellationToken);

        var settings = SettingKeys.All
            .Select(d => stored.TryGetValue(d.Key, out var s)
                ? new SettingDto(d.Key, d.Label, s.Value, d.Type, d.Unit, d.Note, d.Min, d.Max, s.UpdatedAt)
                : new SettingDto(d.Key, d.Label, d.DefaultValue, d.Type, d.Unit, d.Note, d.Min, d.Max, null))
            .ToList();

        var holidays = await db.Holidays
            .AsNoTracking()
            .OrderBy(h => h.Date.Month).ThenBy(h => h.Date.Day).ThenBy(h => h.Date.Year)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name, h.IsRecurring))
            .ToListAsync(cancellationToken);

        var templates = await (from t in db.DocumentTemplates.AsNoTracking()
                               join f in db.StoredFiles on t.FileId equals f.Id
                               where t.IsActive
                               orderby t.Kind, t.Name
                               select new { t.Id, t.Name, t.Kind, f.FileName, FileId = f.Id })
            .ToListAsync(cancellationToken);

        return new AdminSettingsDto(
            settings,
            holidays,
            templates.Select(t => new DocTemplateDto(t.Id, t.Name, t.Kind, t.FileName, FileUrlPrefix + t.FileId)).ToList());
    }
}
