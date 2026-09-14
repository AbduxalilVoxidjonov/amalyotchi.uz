using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Audit;
using Amaliyotchi.Application.Features.Admin.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminAuditController(ISender sender) : ControllerBase
{
    /// <summary>Audit jurnali: <c>?q=&amp;page=&amp;pageSize=&amp;action=settingsChanged</c>.</summary>
    [HttpGet]
    [ProducesResponseType<Paged<AuditEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<AuditEntryDto>>> List([FromQuery] GetAuditLogQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
