using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Grading;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/grading")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorGradingController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GradingRow>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GradingRow>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetGradingQuery(), cancellationToken));

    /// <summary>Tyutor (0–20) va tavsifnoma (0–10) ballari; jami va baho server tomonidan qayta hisoblanadi.</summary>
    [HttpPut("{studentId:guid}")]
    [ProducesResponseType<GradingRow>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradingRow>> Update(
        Guid studentId, UpdateGradingCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { StudentId = studentId }, cancellationToken));
}
