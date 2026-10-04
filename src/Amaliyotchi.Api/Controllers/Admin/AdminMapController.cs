using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Supervision.Map;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/map")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminMapController(ISender sender) : ControllerBase
{
    /// <summary><c>?date=YYYY-MM-DD</c> (berilmasa — bugun): har talabaning oxirgi check-in urinishi nuqtasi.</summary>
    [HttpGet]
    [ProducesResponseType<MapResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MapResponse>> Get([FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAttendanceMapQuery(date), cancellationToken));
}
