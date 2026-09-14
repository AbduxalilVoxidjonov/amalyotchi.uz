using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.ValueObjects;

/// <summary>STIR (INN) — yuridik shaxsning soliq to'lovchi raqami: aynan 9 ta raqam.
/// Bazada faqat raqamlar saqlanadi ("123 456 789" va "123456789" bir xil korxona).</summary>
public static class Tin
{
    public const int DigitCount = 9;

    public static string Normalize(string? tin)
        => TryNormalize(tin, out var normalized)
            ? normalized
            : throw new DomainException($"STIR {DigitCount} ta raqamdan iborat bo'lishi kerak. Namuna: 123456789");

    public static bool TryNormalize(string? tin, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(tin))
            return false;

        var cleaned = new string(tin.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        if (cleaned.Length != DigitCount || !cleaned.All(char.IsAsciiDigit))
            return false;

        normalized = cleaned;
        return true;
    }
}
