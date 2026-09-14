using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Amaliyotchi.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public const string FacultyClaim = "faculty_id";

    /// <summary>JsonWebTokenHandler outbound mapping qilmaydi — ClaimTypes.Role ishlatilsa JWT ichiga
    /// uzun URI tushadi. Qisqa "role" ni JwtBearer (MapInboundClaims=true) ClaimTypes.Role ga o'zi xaritalaydi.
    /// "name" esa inbound map'da YO'Q — u AuthorizationSetup'da <c>NameClaimType</c> orqali sozlangan.</summary>
    public const string RoleClaim = "role";

    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(User user)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(RoleClaim, user.Role.ToString())
        };

        if (user.FacultyId is { } facultyId)
            claims.Add(new Claim(FacultyClaim, facultyId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }

    public string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
