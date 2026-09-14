using System.Diagnostics;
using System.Security.Claims;
using Amaliyotchi.Application.Common.Interfaces;
using Amaliyotchi.Domain.Enums;
using Amaliyotchi.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace Amaliyotchi.Infrastructure.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub"), out var id)
            ? id
            : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role)
            ? role
            : null;

    public Guid? FacultyId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtTokenService.FacultyClaim), out var id)
            ? id
            : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? TraceId => Activity.Current?.Id ?? accessor.HttpContext?.TraceIdentifier;
}
