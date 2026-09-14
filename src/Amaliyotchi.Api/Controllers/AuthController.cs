using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Auth;
using Amaliyotchi.Application.Features.Auth.Login;
using Amaliyotchi.Application.Features.Auth.Logout;
using Amaliyotchi.Application.Features.Auth.Me;
using Amaliyotchi.Application.Features.Auth.Refresh;
using Amaliyotchi.Application.Features.Auth.Telegram;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Amaliyotchi.Api.Infrastructure;

namespace Amaliyotchi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Admin va tyutor uchun parol bilan kirish.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResultDto>> Login(
        LoginCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    /// <summary>Talaba uchun Telegram Mini App orqali kirish: <c>initData</c> imzosi tekshiriladi,
    /// hisob <c>user.id</c> bo'yicha topiladi. Imzo yoki hisob mos kelmasa — 403.</summary>
    [HttpPost("telegram")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResultDto>> Telegram(
        TelegramLoginCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    /// <summary>Access token muddati tugaganda yangilash. Refresh token rotatsiya qilinadi.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    [ProducesResponseType<AuthResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResultDto>> Refresh(
        RefreshTokenCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    [HttpPost("logout")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Joriy foydalanuvchi haqida qisqacha ma'lumot.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType<UserSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSummaryDto>> Me(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetMeQuery(), cancellationToken));
}
