using Amaliyotchi.Domain.Common;

namespace Amaliyotchi.Domain.Identity;

/// <summary>Refresh token — texnik yozuv, audit jurnaliga tushmaydi.</summary>
public sealed class RefreshToken : BaseEntity, IAuditExempt
{
    private RefreshToken() { }

    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    internal static RefreshToken Issue(Guid userId, string token, DateTimeOffset expiresAt, string? createdByIp)
        => new()
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByIp = createdByIp
        };

    public void Revoke(DateTimeOffset at, string reason, string? replacedByToken = null)
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = at;
        RevokedReason = reason;
        ReplacedByToken = replacedByToken;
    }
}
