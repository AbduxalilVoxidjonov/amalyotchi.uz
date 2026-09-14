namespace Amaliyotchi.Application.Common.Scoping;

/// <summary>Joriy so'rov uchun ma'lumot ko'lamini aniqlaydi (so'rov ichida keshlanadi).
/// Har tyutor/talaba handler'i shu bilan boshlanadi: <c>var scope = await scopeResolver.ResolveAsync(ct);</c>.</summary>
public interface IScopeResolver
{
    Task<DataScope> ResolveAsync(CancellationToken cancellationToken = default);
}
