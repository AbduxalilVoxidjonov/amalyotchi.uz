using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Supervision.Calendar;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/calendar")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminCalendarController(ISender sender) : ControllerBase
{
    /// <summary><c>?month=YYYY-MM</c> (berilmasa — joriy oy).</summary>
    [HttpGet]
    [ProducesResponseType<CalendarResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarResponse>> Get([FromQuery] string? month, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAttendanceCalendarQuery(month), cancellationToken));
}
