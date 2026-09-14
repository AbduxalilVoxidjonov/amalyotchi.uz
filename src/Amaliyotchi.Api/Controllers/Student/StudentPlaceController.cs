using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Place;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

[ApiController]
[Route("api/student/place")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentPlaceController(ISender sender) : ControllerBase
{
    /// <summary>Amaliyot joyi (korxona, ariza holati, shartnoma). Biriktirilmagan → 404.</summary>
    [HttpGet]
    [ProducesResponseType<PracticePlaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticePlaceDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPracticePlaceQuery(), cancellationToken));
}
