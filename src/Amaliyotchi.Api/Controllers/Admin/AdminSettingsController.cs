using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Settings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminSettingsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminSettingsDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetSettingsQuery(), cancellationToken));

    /// <summary><c>{ values: { key: "qiymat" } }</c> — faqat o'zgargan kalitlar. Noto'g'ri qiymat → 400 <c>errors.&lt;key&gt;</c>.</summary>
    [HttpPut]
    [ProducesResponseType<AdminSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminSettingsDto>> Update(UpdateSettingsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
}
