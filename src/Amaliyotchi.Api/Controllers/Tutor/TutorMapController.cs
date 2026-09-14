using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Map;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/map")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorMapController(ISender sender) : ControllerBase
{
    /// <summary><c>?date=YYYY-MM-DD</c> (berilmasa — bugun): har talabaning oxirgi check-in urinishi nuqtasi.</summary>
    [HttpGet]
    [ProducesResponseType<MapResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MapResponse>> Get([FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorMapQuery(date), cancellationToken));
}
