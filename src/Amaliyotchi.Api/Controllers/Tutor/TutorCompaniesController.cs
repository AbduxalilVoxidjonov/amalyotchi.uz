using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Companies;
using Amaliyotchi.Application.Features.Tutor.Companies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Tutor;

[ApiController]
[Route("api/tutor/companies")]
[Authorize(Policy = Policies.TutorOnly)]
public sealed class TutorCompaniesController(ISender sender) : ControllerBase
{
    /// <summary>Ko'lamdagi talabalar HOZIR aktiv amaliyot o'tayotgan korxonalar (nom bo'yicha).
    /// <c>students</c> — ko'lamdagi aktiv talabalar, <c>totalStudents</c> — butun tizim bo'yicha aktiv.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TutorCompany>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TutorCompany>>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorCompaniesQuery(), cancellationToken));

    /// <summary>Korxona tafsiloti (ko'lam kesimida) — davom etayotgan davrlar bo'yicha aktiv talabalar soni va STIR nazorati bilan.
    /// Ko'lamda biriktirilgan talabasi yo'q korxona → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDetail>> Detail(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorCompanyDetailQuery(id), cancellationToken));

    /// <summary>Shu korxonada hozir aktiv amaliyot o'tayotgan ko'lamdagi talabalar, FISH bo'yicha tartiblangan.
    /// Ko'lamda biriktirilgan talabasi yo'q korxona → 404.</summary>
    [HttpGet("{id:guid}/students")]
    [ProducesResponseType<IReadOnlyList<CompanyStudent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CompanyStudent>>> Students(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorCompanyStudentsQuery(id), cancellationToken));

    /// <summary>Korxonaning check-in QR kodi (chop etish uchun). Ko'lamda biriktirilgan talabasi yo'q korxona → 404.</summary>
    [HttpGet("{id:guid}/checkin-qr")]
    [ProducesResponseType<CompanyCheckInQrDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyCheckInQrDto>> CheckInQr(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorCompanyCheckInQrQuery(id), cancellationToken));

    /// <summary>QR kodni almashtirish (eski QR yaroqsiz). Audit: <c>CompanyQrRotated</c>. Ko'lam tashqarisi → 404.</summary>
    [HttpPost("{id:guid}/checkin-qr/rotate")]
    [ProducesResponseType<CompanyCheckInQrDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyCheckInQrDto>> RotateCheckInQr(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new RotateTutorCompanyCheckInQrCommand(id), cancellationToken));
}
