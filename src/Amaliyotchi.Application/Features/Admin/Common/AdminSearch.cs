namespace Amaliyotchi.Application.Features.Admin.Common;

/// <summary><c>?q=</c> qidiruvi uchun LIKE naqshi. Application qatlami Npgsql'ga bog'lanmagan (<c>EF.Functions.ILike</c> yo'q),
/// shuning uchun <c>EF.Functions.Like(ustun.ToLower(), naqsh, "\\")</c> ishlatiladi — Postgres'da <c>lower(x) LIKE @p ESCAPE '\'</c>,
/// ya'ni case-insensitive. Foydalanuvchi kiritgan <c>%</c>, <c>_</c>, <c>\</c> belgilari ekranlanadi.</summary>
public static class AdminSearch
{
    public const string Escape = "\\";

    public static string Pattern(string q)
    {
        var escaped = q.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .ToLowerInvariant();
        return $"%{escaped}%";
    }
}
