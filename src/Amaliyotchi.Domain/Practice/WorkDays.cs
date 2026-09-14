using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Practice;

/// <summary>Ish kunlari bitmask'i. Du=1 … Ya=64. Sozlamada "1,2,3,4,5,6" (1=Du … 7=Ya) ko'rinishida saqlanadi.</summary>
[Flags]
public enum WorkDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    MondayToFriday = Monday | Tuesday | Wednesday | Thursday | Friday,
    MondayToSaturday = MondayToFriday | Saturday
}

public static class WorkDaysExtensions
{
    public static bool Includes(this WorkDays days, DayOfWeek dayOfWeek) => (days & Of(dayOfWeek)) != 0;

    public static bool Includes(this WorkDays days, DateOnly date) => days.Includes(date.DayOfWeek);

    /// <summary><see cref="DayOfWeek"/> → bitmask bayrog'i (Yakshanba .NET'da 0, bizda 64).</summary>
    public static WorkDays Of(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => WorkDays.Monday,
        DayOfWeek.Tuesday => WorkDays.Tuesday,
        DayOfWeek.Wednesday => WorkDays.Wednesday,
        DayOfWeek.Thursday => WorkDays.Thursday,
        DayOfWeek.Friday => WorkDays.Friday,
        DayOfWeek.Saturday => WorkDays.Saturday,
        _ => WorkDays.Sunday
    };

    /// <summary>"1,2,3,4,5,6" (1=Du … 7=Ya) → bitmask. Bo'sh/noto'g'ri → DomainException.</summary>
    public static WorkDays Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new DomainException("Ish kunlari ro'yxati bo'sh bo'lishi mumkin emas.");

        var result = WorkDays.None;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var n) || n is < 1 or > 7)
                throw new DomainException("Ish kunlari 1 (Dushanba) dan 7 (Yakshanba) gacha bo'lgan raqamlar ro'yxati bo'lishi kerak.");
            result |= (WorkDays)(1 << (n - 1));
        }

        return result;
    }

    /// <summary>Bitmask → "1,2,3,4,5,6" (sozlama formati).</summary>
    public static string ToCsv(this WorkDays days)
        => string.Join(",", Enumerable.Range(1, 7).Where(n => (days & (WorkDays)(1 << (n - 1))) != 0));
}
