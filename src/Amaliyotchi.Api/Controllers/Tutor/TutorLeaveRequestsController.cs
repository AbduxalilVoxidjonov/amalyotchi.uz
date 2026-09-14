using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.LeaveRequests;
using Amaliyotchi.Domain.Leave;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/leave-requests")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorLeaveRequestsController(ISender sender) : ControllerBase
{
    /// <summary><c>?status=pending|approved|rejected</c> (berilmasa — hammasi, kutilayotganlar birinchi).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TutorLeaveRequest>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorLeaveRequest>>> List(
        [FromQuery] LeaveRequestStatus? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorLeaveRequestsQuery(status), cancellationToken));

    /// <summary><c>approve</c> → oraliqdagi ish kunlari "sababli"; <c>reject</c>. Hal qilingan → 409.</summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType<TutorLeaveRequest>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorLeaveRequest>> Decide(
        Guid id, DecideLeaveRequestCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { LeaveRequestId = id }, cancellationToken));
}
