using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers;

/// <summary>Hisobotlar katalogi (admin + tyutor, ko'lam Bearer'dan). Yuklab olish — keyingi bosqich (M14).</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.TutorOrAdmin)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ReportsCatalog>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportsCatalog>> Catalog(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetReportsCatalogQuery(), cancellationToken));
}
