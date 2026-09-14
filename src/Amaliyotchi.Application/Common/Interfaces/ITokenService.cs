using Amaliyotchi.Domain.Identity;

namespace Amaliyotchi.Application.Common.Interfaces;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    string CreateRefreshToken();
    TimeSpan RefreshTokenLifetime { get; }
}
