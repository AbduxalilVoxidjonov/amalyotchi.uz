namespace Amaliyotchi.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "amaliyotchi";
    public string Audience { get; init; } = "amaliyotchi.clients";

    /// <summary>Kamida 32 ta belgi. appsettings'da emas, muhit o'zgaruvchisi yoki
    /// user-secrets orqali beriladi.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}
