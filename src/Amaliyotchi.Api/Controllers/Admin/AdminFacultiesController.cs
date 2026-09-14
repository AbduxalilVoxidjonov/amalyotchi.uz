using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Faculties;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/faculties")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminFacultiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<FacultyRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<FacultyRow>>> List([FromQuery] GetFacultiesQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
