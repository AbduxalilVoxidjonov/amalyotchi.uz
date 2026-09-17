using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Students;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Tutor.Diaries;
using Amaliyotchi.Application.Features.Tutor.Students;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

/// <summary>Multipart forma: <c>file</c> — shablon bo'yicha to'ldirilgan <c>.xlsx</c>.</summary>
public sealed class StudentImportForm
{
    public IFormFile? File { get; init; }
}

[ApiController]
[Route("api/admin/students")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminStudentsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<StudentRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<StudentRow>>> List([FromQuery] GetAdminStudentsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>To'ldirish uchun <c>.xlsx</c> shablon: "Talabalar" (sarlavha qatori), "Yo'riqnoma" va
    /// mavjud faol guruhlar ro'yxati ("Guruhlar"). Import shu nomlar bo'yicha o'qiydi.</summary>
    [HttpGet("import/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportTemplate(CancellationToken cancellationToken)
    {
        var content = await sender.Send(new GetStudentImportTemplateQuery(), cancellationToken);
        return File(content, StudentImportLimits.ContentType, StudentImportLimits.TemplateFileName);
    }

    /// <summary>Shablon bo'yicha to'ldirilgan Excel'dan talabalarni ommaviy qo'shish. Xato qatorlar
    /// tashlab yuboriladi va hisobotda qaytadi, to'g'rilari saqlanadi (qisman import). Fayl o'qilmasa,
    /// sarlavha topilmasa yoki qatorlar chegaradan ko'p bo'lsa → 400.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(StudentImportLimits.MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = StudentImportLimits.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<StudentImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentImportResult>> Import(
        [FromForm] StudentImportForm form, CancellationToken cancellationToken)
    {
        var file = form.File is { } uploaded
            ? new UploadedFile(uploaded.FileName, uploaded.ContentType, uploaded.Length, uploaded.OpenReadStream)
            : null;

        return Ok(await sender.Send(new ImportStudentsCommand(file), cancellationToken));
    }

    /// <summary>Talaba profili: akademik ma'lumot, tyutor, korxona, ariza, amaliyot davri,
    /// davomat/kundalik statistikasi va joriy baho. Talaba topilmasa → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminStudentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminStudentDetail>> Detail(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAdminStudentDetailQuery(id), cancellationToken));

    /// <summary>Talabaning kun-bakun davomati (tyutor profilidagi bilan bir xil shakl):
    /// holat, check-in/out vaqti, masofa, koordinata, selfi va urinishlar soni.
    /// <c>?from=&amp;to=</c> — ixtiyoriy (berilmasa davr boshidan bugungacha); teskari yoki
    /// 400 kundan uzun oraliq → 400. Faol davr bo'lmasa — bo'sh massiv.</summary>
    [HttpGet("{id:guid}/attendance")]
    [ProducesResponseType<IReadOnlyList<StudentAttendanceDay>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StudentAttendanceDay>>> Attendance(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentAttendanceQuery(id, from, to), cancellationToken));

    /// <summary>Talabaning kundaliklari — sana bo'yicha kamayish tartibida. Talaba topilmasa → 404.</summary>
    [HttpGet("{id:guid}/diaries")]
    [ProducesResponseType<IReadOnlyList<TutorDiaryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorDiaryEntry>>> Diaries(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDiariesQuery(id), cancellationToken));
}
