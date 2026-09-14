using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDashboardController(ISender sender) : ControllerBase
{
    /// <summary>Umumiy ko'rsatkichlar: bugungi davomat, arizalar, fakultetlar kesimi, tyutorlar faolligi, so'nggi audit.</summary>
    [HttpGet]
    [ProducesResponseType<AdminDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminDashboardDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAdminDashboardQuery(), cancellationToken));
}
