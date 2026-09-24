using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Amaliyotchi.Infrastructure.Bot;

/// <summary>Bot yuboradigan javob: matn va (bo'lsa) Mini App'ni ochuvchi inline tugma.</summary>
public sealed record BotReply(long ChatId, string Text, BotWebAppButton? Button);

/// <summary>Inline <c>web_app</c> tugmasi — <see cref="Url"/> Mini App manzili.</summary>
public sealed record BotWebAppButton(string Text, string Url);

/// <summary>Update → javob tanlash mantig'i (sof funksiya, tarmoqsiz — unit testlanadi).
/// Faqat private chat'dagi matnli xabarlarga javob beriladi; guruh/kanal va boshqa update turlari —
/// <c>null</c> (e'tiborsiz).</summary>
public static class BotReplyPlanner
{
    public const string OpenAppButtonText = "Ilovani ochish";

    public static BotReply? Plan(Update update, string webAppUrl)
    {
        var message = update.Message;
        if (message is null || message.Chat.Type != ChatType.Private || message.Text is null)
            return null;

        var button = new BotWebAppButton(OpenAppButtonText, webAppUrl);
        var chatId = message.Chat.Id;

        return ParseCommand(message.Text) switch
        {
            "start" => new BotReply(chatId, StartText(message.From?.FirstName ?? message.Chat.FirstName), button),
            "help" => new BotReply(chatId, HelpText, button),
            _ => new BotReply(chatId, FallbackText, button),
        };
    }

    /// <summary>"/start", "/start xyz", "/START@bot_nomi" → "start"; buyruq bo'lmasa <c>null</c>.</summary>
    public static string? ParseCommand(string text)
    {
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith('/'))
            return null;

        var token = trimmed[1..].Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(token))
            return null;

        var at = token.IndexOf('@');
        if (at >= 0)
            token = token[..at];

        return token.ToLowerInvariant();
    }

    public static string StartText(string? firstName)
    {
        var greeting = string.IsNullOrWhiteSpace(firstName)
            ? "Assalomu alaykum!"
            : $"Assalomu alaykum, {firstName.Trim()}!";

        return greeting + " Amaliyotchi — amaliyot davomati va kundaligi uchun ilova. " +
               "Pastdagi tugma orqali ilovani oching. Birinchi marta kirsangiz, " +
               "tyutoringiz bergan HEMIS ID va parolni kiriting.";
    }

    public const string HelpText =
        "Amaliyotchi ilovasida:\n" +
        "• Kelish va ketish — korxonadagi QR kodni skanerlab, selfi bilan belgilanadi.\n" +
        "• Kundalik — har kuni bajarilgan ishlarni yozib borasiz.\n" +
        "• Ruxsat so'rovi — kela olmasangiz, sababini ko'rsatib so'rov yuborasiz.\n\n" +
        "Muammo bo'lsa (kira olmasangiz, parolni unutsangiz), tyutoringizga murojaat qiling.";

    public const string FallbackText =
        "Men faqat ilovani ochishga yordam beraman. Pastdagi tugmani bosing yoki /help yozing.";
}
