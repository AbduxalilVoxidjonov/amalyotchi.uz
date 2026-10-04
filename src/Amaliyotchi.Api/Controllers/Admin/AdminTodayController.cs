using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Supervision.Today;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/today")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminTodayController(ISender sender) : ControllerBase
{
    /// <summary>Bugungi davomat: statistika, ogohlantirishlar va sahifalangan qatorlar.
    /// <c>?status=present|late|absent|excused|pending|suspicious&amp;q&amp;page&amp;pageSize</c>.</summary>
    [HttpGet]
    [ProducesResponseType<TodayResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TodayResponse>> Get([FromQuery] GetTodayAttendanceQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
