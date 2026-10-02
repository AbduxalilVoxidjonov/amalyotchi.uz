namespace Amaliyotchi.Infrastructure.Messaging;

/// <summary>"Xabarlar" fon dispetcheri sozlamalari (<c>Messaging:*</c>). Sukut qiymatlari production uchun.</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>Fon tsikli (<see cref="BroadcastDispatcherService"/>). Testlar o'chiradi va <see cref="BroadcastDispatcher.RunOnceAsync"/>
    /// ni o'zi chaqiradi. O'chiq bo'lsa xabarlar navbatda qoladi (yo'qolmaydi).</summary>
    public bool DispatcherEnabled { get; init; } = true;

    /// <summary>Global tezlik chegarasi. Telegram: botdan ~30 xabar/s — zaxira bilan 25.</summary>
    public int MessagesPerSecond { get; init; } = 25;

    /// <summary>Bitta "tick"da olinadigan yetkazishlar.</summary>
    public int BatchSize { get; init; } = 25;

    /// <summary>Navbat bo'sh bo'lganda tekshirish oralig'i.</summary>
    public int PollIntervalMs { get; init; } = 1000;
}
