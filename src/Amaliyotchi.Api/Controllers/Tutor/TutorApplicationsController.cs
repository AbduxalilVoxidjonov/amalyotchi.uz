using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Applications;
using Amaliyotchi.Domain.Practice;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/applications")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorApplicationsController(ISender sender) : ControllerBase
{
    /// <summary><c>?status=submitted|revisionNeeded|approved|rejected</c> (berilmasa — hammasi) + tab hisoblagichlari.</summary>
    [HttpGet]
    [ProducesResponseType<ApplicationListResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationListResponse>> List(
        [FromQuery] ApplicationStatus? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetApplicationsQuery(status), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDetail>> Detail(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetApplicationDetailQuery(id), cancellationToken));

    /// <summary>Qaror: <c>approve</c> (radiusM majburiy), <c>return</c> / <c>reject</c> (izoh majburiy). Hal qilingan → 409.</summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<ApplicationDecisionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDecisionResponse>> Decide(
        Guid id, DecideApplicationCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { ApplicationId = id }, cancellationToken));
}
