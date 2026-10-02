using Amaliyotchi.Domain.Identity;
using Amaliyotchi.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace Amaliyotchi.Infrastructure.Persistence.Interceptors;

/// <summary><see cref="User"/> yozuvi o'zgarganda (parol, login, faollik, o'chirish ...) uning access token stamp keshini
/// tozalaydi — <see cref="UserSessionValidator"/> keyingi so'rovda bazadan yangi holatni oladi.
/// Kesh saqlashdan oldin ham, keyin ham tozalanadi: oraliqda eski qiymat qayta keshlanib qolmasin.</summary>
public sealed class UserSessionCacheInterceptor(IMemoryCache cache) : SaveChangesInterceptor
{
    private List<Guid> _pending = [];

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
        Flush();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Flush();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Flush();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<User>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                _pending.Add(entry.Entity.Id);
                cache.Remove(UserSessionValidator.CacheKey(entry.Entity.Id));
            }
        }
    }

    private void Flush()
    {
        if (_pending.Count == 0)
            return;

        foreach (var id in _pending)
            cache.Remove(UserSessionValidator.CacheKey(id));
        _pending = [];
    }
}
