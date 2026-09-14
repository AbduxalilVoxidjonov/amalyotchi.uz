using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/students")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminStudentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<StudentRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<StudentRow>>> List([FromQuery] GetAdminStudentsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
