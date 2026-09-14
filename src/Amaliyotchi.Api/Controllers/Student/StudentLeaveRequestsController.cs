using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.LeaveRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/leave-requests")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentLeaveRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LeaveRequestDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentLeaveRequestsQuery(), cancellationToken));

    /// <summary>Ruxsat so'rovi. 201; sanalar davr tashqarisida → 400; kesishuvchi so'rov bor → 409.</summary>
    [HttpPost]
    [ProducesResponseType<LeaveRequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LeaveRequestDto>> Create(CreateLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
