using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Amaliyotchi.Infrastructure.Identity;

/// <summary>Telegram Mini App initData tekshiruvi — rasmiy algoritm
/// (https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app):
/// <c>secret = HMAC_SHA256(key: "WebAppData", msg: bot_token)</c>,
/// <c>hash == hex(HMAC_SHA256(key: secret, msg: data_check_string))</c>, bunda data_check_string —
/// <c>hash</c> dan tashqari barcha maydonlar kalit bo'yicha tartiblangan <c>key=value</c> qatorlari, <c>\n</c> bilan.</summary>
public sealed class TelegramInitDataValidator(IOptions<TelegramOptions> options) : ITelegramInitDataValidator
{
    private const string HashKey = "hash";
    private const string AuthDateKey = "auth_date";
    private const string UserKey = "user";

    private static readonly byte[] WebAppDataKey = "WebAppData"u8.ToArray();

    private readonly TelegramOptions _options = options.Value;

    public bool TryValidate(string initData, DateTimeOffset now, out TelegramInitUser? user, out string? error)
    {
        user = null;

        if (string.IsNullOrWhiteSpace(initData))
            return Fail("initData bo'sh", out error);

        if (string.IsNullOrWhiteSpace(_options.BotToken))
            return Fail("Telegram:BotToken sozlanmagan", out error);

        var fields = Parse(initData);

        if (!fields.Remove(HashKey, out var providedHash) || string.IsNullOrEmpty(providedHash))
            return Fail("hash maydoni yo'q", out error);

        if (!fields.TryGetValue(AuthDateKey, out var authDateRaw)
            || !long.TryParse(authDateRaw, out var authDateUnix))
            return Fail("auth_date maydoni yo'q yoki noto'g'ri", out error);

        var expectedHash = ComputeHash(fields);
        var providedBytes = TryFromHex(providedHash);
        if (providedBytes is null || !CryptographicOperations.FixedTimeEquals(expectedHash, providedBytes))
            return Fail("imzo mos kelmadi", out error);

        // Imzo to'g'ri — endi yoshi. Kelajakdagi auth_date (soat farqi) ham qabul qilinmaydi.
        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        var age = now - authDate;
        if (age > TimeSpan.FromSeconds(_options.MaxAgeSeconds))
            return Fail("auth_date eskirgan", out error);
        if (age < -TimeSpan.FromMinutes(5))
            return Fail("auth_date kelajakda", out error);

        if (!fields.TryGetValue(UserKey, out var userJson) || !TryParseUser(userJson, authDate, out user))
            return Fail("user maydoni yo'q yoki noto'g'ri", out error);

        error = null;
        return true;
    }

    private byte[] ComputeHash(SortedDictionary<string, string> fields)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in fields)
        {
            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append(key).Append('=').Append(value);
        }

        var secret = HMACSHA256.HashData(WebAppDataKey, Encoding.UTF8.GetBytes(_options.BotToken));
        return HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(builder.ToString()));
    }

    /// <summary>Kalitlar ordinal tartibda — Telegram ham shunday tartiblaydi (ASCII).</summary>
    private static SortedDictionary<string, string> Parse(string initData)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, values) in QueryHelpers.ParseQuery(initData))
        {
            if (!string.IsNullOrEmpty(key))
                result[key] = values.LastOrDefault() ?? string.Empty;
        }

        return result;
    }

    private static bool TryParseUser(string json, DateTimeOffset authDate, out TelegramInitUser? user)
    {
        user = null;
        try
        {
            var payload = JsonSerializer.Deserialize<UserPayload>(json);
            if (payload is null || payload.Id <= 0)
                return false;

            user = new TelegramInitUser(payload.Id, payload.FirstName, payload.LastName, payload.Username, authDate);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte[]? TryFromHex(string hex)
    {
        if (hex.Length != 64)
            return null;

        try
        {
            return Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool Fail(string reason, out string? error)
    {
        error = reason;
        return false;
    }

    private sealed class UserPayload
    {
        [JsonPropertyName("id")] public long Id { get; init; }
        [JsonPropertyName("first_name")] public string? FirstName { get; init; }
        [JsonPropertyName("last_name")] public string? LastName { get; init; }
        [JsonPropertyName("username")] public string? Username { get; init; }
    }
}
