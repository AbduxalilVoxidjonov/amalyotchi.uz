using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;

namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>Ko'lam roldan aniqlanadi: admin — cheklovsiz, talaba — faqat o'zi, boshqa/anonim — bo'sh.</summary>
internal sealed class ScopeResolver(ICurrentUser currentUser) : IScopeResolver
{
    public Task<DataScope> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId)
            return Task.FromResult(DataScope.Empty);

        return Task.FromResult(currentUser.Role switch
        {
            UserRole.Admin => DataScope.Unrestricted(userId),
            UserRole.Student => DataScope.ForStudent(userId),
            _ => DataScope.Empty
        });
    }
}
