using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.Diary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Multipart forma: <c>text</c>, <c>learned?</c>, <c>files</c> (0–5, har biri ≤ 5 MB, rasm/PDF).</summary>
public sealed class DiaryCreateForm
{
    public string Text { get; init; } = string.Empty;
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

    /// <summary>Bugungi hisobotni yuborish. 201; bugungisi allaqachon bor → 409 (qayta yozish so'ralgan bo'lsa — yangilanadi).</summary>
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

        var result = await sender.Send(new CreateDiaryEntryCommand(form.Text, form.Learned, files), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
