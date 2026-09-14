using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.ValueObjects;

/// <summary>HEMIS talaba identifikatori: faqat raqamlar, 5–20 belgi (HEMIS'da odatda 12 xonali,
/// eski import'larda 6 xonali). Bazada boshidagi/oxiridagi bo'shliqlarsiz saqlanadi.</summary>
public static class HemisId
{
    public const int MinLength = 5;
    public const int MaxLength = 20;

    public static string Normalize(string? hemisId)
        => TryNormalize(hemisId, out var normalized)
            ? normalized
            : throw new DomainException($"HEMIS ID {MinLength}–{MaxLength} ta raqamdan iborat bo'lishi kerak.");

    public static bool TryNormalize(string? hemisId, out string normalized)
    {
        normalized = string.Empty;
        var trimmed = hemisId?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;
        if (trimmed.Length is < MinLength or > MaxLength || !trimmed.All(char.IsAsciiDigit))
            return false;

        normalized = trimmed;
        return true;
    }
}
