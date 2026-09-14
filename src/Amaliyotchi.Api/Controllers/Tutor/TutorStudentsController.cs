using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/students")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorStudentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TutorStudent>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorStudent>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentsQuery(), cancellationToken));
}
