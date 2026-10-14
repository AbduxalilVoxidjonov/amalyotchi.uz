using System.Globalization;
using Amaliyotchi.Application.Common.Exceptions;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Diary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Multipart forma: <c>date?</c> ("YYYY-MM-DD", Toshkent; berilmasa bugun), <c>text</c> (files ichida PDF bo'lsa
/// ixtiyoriy), <c>learned?</c>, <c>files</c> (0–5, har biri ≤ 5 MB, rasm/PDF).</summary>
public sealed class DiaryCreateForm
{
    public string? Date { get; init; }
    public string? Text { get; init; }
    public string? Learned { get; init; }
    public List<IFormFile> Files { get; init; } = [];
}

[ApiController]
[Route("api/student/diary")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentDiaryController(ISender sender) : ControllerBase
{
    /// <summary>Barcha jami hajm chegarasi: 5 fayl × 5 MB + matn.</summary>
    private const long MaxRequestBodyBytes = 6 * 5 * 1024 * 1024;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DiaryEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DiaryEntryDto>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentDiaryQuery(), cancellationToken));

    /// <summary>Kun (sukut — bugun) hisobotini yuborish. 201; shu kunga yozuv allaqachon bor → 409 (qayta yozish so'ralgan
    /// bo'lsa — yangilanadi); kelajak / dam olish kuni / davr tashqarisi → 400 <c>errors.Date</c>.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    [ProducesResponseType<DiaryEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiaryEntryDto>> Create([FromForm] DiaryCreateForm form, CancellationToken cancellationToken)
    {
        var files = form.Files
            .Select(f => new UploadedFile(f.FileName, f.ContentType, f.Length, f.OpenReadStream))
            .ToList();

        var result = await sender.Send(
            new CreateDiaryEntryCommand(form.Text ?? string.Empty, form.Learned, files, ParseDate(form.Date)), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    public const string InvalidDateMessage = "Sana noto'g'ri formatda — YYYY-MM-DD kutiladi.";

    /// <summary>Bo'sh → null (bugun); noto'g'ri format → 400 <c>errors.Date</c>.</summary>
    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new ValidationException(
            new Dictionary<string, string[]> { ["Date"] = [InvalidDateMessage] }, InvalidDateMessage);
    }
}
