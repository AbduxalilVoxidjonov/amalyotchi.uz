using Amaliyotchi.Application.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Amaliyotchi.Infrastructure.Caching;

/// <summary><see cref="IAppCache"/> — <see cref="HybridCache"/> ustida: L1 (jarayon xotirasi) + L2 (Redis, agar
/// <c>ConnectionStrings:Redis</c> berilgan bo'lsa). Bir kalitga bir vaqtda kelgan so'rovlar bitta factory'ni kutadi
/// (stampede himoyasi). Kesh qatlamining o'z xatosi (Redis uzilgan va h.k.) so'rovni yiqitmaydi — qiymat bazadan olinadi.</summary>
internal sealed class HybridAppCache(HybridCache cache, ILogger<HybridAppCache> logger) : IAppCache
{
    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan expiration,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        var factoryFailed = false;
        var options = new HybridCacheEntryOptions { Expiration = expiration, LocalCacheExpiration = expiration };

        try
        {
            return await cache.GetOrCreateAsync(
                key,
                async ct =>
                {
                    try
                    {
                        return await factory(ct);
                    }
                    catch
                    {
                        factoryFailed = true;
                        throw;
                    }
                },
                options, tags, cancellationToken);
        }
        catch (Exception ex) when (!factoryFailed && ex is not OperationCanceledException)
        {
            // Faqat kesh infratuzilmasi xatosi — factory'ning o'z xatosi (validatsiya, 404 ...) o'zgarishsiz ko'tariladi.
            logger.LogWarning("Kesh o'qish xatosi ({Key}: {Type}: {Message}) — qiymat bazadan olinadi.",
                key, ex.GetType().Name, ex.Message);
            return await factory(cancellationToken);
        }
    }

    public async ValueTask InvalidateAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveByTagAsync(tags, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Kesh tozalash xatosi ({Type}: {Message}) — yozuvlar TTL tugaguncha eskirishi mumkin.",
                ex.GetType().Name, ex.Message);
        }
    }
}
