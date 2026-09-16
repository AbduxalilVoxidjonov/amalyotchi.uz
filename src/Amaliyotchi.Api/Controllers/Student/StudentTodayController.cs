using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student;
using Amaliyotchi.Application.Features.Student.CheckIn;
using Amaliyotchi.Application.Features.Student.CheckOut;
using Amaliyotchi.Application.Features.Student.Common;
using Amaliyotchi.Application.Features.Student.Today;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary>Multipart forma: <c>lat</c>, <c>lng</c>, <c>accuracy</c>, <c>occurredAt</c> va ixtiyoriy
/// <c>photo</c> (selfi, ≤ 5 MB, JPEG/PNG/WebP/HEIC). Sozlama <c>checkinPhotoRequired</c> yoqilgan bo'lsa
/// rasm majburiy.</summary>
public sealed class GeoForm
{
    public double Lat { get; init; }
    public double Lng { get; init; }
    public double Accuracy { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public IFormFile? Photo { get; init; }

    public UploadedFile? ToUploadedFile()
        => Photo is null ? null : new UploadedFile(Photo.FileName, Photo.ContentType, Photo.Length, Photo.OpenReadStream);
}

/// <summary>Rasmsiz JSON so'rov (kontrakt <c>CheckinRequest</c>) — eski klientlar va offline navbat uchun saqlanadi.</summary>
public sealed record GeoJsonRequest(double Lat, double Lng, double Accuracy, DateTimeOffset OccurredAt);

/// <summary>Talabaning bugungi belgilanishi: holat, check-in, check-out. Ko'lam — faqat o'zi (Bearer'dan).
/// Check-in/check-out ikkala formatda qabul qilinadi: <c>multipart/form-data</c> (selfi bilan) va
/// <c>application/json</c> (rasmsiz).</summary>
[ApiController]
[Route("api/student")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentTodayController(ISender sender) : ControllerBase
{
    /// <summary>Bitta selfi (5 MB) + forma maydonlari uchun yetarli chegara.</summary>
    private const long MaxRequestBodyBytes = 6 * 1024 * 1024;

    [HttpGet("today")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TodayDto>> Today(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetStudentTodayQuery(), cancellationToken));

    /// <summary>Kelganini belgilash (multipart, ixtiyoriy <c>photo</c> selfi). 400 — oyna/ish kuni/GPS aniqligi/rasm;
    /// 409 — radius tashqarisi yoki allaqachon belgilangan. Rad etilgan urinish rasmi bilan saqlanadi.</summary>
    [HttpPost("checkin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckIn([FromForm] GeoForm form, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new CheckInCommand(form.Lat, form.Lng, form.Accuracy, form.OccurredAt, form.ToUploadedFile()), cancellationToken));

    /// <summary>Kelganini belgilash (rasmsiz JSON). <c>checkinPhotoRequired</c> yoqilgan bo'lsa — 400.
    /// Bu action'da <c>[Consumes]</c> ATAYLAB yo'q: u cheklovsiz "fallback" bo'lishi shart. Ikkala action'da
    /// ham cheklov bo'lsa, <c>Content-Type</c> siz so'rovda ikkalasi ham mos keladi va marshrutlash
    /// <c>AmbiguousMatchException</c> (500) bilan tugaydi. Cheklovsiz kandidat esa oxirgi tanlanadi:
    /// multipart → yuqoridagi action, boshqa/yo'q tur → shu action → mos formatter yo'q → 415.</summary>
    [HttpPost("checkin")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckInJson(GeoJsonRequest body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new CheckInCommand(body.Lat, body.Lng, body.Accuracy, body.OccurredAt), cancellationToken));

    /// <summary>Ketganini belgilash (multipart, ixtiyoriy <c>photo</c> selfi). 400 — oyna/GPS/rasm;
    /// 409 — avval check-in kerak, allaqachon ketgan, radius tashqarisi.</summary>
    [HttpPost("checkout")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckOut([FromForm] GeoForm form, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new CheckOutCommand(form.Lat, form.Lng, form.Accuracy, form.OccurredAt, form.ToUploadedFile()), cancellationToken));

    /// <summary>Ketganini belgilash (rasmsiz JSON). <c>[Consumes]</c> siz — sabab check-in'dagidek.</summary>
    [HttpPost("checkout")]
    [ProducesResponseType<TodayDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TodayDto>> CheckOutJson(GeoJsonRequest body, CancellationToken cancellationToken)
        => Ok(await sender.Send(
            new CheckOutCommand(body.Lat, body.Lng, body.Accuracy, body.OccurredAt), cancellationToken));
}
