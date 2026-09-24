namespace Amaliyotchi.Infrastructure.Identity;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>BotFather bergan token. appsettings'da emas — muhit o'zgaruvchisi (<c>Telegram__BotToken</c>)
    /// yoki user-secrets orqali beriladi.</summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary><c>auth_date</c> shundan eski bo'lsa initData rad etiladi. Standart — 24 soat.</summary>
    public int MaxAgeSeconds { get; init; } = 24 * 60 * 60;

    /// <summary>Bot xizmati (long polling: /start, /help, Menu Button). Sukut — o'chiq: bir vaqtda faqat
    /// bitta instansiya polling qilishi mumkin (aks holda Telegram 409 Conflict qaytaradi), shuning uchun
    /// faqat deploy muhitida yoqiladi (<c>Telegram__BotEnabled=true</c>).</summary>
    public bool BotEnabled { get; init; }

    /// <summary>Mini App (TWA) ommaviy HTTPS manzili — bot tugmasi va Menu Button shu yerga ochadi
    /// (masalan <c>https://app.amalyotchi.uz</c>).</summary>
    public string WebAppUrl { get; init; } = string.Empty;
}
