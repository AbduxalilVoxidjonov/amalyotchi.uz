using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Domain.Diary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/diaries")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorDiariesController(ISender sender) : ControllerBase
{
    /// <summary><c>?status=submitted|seen|rewrite|approved</c> (berilmasa — hammasi, tekshirilmaganlar birinchi).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TutorDiaryEntry>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorDiaryEntry>>> List(
        [FromQuery] DiaryStatus? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorDiariesQuery(status), cancellationToken));

    /// <summary><c>approve</c> (ball ixtiyoriy) · <c>score</c> (ball 1–5 majburiy) · <c>rewrite</c> (izoh majburiy).</summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType<TutorDiaryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorDiaryEntry>> Review(
        Guid id, ReviewDiaryCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { DiaryId = id }, cancellationToken));
}
