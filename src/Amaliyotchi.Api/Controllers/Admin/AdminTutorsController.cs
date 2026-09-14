using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Tutors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/tutors")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminTutorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<TutorRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<TutorRow>>> List([FromQuery] GetTutorsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
