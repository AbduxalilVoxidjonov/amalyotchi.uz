using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Today;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/today")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorTodayController(ISender sender) : ControllerBase
{
    /// <summary>Bugungi davomat: statistika, ogohlantirishlar va sahifalangan qatorlar.
    /// <c>?status=present|late|absent|excused|pending|suspicious&amp;q&amp;page&amp;pageSize</c>.</summary>
    [HttpGet]
    [ProducesResponseType<TodayResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TodayResponse>> Get([FromQuery] GetTutorTodayQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));
}
