using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.ValueObjects;

/// <summary>O'zbekiston telefon raqami. Bazada doimo bitta ko'rinishda saqlanadi:
/// +998901234567 — aks holda "90 123 45 67" va "+998901234567" ikki xil foydalanuvchi bo'lib qoladi.</summary>
public static class PhoneNumber
{
    public const int DigitCount = 12;
    private const string CountryCode = "998";

    public static string Normalize(string? phone)
        => TryNormalize(phone, out var normalized)
            ? normalized
            : throw new DomainException("Telefon raqami noto'g'ri. Namuna: +998901234567");

    public static bool TryNormalize(string? phone, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 9)
            digits = CountryCode + digits;

        if (digits.Length != DigitCount || !digits.StartsWith(CountryCode, StringComparison.Ordinal))
            return false;

        normalized = "+" + digits;
        return true;
    }
}
