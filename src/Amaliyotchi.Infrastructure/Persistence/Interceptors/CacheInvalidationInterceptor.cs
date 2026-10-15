using System.Data.Common;
using Amaliyotchi.Application.Common.Caching;
using Amaliyotchi.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Amaliyotchi.Infrastructure.Persistence.Interceptors;

/// <summary>Yozuvdan keyin kesh teglarini bekor qiladi — qaysi teglar, o'zgargan entity turlari bo'yicha
/// (<see cref="CacheDependencies"/>). Handler'lar keshni o'zi tozalamaydi: admin yoki talaba amali bo'lsin
/// (ariza qarori, kundalik yuborish, check-in ...), har qanday <c>SaveChanges</c> tegishli sidebar badge'lari va
/// dashboard keshini darhol eskirtiradi.
/// <para>Muvaffaqiyatli <c>SaveChanges</c> dan keyin darhol tozalanadi; ochiq tranzaksiya ichida bo'lsa — commit'dan keyin
/// yana bir marta (commit'gacha boshqa so'rov eski holatni qayta keshlab qo'ygan bo'lsa ham u tozalanadi).
/// <c>ExecuteUpdate/ExecuteDelete</c> interceptor'dan o'tmaydi — ular keshlangan jadvallarga tegsa, chaqiruvchi
/// <see cref="IAppCache.InvalidateAsync"/> ni o'zi chaqiradi (masalan demo tozalash).</para></summary>
public sealed class CacheInvalidationInterceptor(IAppCache cache) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly HashSet<string> _collected = new(StringComparer.Ordinal);
    private readonly HashSet<string> _awaitingCommit = new(StringComparer.Ordinal);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakeCollected(eventData.Context) is { } tags)
            cache.InvalidateAsync(tags).AsTask().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        // CancellationToken.None: ma'lumot allaqachon yozilgan — so'rov bekor qilinsa ham kesh tozalanishi shart.
        if (TakeCollected(eventData.Context) is { } tags)
            await cache.InvalidateAsync(tags, CancellationToken.None);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _collected.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _collected.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    void IDbTransactionInterceptor.TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (TakeAwaitingCommit() is { } tags)
            cache.InvalidateAsync(tags).AsTask().GetAwaiter().GetResult();
    }

    async Task IDbTransactionInterceptor.TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
    {
        if (TakeAwaitingCommit() is { } tags)
            await cache.InvalidateAsync(tags, CancellationToken.None);
    }

    void IDbTransactionInterceptor.TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => _awaitingCommit.Clear();

    Task IDbTransactionInterceptor.TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
    {
        _awaitingCommit.Clear();
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // Owned tur (masalan davr qoidalari) — egasining turi bo'yicha.
            var entityType = entry.Metadata;
            while (entityType.IsOwned() && entityType.FindOwnership() is { } ownership)
                entityType = ownership.PrincipalEntityType;

            foreach (var tag in CacheDependencies.For(entityType.ClrType))
                _collected.Add(tag);
        }
    }

    private string[]? TakeCollected(DbContext? context)
    {
        if (_collected.Count == 0)
            return null;

        var tags = _collected.ToArray();
        _collected.Clear();

        if (context?.Database.CurrentTransaction is not null)
            _awaitingCommit.UnionWith(tags);

        return tags;
    }

    private string[]? TakeAwaitingCommit()
    {
        if (_awaitingCommit.Count == 0)
            return null;

        var tags = _awaitingCommit.ToArray();
        _awaitingCommit.Clear();
        return tags;
    }
}
