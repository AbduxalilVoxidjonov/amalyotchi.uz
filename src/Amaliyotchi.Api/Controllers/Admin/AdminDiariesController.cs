using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/diaries")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDiariesController(ISender sender) : ControllerBase
{
    /// <summary>Kundalikni baholash — tyutornikidek (<c>POST /api/tutor/diaries/{id}/review</c>, ayni
    /// <see cref="ReviewDiaryCommand"/>): <c>approve</c> (ball ixtiyoriy) · <c>score</c> (ball 1–5 majburiy) ·
    /// <c>rewrite</c> (izoh majburiy). Tekshiruvchi sifatida joriy foydalanuvchi (admin) yoziladi va
    /// audit jurnaliga <c>DiaryReviewed</c> tushadi. Allaqachon ko'rib chiqilgan yozuv → 409.
    /// Admin ko'lami cheklovsiz — har qanday talabaning kundaligi ko'rinadi.</summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType<TutorDiaryEntry>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorDiaryEntry>> Review(
        Guid id, ReviewDiaryCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { DiaryId = id }, cancellationToken));
}
