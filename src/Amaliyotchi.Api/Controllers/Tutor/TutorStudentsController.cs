using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Students;
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
    /// statistikasi va baho — tanlangan davr bo'yicha (<c>?periodId=</c>, berilmasa sukut bo'yicha davr).
    /// Ko'lamdan tashqari talaba yoki begona <c>periodId</c> → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TutorStudentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorStudentDetail>> Detail(Guid id, [FromQuery] Guid? periodId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDetailQuery(id, periodId), cancellationToken));

    /// <summary>Talabaning kun-bakun davomati: holat, check-in/out vaqti, masofa, koordinata, rasm va
    /// urinishlar soni. <c>?from=&amp;to=</c> — ixtiyoriy (berilmasa davr boshidan bugungacha);
    /// teskari yoki 400 kundan uzun oraliq → 400. <c>?periodId=</c> — davr (berilmasa sukut bo'yicha), oraliq davr chegaralariga qisiladi.
    /// Davr bo'lmasa — bo'sh massiv.</summary>
    [HttpGet("{id:guid}/attendance")]
    [ProducesResponseType<IReadOnlyList<StudentAttendanceDay>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StudentAttendanceDay>>> Attendance(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? periodId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentAttendanceQuery(id, from, to, periodId), cancellationToken));

    /// <summary>Talabaning tanlangan davrdagi (<c>?periodId=</c>) kundaliklari — sana bo'yicha kamayish tartibida. Ko'lamdan tashqari talaba → 404.</summary>
    [HttpGet("{id:guid}/diaries")]
    [ProducesResponseType<IReadOnlyList<TutorDiaryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorDiaryEntry>>> Diaries(
        Guid id, [FromQuery] Guid? periodId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDiariesQuery(id, periodId), cancellationToken));

    /// <summary>Talabaga brauzer orqali (HEMIS ID + parol) kirish uchun vaqtinchalik parol o'rnatish: <c>{ password }</c> → 204.
    /// Talaba keyingi kirishda parolini almashtirishi kerak (<c>mustChangePassword=true</c>); barcha refresh tokenlari
    /// bekor qilinadi. Audit: <c>StudentPasswordSet</c>. Ko'lamdan tashqari (yoki yo'q) talaba → 404.</summary>
    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(
        Guid id, SetStudentPasswordCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }
}
