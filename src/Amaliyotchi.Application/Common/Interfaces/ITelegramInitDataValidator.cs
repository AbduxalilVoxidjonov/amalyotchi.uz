namespace Amaliyotchi.Application.Common.Interfaces;

/// <summary>Telegram Mini App <c>initData</c> ichidagi <c>user</c> obyektidan kerakli qism.</summary>
public sealed record TelegramInitUser(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username,
    DateTimeOffset AuthDate);

/// <summary>Telegram Mini App <c>initData</c> satrining HMAC imzosini va yoshini tekshiradi.</summary>
public interface ITelegramInitDataValidator
{
    /// <summary>Imzo to'g'ri va <c>auth_date</c> eskirmagan bo'lsa <c>true</c>; aks holda
    /// <paramref name="error"/> da sabab (log uchun — mijozga oshkor qilinmaydi).</summary>
    bool TryValidate(
        string initData,
        DateTimeOffset now,
        out TelegramInitUser? user,
        out string? error);
}
