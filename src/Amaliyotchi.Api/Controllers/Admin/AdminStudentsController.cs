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
        return File(content, ExcelImport.ContentType, StudentImportLimits.TemplateFileName);
    }

    /// <summary>Shablon bo'yicha to'ldirilgan Excel'dan talabalarni ommaviy qo'shish. Xato qatorlar
    /// tashlab yuboriladi va hisobotda qaytadi, to'g'rilari saqlanadi (qisman import). Fayl o'qilmasa,
    /// sarlavha topilmasa yoki qatorlar chegaradan ko'p bo'lsa → 400.</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImport.MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExcelImport.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ImportResult>> Import(
        [FromForm] StudentImportForm form, CancellationToken cancellationToken)
    {
        var file = form.File is { } uploaded
            ? new UploadedFile(uploaded.FileName, uploaded.ContentType, uploaded.Length, uploaded.OpenReadStream)
            : null;

        return Ok(await sender.Send(new ImportStudentsCommand(file), cancellationToken));
    }

    /// <summary>Belgilangan talabalarni korxonaga biriktirish: har biriga tasdiqlangan ariza
    /// yaratiladi (talaba ariza bermaydi). Korxona topilmasa → 404, faol bo'lmasa → 409;
    /// alohida talabalar sabab bilan hisobotda tashlab yuboriladi.</summary>
    [HttpPost("assign-company")]
    [ProducesResponseType<AssignCompanyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignCompanyResult>> AssignCompany(
        AssignStudentsToCompanyCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));

    /// <summary>Talaba profili: akademik ma'lumot, tyutor, korxona, ariza, amaliyot davri,
    /// davomat/kundalik statistikasi va baho — tanlangan davr bo'yicha (<c>?periodId=</c>, berilmasa sukut
    /// bo'yicha davr). Talaba topilmasa yoki begona <c>periodId</c> → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<AdminStudentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminStudentDetail>> Detail(Guid id, [FromQuery] Guid? periodId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAdminStudentDetailQuery(id, periodId), cancellationToken));

    /// <summary>Talaba profilidan korxonaga biriktirish yoki boshqa korxonaga o'tkazish: <c>{ companyId, comment? }</c> →
    /// 200 yangilangan profil (sukut davri bo'yicha, <c>GET {id}</c> bilan bir xil). Davrdagi ochiq ariza <c>transferred</c> holatiga o'tadi
    /// (tarix saqlanadi), yangi korxonaga tasdiqlangan ariza yaratiladi. Talaba/korxona topilmasa → 404; korxona yoki
    /// talaba faol emas, guruhida ochiq davr yo'q, allaqachon shu korxonada → 409; <c>companyId</c> bo'sh → 400.</summary>
    [HttpPost("{id:guid}/company")]
    [ProducesResponseType<AdminStudentDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminStudentDetail>> ReassignCompany(
        Guid id, ReassignStudentCompanyCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Talabaning kun-bakun davomati (tyutor profilidagi bilan bir xil shakl):
    /// holat, check-in/out vaqti, masofa, koordinata, selfi va urinishlar soni.
    /// <c>?from=&amp;to=</c> — ixtiyoriy (berilmasa davr boshidan bugungacha); teskari yoki
    /// 400 kundan uzun oraliq → 400. <c>?periodId=</c> — davr (berilmasa sukut bo'yicha), oraliq davr chegaralariga qisiladi.
    /// Davr bo'lmasa — bo'sh massiv.</summary>
    [HttpGet("{id:guid}/attendance")]
    [ProducesResponseType<IReadOnlyList<StudentAttendanceDay>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<StudentAttendanceDay>>> Attendance(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? periodId,
        CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentAttendanceQuery(id, from, to, periodId), cancellationToken));

    /// <summary>Talabaning tanlangan davrdagi (<c>?periodId=</c>) kundaliklari — sana bo'yicha kamayish tartibida. Talaba topilmasa → 404.</summary>
    [HttpGet("{id:guid}/diaries")]
    [ProducesResponseType<IReadOnlyList<TutorDiaryEntry>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TutorDiaryEntry>>> Diaries(
        Guid id, [FromQuery] Guid? periodId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorStudentDiariesQuery(id, periodId), cancellationToken));

    /// <summary>Talabaga brauzer orqali (HEMIS ID + parol) kirish uchun vaqtinchalik parol o'rnatish: <c>{ password }</c> → 204.
    /// Talaba keyingi kirishda parolini almashtirishi kerak (<c>mustChangePassword=true</c>); barcha refresh tokenlari
    /// bekor qilinadi. Audit: <c>StudentPasswordSet</c>. Talaba topilmasa → 404.</summary>
    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(
        Guid id, SetStudentPasswordCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }
}
