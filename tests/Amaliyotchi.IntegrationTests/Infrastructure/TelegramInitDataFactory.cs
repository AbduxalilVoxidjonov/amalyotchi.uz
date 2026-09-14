using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Telegram rasmiy algoritmi bo'yicha imzolangan <c>initData</c> yasaydi — validator va
/// <c>POST /api/auth/telegram</c> testlari uchun. Ishlab chiqarishdagi validator bilan bir xil formula,
/// lekin mustaqil yozilgan (validator o'z-o'zini tekshirmasin).</summary>
public static class TelegramInitDataFactory
{
    public static string Create(
        long telegramId,
        string botToken,
        DateTimeOffset authDate,
        string firstName = "Ali",
        string? username = "ali_student",
        string? hashOverride = null)
    {
        var userJson = username is null
            ? $$"""{"id":{{telegramId}},"first_name":"{{firstName}}","language_code":"uz"}"""
            : $$"""{"id":{{telegramId}},"first_name":"{{firstName}}","username":"{{username}}","language_code":"uz"}""";

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
            ["user"] = userJson,
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        };

        var hash = hashOverride ?? Sign(fields, botToken);

        var query = fields.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}").ToList();
        query.Add($"hash={hash}");
        return string.Join('&', query);
    }

    public static string Sign(SortedDictionary<string, string> fields, string botToken)
    {
        var dataCheckString = string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        return Convert.ToHexStringLower(hash);
    }
}
