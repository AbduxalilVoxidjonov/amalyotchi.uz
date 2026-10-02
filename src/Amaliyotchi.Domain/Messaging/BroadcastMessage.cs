using Amaliyotchi.Domain.Common;
using Amaliyotchi.Domain.Exceptions;

namespace Amaliyotchi.Domain.Messaging;

/// <summary>Admin Telegram orqali yuborgan xabar ("Xabarlar" bo'limi). Qabul qiluvchilar yaratish paytida snapshot
/// qilinadi — har biri uchun <see cref="BroadcastDelivery"/>; keyin talaba ulansa/uzilsa ham ro'yxat o'zgarmaydi.
/// <see cref="AuditableEntity.CreatedBy"/> — yuborgan admin (interceptor to'ldiradi).
/// Audit interceptor'dan istisno: matn butun holda audit jurnaliga tushmasin — handler <c>IAuditWriter</c> bilan
/// qisqartirilgan yozuv qoldiradi.</summary>
public sealed class BroadcastMessage : AuditableEntity, IAuditExempt
{
    /// <summary>Telegram <c>sendMessage</c> chegarasi 4096 belgi; zaxira bilan 4000.</summary>
    public const int TextMaxLength = 4000;

    public const int AudienceLabelMaxLength = 300;

    /// <summary>Auditoriya parametrlari JSON'i (kind + filtr yoki tanlanganlar soni) — tarix uchun.</summary>
    public const int AudienceJsonMaxLength = 2000;

    private BroadcastMessage() { }

    public string Text { get; private set; } = string.Empty;
    public bool AttachAppButton { get; private set; }
    public BroadcastAudienceKind AudienceKind { get; private set; }

    /// <summary>Tarixda ko'rsatiladigan o'zbekcha tavsif ("Barcha ulanganlar", "Tanlangan: 3 ta talaba" ...).</summary>
    public string AudienceLabel { get; private set; } = string.Empty;

    public string AudienceJson { get; private set; } = "{}";

    public static BroadcastMessage Create(
        string text, bool attachAppButton, BroadcastAudienceKind audienceKind, string audienceLabel, string audienceJson)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException("Xabar matni bo'sh bo'lishi mumkin emas.");
        if (trimmed.Length > TextMaxLength)
            throw new DomainException($"Xabar matni {TextMaxLength} belgidan oshmasligi kerak.");
        if (string.IsNullOrWhiteSpace(audienceLabel))
            throw new DomainException("Auditoriya tavsifi bo'sh bo'lishi mumkin emas.");

        return new BroadcastMessage
        {
            Text = trimmed,
            AttachAppButton = attachAppButton,
            AudienceKind = audienceKind,
            AudienceLabel = audienceLabel.Length > AudienceLabelMaxLength ? audienceLabel[..AudienceLabelMaxLength] : audienceLabel,
            AudienceJson = string.IsNullOrWhiteSpace(audienceJson) ? "{}" : audienceJson
        };
    }
}
