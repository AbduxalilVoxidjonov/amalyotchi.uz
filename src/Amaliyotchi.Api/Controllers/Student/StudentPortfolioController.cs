using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Portfolio;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/portfolio")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentPortfolioController(ISender sender) : ControllerBase
{
    /// <summary>Amaliyot portfoliosi: statistika, baho tarkibi, tyutor xulosasi. Faol davr yo'q → 404.</summary>
    [HttpGet]
    [ProducesResponseType<PortfolioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPortfolioQuery(), cancellationToken));
}
