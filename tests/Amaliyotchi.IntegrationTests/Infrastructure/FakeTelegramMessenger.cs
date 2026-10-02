using System.Collections.Concurrent;
using Amaliyotchi.Application.Common.Interfaces;

namespace Amaliyotchi.IntegrationTests.Infrastructure;

/// <summary>Testlarda Telegram o'rniga: tarmoqqa chiqmaydi. Sukut bo'yicha har yuborish muvaffaqiyatli (yangi message_id);
/// <see cref="Script"/> bilan chat uchun ketma-ket natijalar (403, 429, tarmoq xatosi ...) beriladi.</summary>
public sealed class FakeTelegramMessenger : ITelegramMessenger
{
    public sealed record Call(long ChatId, string Text, bool WithAppButton);

    private readonly ConcurrentDictionary<long, ConcurrentQueue<TelegramSendResult>> _scripts = new();
    private readonly ConcurrentQueue<Call> _calls = new();
    private long _nextMessageId = 1000;

    /// <summary>Chat uchun keyingi natijalar (navbat bilan). Navbat tugagach — muvaffaqiyat.</summary>
    public void Script(long chatId, params TelegramSendResult[] results)
    {
        var queue = _scripts.GetOrAdd(chatId, _ => new ConcurrentQueue<TelegramSendResult>());
        foreach (var result in results)
            queue.Enqueue(result);
    }

    public IReadOnlyList<Call> CallsTo(long chatId) => _calls.Where(c => c.ChatId == chatId).ToList();

    public Task<TelegramSendResult> SendTextAsync(long chatId, string text, bool withAppButton, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Enqueue(new Call(chatId, text, withAppButton));

        if (_scripts.TryGetValue(chatId, out var queue) && queue.TryDequeue(out var scripted))
            return Task.FromResult(scripted);

        return Task.FromResult(TelegramSendResult.Sent(Interlocked.Increment(ref _nextMessageId)));
    }
}
