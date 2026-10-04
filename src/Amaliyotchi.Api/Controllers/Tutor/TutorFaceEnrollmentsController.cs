using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Tutor.Faces;
using Amaliyotchi.Domain.Faces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

/// <summary>Tana: <c>{ reason }</c> — majburiy, ≤ 500 belgi.</summary>
public sealed record RejectFaceRequest(string? Reason);

/// <summary>"Yuzni tasdiqlash": talabalar etalon yuz rasmlarini ko'rib chiqish. Tyutor — o'z ko'lami (tashqarisi → 404),
/// admin — hamma talabalar.</summary>
[ApiController]
[Route("api/tutor")]
[Authorize(Policy = Policies.TutorOrAdmin)]
public sealed class TutorFaceEnrollmentsController(ISender sender) : ControllerBase
{
    /// <summary>Etalonlar ro'yxati: <c>?status=pending|approved|rejected</c> (sukut — <c>pending</c>) → <c>{ items }</c>.</summary>
    [HttpGet("face-enrollments")]
    [ProducesResponseType<FaceEnrollmentList>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaceEnrollmentList>> List(
        [FromQuery] FaceEnrollmentStatus? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetFaceEnrollmentsQuery(status), cancellationToken));

    /// <summary>Kutilayotgan etalonni tasdiqlash. Etalon yo'q yoki pending emas → 409.</summary>
    [HttpPost("students/{studentId:guid}/face/approve")]
    [ProducesResponseType<StudentFaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentFaceDto>> Approve(Guid studentId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ApproveFaceEnrollmentCommand(studentId), cancellationToken));

    /// <summary>Etalonni rad etish (sabab bilan). Bo'sh sabab → 400 (<c>errors.Reason</c>); etalon yo'q yoki allaqachon
    /// rad etilgan → 409.</summary>
    [HttpPost("students/{studentId:guid}/face/reject")]
    [ProducesResponseType<StudentFaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentFaceDto>> Reject(
        Guid studentId, RejectFaceRequest body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RejectFaceEnrollmentCommand(studentId, body.Reason ?? string.Empty), cancellationToken));

    /// <summary>Etalonni bekor qilish → holat <c>none</c> (talaba qayta yuboradi). Etalon yo'q → 409.</summary>
    [HttpPost("students/{studentId:guid}/face/reset")]
    [ProducesResponseType<StudentFaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentFaceDto>> Reset(Guid studentId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ResetFaceEnrollmentCommand(studentId), cancellationToken));
}
