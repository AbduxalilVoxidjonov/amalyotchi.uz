using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/students")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorStudentsController(ISender sender) : ControllerBase
{
    /// <summary>Ko'lamdagi talabalar ro'yxati — davomat va kundalik ko'rsatkichlari bilan.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TutorStudent>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TutorStudent>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentsQuery(), cancellationToken));

    /// <summary>Talaba profili: akademik ma'lumot, korxona, ariza, amaliyot davri, davomat/kundalik
    /// statistikasi va joriy baho. Ko'lamdan tashqari talaba → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TutorStudentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorStudentDetail>> Detail(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDetailQuery(id), cancellationToken));

    /// <summary>Talabaning kun-bakun davomati: holat, check-in/out vaqti, masofa, koordinata, rasm va
    /// urinishlar soni. <c>?from=&amp;to=</c> — ixtiyoriy (berilmasa davr boshidan bugungacha);
    /// teskari yoki 400 kundan uzun oraliq → 400. Faol davr bo'lmasa — bo'sh massiv.</summary>
    [HttpGet("{id:guid}/attendance")]
    [ProducesResponseType<IReadOnlyList<StudentAttendanceDay>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StudentAttendanceDay>>> Attendance(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentAttendanceQuery(id, from, to), cancellationToken));

    /// <summary>Talabaning kundaliklari — sana bo'yicha kamayish tartibida. Ko'lamdan tashqari talaba → 404.</summary>
    [HttpGet("{id:guid}/diaries")]
    [ProducesResponseType<IReadOnlyList<TutorDiaryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorDiaryEntry>>> Diaries(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDiariesQuery(id), cancellationToken));
}
