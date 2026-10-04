using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Faces;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Face;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Multipart forma: <c>photo</c> — etalon selfi (≤ 5 MB, JPEG/PNG/WebP/HEIC), <c>consent</c> — "true"
/// (biometrik ma'lumotni qayta ishlashga rozilik).</summary>
public sealed class FaceEnrollmentForm
{
    public IFormFile? Photo { get; init; }
    public string? Consent { get; init; }

    public SubmitStudentFaceCommand ToCommand()
        => new(
            Photo is null ? null : new UploadedFile(Photo.FileName, Photo.ContentType, Photo.Length, Photo.OpenReadStream),
            string.Equals(Consent?.Trim(), "true", StringComparison.OrdinalIgnoreCase));
}

/// <summary>"Yuzni tasdiqlash": talabaning etalon yuz rasmi. Ko'lam — faqat o'zi (Bearer'dan).</summary>
[ApiController]
[Route("api/student/face")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentFaceController(ISender sender) : ControllerBase
{
    /// <summary>Bitta rasm (5 MB) + forma maydonlari uchun yetarli chegara.</summary>
    private const long MaxRequestBodyBytes = 6 * 1024 * 1024;

    /// <summary>Etalon holati: <c>none|pending|approved|rejected</c>, <c>required</c> — sozlama <c>faceVerificationEnabled</c>.</summary>
    [HttpGet]
    [ProducesResponseType<StudentFaceDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentFaceDto>> Get(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentFaceQuery(), cancellationToken));

    /// <summary>Etalon selfi yuborish → 200 (<c>pending</c>). 400 — rozilik yo'q (<c>errors.Consent</c>), rasm qoidalari,
    /// yuz topilmadi / bir nechta yuz (<c>errors.Photo</c>); 409 — allaqachon tasdiqlangan; 503 — yuz modellari ishlamayapti.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    [ProducesResponseType<StudentFaceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StudentFaceDto>> Submit([FromForm] FaceEnrollmentForm form, CancellationToken cancellationToken)
        => Ok(await sender.Send(form.ToCommand(), cancellationToken));
}
