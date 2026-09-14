using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Settings;

/// <summary>Global sozlama: kalit (PK) + xom string qiymat. Tur/birlik/chegara <see cref="SettingKeys"/> da,
/// validatsiya kalit bo'yicha. <c>BaseEntity</c> emas — kalit o'zi identifikator.</summary>
public sealed class AppSetting
{
    public const int KeyMaxLength = 50;
    public const int ValueMaxLength = 200;

    private AppSetting() { }

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public SettingDefinition Definition => SettingKeys.Get(Key);

    public static AppSetting Create(string key, string value, DateTimeOffset at, Guid? byUserId = null)
    {
        var definition = SettingKeys.Get(key);
        return new AppSetting
        {
            Key = definition.Key,
            Value = definition.Validate(value),
            UpdatedAt = at,
            UpdatedByUserId = byUserId
        };
    }

    /// <summary>Standart qiymat bilan yaratadi (seed uchun).</summary>
    public static AppSetting CreateDefault(string key, DateTimeOffset at)
    {
        var definition = SettingKeys.Get(key);
        return Create(definition.Key, definition.DefaultValue, at);
    }

    /// <summary>Qiymatni o'zgartiradi. O'zgargan bo'lsa <c>true</c> — audit uchun.</summary>
    public bool Update(string value, DateTimeOffset at, Guid? byUserId)
    {
        var normalized = Definition.Validate(value);
        if (string.Equals(Value, normalized, StringComparison.Ordinal))
            return false;

        Value = normalized;
        UpdatedAt = at;
        UpdatedByUserId = byUserId;
        return true;
    }

    public int AsInt()
        => Definition.Type == SettingType.Int
            ? int.Parse(Value, System.Globalization.CultureInfo.InvariantCulture)
            : throw new DomainException($"'{Key}' butun son emas.");

    public bool AsBool()
        => Definition.Type == SettingType.Bool
            ? string.Equals(Value, "true", StringComparison.Ordinal)
            : throw new DomainException($"'{Key}' mantiqiy qiymat emas.");

    public Practice.WorkDays AsWorkDays()
        => Definition.Type == SettingType.Weekdays
            ? Practice.WorkDaysExtensions.Parse(Value)
            : throw new DomainException($"'{Key}' ish kunlari ro'yxati emas.");
}
