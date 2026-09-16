using Amaliyotchi.Domain.Exceptions;
using Amaliyotchi.Domain.Practice;

namespace Amaliyotchi.Domain.Settings;

/// <summary>Bitta sozlama kalitining ta'rifi: tur, birlik, chegaralar, standart qiymat.</summary>
/// <param name="Unit">Kontrakt bo'yicha: "m", "min", "chars" yoki null.</param>
public sealed record SettingDefinition(
    string Key,
    string Label,
    SettingType Type,
    string? Unit,
    string Note,
    string DefaultValue,
    int? Min = null,
    int? Max = null)
{
    /// <summary>Xom qiymatni tekshiradi va bazaga yoziladigan ko'rinishga keltiradi.</summary>
    public string Validate(string? value)
    {
        var raw = value?.Trim();
        if (string.IsNullOrEmpty(raw))
            throw new DomainException($"'{Key}' sozlamasi bo'sh bo'lishi mumkin emas.");

        switch (Type)
        {
            case SettingType.Int:
                if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    throw new DomainException($"'{Key}' butun son bo'lishi kerak.");
                if ((Min is not null && n < Min) || (Max is not null && n > Max))
                    throw new DomainException($"'{Key}' {Min}–{Max} oralig'ida bo'lishi kerak.");
                return n.ToString(System.Globalization.CultureInfo.InvariantCulture);

            case SettingType.Bool:
                return raw.ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" or "ha" => "true",
                    "false" or "0" or "no" or "yo'q" => "false",
                    _ => throw new DomainException($"'{Key}' true yoki false bo'lishi kerak.")
                };

            case SettingType.Weekdays:
                return WorkDaysExtensions.Parse(raw).ToCsv();

            default:
                throw new DomainException($"'{Key}' sozlamasining turi noma'lum.");
        }
    }
}

/// <summary>Global sozlamalar kalitlari va ularning ta'riflari. Kontraktdagi <c>SettingKey</c> bilan bir xil nomlar.</summary>
public static class SettingKeys
{
    public const string GeofenceRadius = "geofenceRadius";
    public const string LateTolerance = "lateTolerance";
    public const string MinGpsAccuracy = "minGpsAccuracy";
    public const string AutoCheckout = "autoCheckout";
    public const string WorkDays = "workDays";
    public const string DailyReportRequired = "dailyReportRequired";
    public const string MinReportLength = "minReportLength";
    public const string CheckInWindow = "checkInWindow";
    public const string CheckInPhotoRequired = "checkinPhotoRequired";
    public const string MaxStudentsPerCompany = "maxStudentsPerCompany";

    public const string UnitMeters = "m";
    public const string UnitMinutes = "min";
    public const string UnitChars = "chars";

    /// <summary>Kontraktdagi tartibda.</summary>
    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new(GeofenceRadius, "Standart geofence radiusi", SettingType.Int, UnitMeters,
            "Yangi korxona uchun standart radius; tyutor har korxona uchun o'zgartira oladi.", "200", 50, 1000),
        new(LateTolerance, "Kechikish chegarasi", SettingType.Int, UnitMinutes,
            "Ish boshlanishidan shuncha daqiqa o'tgach check-in \"kech keldi\" bo'ladi.", "15", 0, 120),
        new(MinGpsAccuracy, "Minimal GPS aniqligi", SettingType.Int, UnitMeters,
            "Aniqlik shundan yomon bo'lsa check-in qabul qilinmaydi.", "100", 10, 1000),
        new(AutoCheckout, "Avtomatik check-out", SettingType.Int, UnitMinutes,
            "Ish tugagach shuncha daqiqa ichida check-out qilinmasa kun avtomatik yopiladi.", "60", 0, 360),
        new(WorkDays, "Ish kunlari", SettingType.Weekdays, null,
            "1 — Dushanba … 7 — Yakshanba.", "1,2,3,4,5,6"),
        new(DailyReportRequired, "Kundalik hisobot majburiy", SettingType.Bool, null,
            "Yoqilgan bo'lsa hisobotsiz kun \"tugallanmagan\" hisoblanadi.", "true"),
        new(MinReportLength, "Hisobotning minimal uzunligi", SettingType.Int, UnitChars,
            "Shundan qisqa hisobot qabul qilinmaydi.", "150", 0, 5000),
        new(CheckInWindow, "Check-in oynasi", SettingType.Int, UnitMinutes,
            "Ish boshlanishidan shuncha daqiqa o'tgach check-in yopiladi va talaba \"kelmadi\" bo'ladi.", "90", 15, 480),
        new(CheckInPhotoRequired, "Check-in uchun rasm majburiy", SettingType.Bool, null,
            "Yoqilgan bo'lsa check-in/check-out so'roviga selfi biriktirilmasa urinish qabul qilinmaydi.", "false"),
        new(MaxStudentsPerCompany, "Korxonaga maksimal talaba", SettingType.Int, null,
            "Shu sondan ko'p talaba bitta korxonaga biriktirilsa ogohlantirish chiqadi.", "10", 1, 200)
    ];

    private static readonly Dictionary<string, SettingDefinition> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.Ordinal);

    public static bool IsKnown(string key) => ByKey.ContainsKey(key);

    public static SettingDefinition Get(string key)
        => ByKey.TryGetValue(key, out var definition)
            ? definition
            : throw new DomainException($"Noma'lum sozlama kaliti: '{key}'.");

    public static bool TryGet(string key, out SettingDefinition definition)
        => ByKey.TryGetValue(key, out definition!);
}
