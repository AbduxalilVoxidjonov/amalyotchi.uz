using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Student.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

/// <summary>Multipart forma: <c>file</c> — shablon bo'yicha to'ldirilgan <c>.xlsx</c>.</summary>
public sealed class CompanyImportForm
{
    public IFormFile? File { get; init; }
}

[ApiController]
[Route("api/admin/companies")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminCompaniesController(ISender sender) : ControllerBase
{
    /// <summary>Korxonalar ro'yxati. <c>q</c> — nom, STIR yoki manzil bo'yicha qidiruv.
    /// <c>maxStudents</c>/<c>overLimit</c> — <c>maxStudentsPerCompany</c> sozlamasi bo'yicha STIR nazorati.</summary>
    [HttpGet]
    [ProducesResponseType<Paged<CompanyRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<CompanyRow>>> List([FromQuery] GetCompaniesQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Korxona tafsiloti: rekvizitlar, koordinata, rahbar/mentor, talabalar soni,
    /// STIR nazorati va amaliyot davrlari kesimi. Korxona topilmasa → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDetail>> Detail(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompanyDetailQuery(id), cancellationToken));

    /// <summary>Yangi korxona (admin oldindan kiritadi — talaba keyin faqat STIR ni yozadi).
    /// STIR band bo'lsa → 409.</summary>
    [HttpPost]
    [ProducesResponseType<CompanyDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetail>> Create(CreateCompanyCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Korxonani tahrirlash. <c>id</c> route'dan; topilmasa → 404, STIR band bo'lsa → 409.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CompanyDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDetail>> Update(
        Guid id, UpdateCompanyCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Korxonaning check-in QR kodi (chop etish uchun): <c>payload</c> — <c>AMLQR:1:{token}</c>.
    /// Korxona topilmasa → 404.</summary>
    [HttpGet("{id:guid}/checkin-qr")]
    [ProducesResponseType<CompanyCheckInQrDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyCheckInQrDto>> CheckInQr(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompanyCheckInQrQuery(id), cancellationToken));

    /// <summary>QR kodni almashtirish — eski (osilgan) QR darhol yaroqsiz bo'ladi. Audit: <c>CompanyQrRotated</c>.
    /// Korxona topilmasa → 404.</summary>
    [HttpPost("{id:guid}/checkin-qr/rotate")]
    [ProducesResponseType<CompanyCheckInQrDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyCheckInQrDto>> RotateCheckInQr(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RotateCompanyCheckInQrCommand(id), cancellationToken));

    /// <summary>Faol/faol emas. Faolsizlantirilgan korxona STIR qidiruvida chiqmaydi
    /// (<c>GET /api/companies/lookup</c> → 404) — talaba uni tanlay olmaydi.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<CompanyDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDetail>> SetStatus(
        Guid id, SetCompanyStatusCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Arxivlash (soft delete). Korxona hali faol bo'lsa yoki unga talaba biriktirilgan bo'lsa → 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteCompanyCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Ko'p korxonani bir vaqtda yuklash uchun <c>.xlsx</c> shablon
    /// ("Korxonalar" sarlavhasi + "Yo'riqnoma").</summary>
    [HttpGet("import/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ImportTemplate(CancellationToken cancellationToken)
    {
        var content = await sender.Send(new GetCompanyImportTemplateQuery(), cancellationToken);
        return File(content, ExcelImport.ContentType, CompanyImportLimits.TemplateFileName);
    }

    /// <summary>Shablon bo'yicha to'ldirilgan Excel'dan korxonalarni ommaviy qo'shish (qisman import:
    /// xato qatorlar hisobotda qaytadi, to'g'rilari saqlanadi).</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ExcelImport.MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ExcelImport.MaxFileBytes + 64 * 1024)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ImportResult>> Import(
        [FromForm] CompanyImportForm form, CancellationToken cancellationToken)
    {
        var file = form.File is { } uploaded
            ? new UploadedFile(uploaded.FileName, uploaded.ContentType, uploaded.Length, uploaded.OpenReadStream)
            : null;

        return Ok(await sender.Send(new ImportCompaniesCommand(file), cancellationToken));
    }

    /// <summary>Korxonaga ariza bergan talabalar (qoralamadan boshqa), FISH bo'yicha tartiblangan —
    /// guruh, tyutor, ariza holati va faol davr ko'rsatkichlari bilan. Korxona topilmasa → 404.</summary>
    [HttpGet("{id:guid}/students")]
    [ProducesResponseType<IReadOnlyList<CompanyStudent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CompanyStudent>>> Students(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompanyStudentsQuery(id), cancellationToken));
}
