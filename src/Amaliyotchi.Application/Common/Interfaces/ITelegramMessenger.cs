namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Bitta Telegram yuborish natijasi turi — dispetcher shunga qarab yetkazish holatini o'zgartiradi.</summary>
public enum TelegramSendOutcome
{
    /// <summary>Telegram qabul qildi.</summary>
    Sent,

    /// <summary>Bot bu chatga yoza olmaydi: 403 (botni bloklagan, hisob o'chirilgan, botni ishga tushirmagan) yoki
    /// 400 chat not found. Qayta urinilmaydi.</summary>
    Blocked,

    /// <summary>429 Too Many Requests — <see cref="TelegramSendResult.RetryAfter"/> dan keyin; urinish hisoblanmaydi.</summary>
    RateLimited,

    /// <summary>Tarmoq, timeout, 5xx — eksponensial backoff bilan qayta urinish.</summary>
    TransientError,

    /// <summary>Qayta urinib foyda yo'q (masalan boshqa 400, 401 token yaroqsiz).</summary>
    PermanentError,

    /// <summary><c>Telegram:BotToken</c> sozlanmagan yoki formati noto'g'ri.</summary>
    NotConfigured
}

/// <param name="Error">O'zbekcha qisqa sabab (yetkazish qatoriga yoziladi) — token va xabar matnisiz.</param>
public sealed record TelegramSendResult(
    TelegramSendOutcome Outcome,
    long? MessageId = null,
    TimeSpan? RetryAfter = null,
    string? Error = null)
{
    public static TelegramSendResult Sent(long messageId) => new(TelegramSendOutcome.Sent, MessageId: messageId);
}

/// <summary>Bot nomidan talabaga shaxsiy xabar yuborish ("Xabarlar"). Faqat <c>Telegram:BotToken</c> ga bog'liq —
/// long polling (<c>Telegram:BotEnabled</c>) o'chiq bo'lsa ham ishlaydi. Matn oddiy (parse_mode yo'q), havola
/// preview'i o'chiq. Istisno tashlamaydi (bekor qilishdan tashqari) — xato <see cref="TelegramSendResult"/> da.</summary>
public interface ITelegramMessenger
{
    /// <param name="withAppButton">true → xabar ostida "Ilovani ochish" (web_app) tugmasi — <c>Telegram:WebAppUrl</c>
    /// HTTPS bo'lsa; bo'lmasa tugmasiz yuboriladi.</param>
    Task<TelegramSendResult> SendTextAsync(long chatId, string text, bool withAppButton, CancellationToken cancellationToken);
}
