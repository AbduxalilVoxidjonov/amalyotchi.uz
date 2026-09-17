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

    /// <summary>Amaliyot joyini tanlash: talaba faqat <c>{ tin }</c> (STIR) yuboradi, korxona
    /// ma'lumoti admin oldindan kiritgan yozuvdan olinadi. Ariza tyutorga <c>Submitted</c> holatida
    /// boradi. Faol korxona topilmasa → 404; faol davr yo'q yoki ariza allaqachon bor → 409.</summary>
    [HttpPost]
    [ProducesResponseType<PracticePlaceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PracticePlaceDto>> Submit(
        SubmitPracticePlaceCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
