using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Nav;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/nav")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorNavController(ISender sender) : ControllerBase
{
    /// <summary>Sidebar badge'lari (ko'lam bo'yicha) va header konteksti (guruhlar, joriy davr nomi).</summary>
    [HttpGet]
    [ProducesResponseType<TutorNavDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TutorNavDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorNavQuery(), cancellationToken));
}
