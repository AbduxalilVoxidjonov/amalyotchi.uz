namespace Amaliyotchi.Domain.Messaging;

/// <summary>Xabar auditoriyasi turi (yuborish paytida server tomonda resolve qilinadi). Raqamlar bazada saqlanadi —
/// o'zgartirilmaydi, yangilari faqat oxiriga qo'shiladi.</summary>
public enum BroadcastAudienceKind
{
    /// <summary>Admin ro'yxatdan belgilagan talabalar (<c>userIds</c>).</summary>
    Selected = 0,

    /// <summary>Filtr bo'yicha: <c>q</c>, fakultet, yo'nalish, guruh, kurs.</summary>
    Filter = 1,

    /// <summary>Telegram ulangan barcha faol talabalar.</summary>
    All = 2
}

/// <summary>Bitta talabaga yetkazish holati. Raqamlar bazada saqlanadi — o'zgartirilmaydi.</summary>
public enum BroadcastDeliveryStatus
{
    /// <summary>Navbatda (yoki qayta urinishni kutmoqda — <see cref="BroadcastDelivery.NextAttemptAt"/>).</summary>
    Pending = 0,

    /// <summary>Telegram qabul qildi.</summary>
    Sent = 1,

    /// <summary>Urinishlar tugadi yoki qayta urinib bo'lmaydigan xato — admin qayta yuborishi mumkin (<c>retry</c>).</summary>
    Failed = 2,

    /// <summary>Talaba botni bloklagan / botni ishga tushirmagan / hisobi o'chirilgan (403, 400 chat not found).
    /// Qayta yuborish (<c>retry</c>) bularga tegmaydi.</summary>
    Blocked = 3
}

/// <summary>Xabarning umumiy holati — yetkazishlar sonidan hisoblanadi (<see cref="BroadcastStatusRule"/>), saqlanmaydi.</summary>
public enum BroadcastMessageStatus
{
    /// <summary>Hali hech bir yetkazish yakuniy holatga yetmagan.</summary>
    Queued = 0,

    /// <summary>Bir qismi yakunlangan, navbatda hali bor.</summary>
    Sending = 1,

    /// <summary>Navbatda hech narsa qolmadi.</summary>
    Completed = 2
}

/// <summary>Xabar holati qoidasi: navbatdagi (<c>pending</c>) yetkazishlar jami bilan teng (hech biri yuborilmagan) →
/// <see cref="BroadcastMessageStatus.Queued"/>; <c>pending &gt; 0</c> → <see cref="BroadcastMessageStatus.Sending"/>;
/// aks holda <see cref="BroadcastMessageStatus.Completed"/>.</summary>
public static class BroadcastStatusRule
{
    public static BroadcastMessageStatus For(int total, int pending)
    {
        if (pending > 0 && pending >= total)
            return BroadcastMessageStatus.Queued;
        return pending > 0 ? BroadcastMessageStatus.Sending : BroadcastMessageStatus.Completed;
    }
}
