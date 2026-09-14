using Amaliyotchi.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Amaliyotchi.Application.Features.Student.Common;

/// <summary>Check-in/check-out "o'qi → qaror → yoz" ni bitta tranzaksiyada bajaradi. Npgsql retry
/// (<c>EnableRetryOnFailure</c>) yoqilgani uchun tranzaksiya execution strategy ichida ochiladi —
/// aks holda EF "user-initiated transactions" xatosini tashlaydi. Ulanish uzilsa butun blok qayta bajariladi.</summary>
internal static class DbTransactions
{
    public static Task<T> InTransactionAsync<T>(
        this IApplicationDbContext db, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}
