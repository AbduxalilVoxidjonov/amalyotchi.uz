using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Messaging;

/// <summary>Xabarning bitta talabaga yetkazilishi. Ikki vazifasi bor:
/// <list type="bullet">
/// <item><b>Ilova qutisi (inbox)</b> — har qabul qiluvchi uchun bitta qator; talaba qutisi shu qatorlardan o'qiladi,
/// o'qilganlik — <see cref="ReadAt"/>. Qator xabar bilan bir tranzaksiyada yaratiladi — Telegram yetkazilishini kutmaydi.</item>
/// <item><b>Telegram DB-navbati</b> — fon dispetcheri <see cref="BroadcastDeliveryStatus.Pending"/> va <see cref="NextAttemptAt"/>
/// o'tgan qatorlarni oladi. <see cref="ChatId"/> — yaratish paytidagi Telegram id snapshot'i; talaba Telegram bog'lamagan
/// bo'lsa null va holat darhol <see cref="BroadcastDeliveryStatus.AppOnly"/> (yakuniy) — navbatga hech qachon tushmaydi.</item>
/// </list>
/// Yuqori hajmli texnik yozuv — audit jurnaliga tushmaydi (<see cref="IAuditExempt"/>).</summary>
public sealed class BroadcastDelivery : BaseEntity, IAuditExempt
{
    public const int ErrorMaxLength = 500;

    /// <summary>Tarmoq/5xx xatolarida shuncha urinishdan keyin <see cref="BroadcastDeliveryStatus.Failed"/>.</summary>
    public const int MaxAttempts = 5;

    public static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private BroadcastDelivery() { }

    public Guid MessageId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    /// <summary>Telegram chat id (yaratish paytidagi snapshot). null — talaba Telegram bog'lamagan
    /// (<see cref="BroadcastDeliveryStatus.AppOnly"/>).</summary>
    public long? ChatId { get; private set; }
    public BroadcastDeliveryStatus Status { get; private set; }

    /// <summary>Muvaffaqiyatsiz (vaqtinchalik xato) urinishlar soni. 429 (rate limit) hisoblanmaydi.</summary>
    public int Attempts { get; private set; }

    /// <summary>Oxirgi xato — qisqa, o'zbekcha (≤ <see cref="ErrorMaxLength"/>). Token/xabar matni yozilmaydi.</summary>
    public string? Error { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }
    public long? TelegramMessageId { get; private set; }

    /// <summary>Dispetcher shu vaqtdan keyin oladi (backoff, 429 <c>retry_after</c>, ijara).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Talaba xabarni ilova qutisida o'qigan vaqt (null — o'qilmagan). Telegram holatidan mustaqil.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary><paramref name="chatId"/> berilsa — Telegram navbatiga (<see cref="BroadcastDeliveryStatus.Pending"/>);
    /// null bo'lsa — faqat ilova qutisi (<see cref="BroadcastDeliveryStatus.AppOnly"/>, yakuniy holat).</summary>
    public static BroadcastDelivery Create(Guid messageId, Guid recipientUserId, long? chatId, DateTimeOffset now)
    {
        if (messageId == Guid.Empty || recipientUserId == Guid.Empty)
            throw new DomainException("Xabar yoki qabul qiluvchi ko'rsatilmagan.");

        return new BroadcastDelivery
        {
            MessageId = messageId,
            RecipientUserId = recipientUserId,
            ChatId = chatId,
            Status = chatId is null ? BroadcastDeliveryStatus.AppOnly : BroadcastDeliveryStatus.Pending,
            NextAttemptAt = now
        };
    }

    /// <summary>Ilova qutisida o'qildi. Idempotent: birinchi o'qilgan vaqt saqlanadi.</summary>
    public void MarkRead(DateTimeOffset at)
    {
        ReadAt ??= at;
    }

    public void MarkSent(DateTimeOffset at, long? telegramMessageId)
    {
        Status = BroadcastDeliveryStatus.Sent;
        SentAt = at;
        TelegramMessageId = telegramMessageId;
        Error = null;
    }

    /// <summary>Talaba botni bloklagan / botni ishga tushirmagan — qayta urinilmaydi.</summary>
    public void MarkBlocked(string error)
    {
        Status = BroadcastDeliveryStatus.Blocked;
        Error = Clip(error);
    }

    /// <summary>Qayta urinib bo'lmaydigan xato (yoki bot tokeni sozlanmagan).</summary>
    public void MarkFailed(string error)
    {
        Status = BroadcastDeliveryStatus.Failed;
        Error = Clip(error);
    }

    /// <summary>Vaqtinchalik xato (tarmoq, 5xx): urinish hisoblanadi, eksponensial backoff; <see cref="MaxAttempts"/> dan
    /// keyin <see cref="BroadcastDeliveryStatus.Failed"/>.</summary>
    public void RegisterTransientFailure(string error, DateTimeOffset now)
    {
        Attempts++;
        Error = Clip(error);
        if (Attempts >= MaxAttempts)
        {
            Status = BroadcastDeliveryStatus.Failed;
            return;
        }

        NextAttemptAt = now + BackoffFor(Attempts);
    }

    /// <summary>Urinish hisoblanmaydigan kechiktirish: 429 <c>retry_after</c>, to'xtatish paytida ijarani bo'shatish.</summary>
    public void Defer(DateTimeOffset until)
    {
        if (Status == BroadcastDeliveryStatus.Pending)
            NextAttemptAt = until;
    }

    /// <summary>Admin "qayta yuborish": faqat <see cref="BroadcastDeliveryStatus.Failed"/> → navbatga, urinishlar nollanadi.</summary>
    public bool Requeue(DateTimeOffset now)
    {
        if (Status != BroadcastDeliveryStatus.Failed)
            return false;

        Status = BroadcastDeliveryStatus.Pending;
        Attempts = 0;
        Error = null;
        NextAttemptAt = now;
        return true;
    }

    /// <summary><paramref name="attempts"/>-urinishdan keyingi kutish: 5 s · 2^(n−1), <see cref="MaxBackoff"/> bilan cheklangan.</summary>
    public static TimeSpan BackoffFor(int attempts)
    {
        if (attempts < 1)
            return BaseBackoff;

        var factor = Math.Pow(2, Math.Min(attempts - 1, 20));
        var delay = TimeSpan.FromTicks((long)Math.Min(BaseBackoff.Ticks * factor, MaxBackoff.Ticks));
        return delay;
    }

    private static string Clip(string error)
    {
        var value = string.IsNullOrWhiteSpace(error) ? "Noma'lum xato" : error.Trim();
        return value.Length > ErrorMaxLength ? value[..ErrorMaxLength] : value;
    }
}
