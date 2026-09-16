using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Companies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

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

    /// <summary>Korxonaga ariza bergan talabalar (qoralamadan boshqa), FISH bo'yicha tartiblangan —
    /// guruh, tyutor, ariza holati va faol davr ko'rsatkichlari bilan. Korxona topilmasa → 404.</summary>
    [HttpGet("{id:guid}/students")]
    [ProducesResponseType<IReadOnlyList<CompanyStudent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CompanyStudent>>> Students(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompanyStudentsQuery(id), cancellationToken));
}
