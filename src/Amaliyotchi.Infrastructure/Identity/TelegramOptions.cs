namespace Amaliyotchi.Infrastructure.Identity;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>BotFather bergan token. appsettings'da emas — muhit o'zgaruvchisi (<c>Telegram__BotToken</c>)
    /// yoki user-secrets orqali beriladi.</summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary><c>auth_date</c> shundan eski bo'lsa initData rad etiladi. Standart — 24 soat.</summary>
    public int MaxAgeSeconds { get; init; } = 24 * 60 * 60;
}
