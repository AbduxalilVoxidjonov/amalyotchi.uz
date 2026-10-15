using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Supervision.Diaries;
using Amaliyotchi.Domain.Diary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/diaries")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDiariesController(ISender sender) : ControllerBase
{
    /// <summary><c>?status=submitted|seen|rewrite|approved</c> (berilmasa — hammasi, tekshirilmaganlar birinchi);
    /// <c>?limit=</c> — eng ko'p yozuv (sukut 100, maks. 1000).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DiaryReviewEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DiaryReviewEntry>>> List(
        [FromQuery] DiaryStatus? status, [FromQuery] int? limit, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDiariesQuery(status, limit), cancellationToken));

    /// <summary>Kundalikni baholash (<see cref="ReviewDiaryCommand"/>): <c>approve</c> (ball ixtiyoriy) ·
    /// <c>score</c> (ball 1–5 majburiy) · <c>rewrite</c> (izoh majburiy). Tekshiruvchi sifatida joriy admin yoziladi va
    /// audit jurnaliga <c>DiaryReviewed</c> tushadi. Allaqachon ko'rib chiqilgan yozuv → 409.</summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType<DiaryReviewEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiaryReviewEntry>> Review(
        Guid id, ReviewDiaryCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { DiaryId = id }, cancellationToken));
}
