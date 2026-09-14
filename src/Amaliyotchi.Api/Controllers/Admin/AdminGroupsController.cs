using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Groups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/groups")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminGroupsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<GroupRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<GroupRow>>> List([FromQuery] GetGroupsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
