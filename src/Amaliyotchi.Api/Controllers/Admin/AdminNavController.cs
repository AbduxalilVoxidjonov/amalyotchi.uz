using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Nav;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/nav")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminNavController(ISender sender) : ControllerBase
{
    /// <summary>Sidebar badge'lari (ro'yxatlarning sukut <c>total</c>lari) va header konteksti (joriy o'quv yili).</summary>
    [HttpGet]
    [ProducesResponseType<AdminNavDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminNavDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAdminNavQuery(), cancellationToken));
}
