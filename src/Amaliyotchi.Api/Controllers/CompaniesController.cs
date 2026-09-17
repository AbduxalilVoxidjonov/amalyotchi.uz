using Amaliyotchi.Application.Features.Companies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers;

/// <summary>Barcha rollar uchun ochiq korxona qidiruvi. Talaba korxona ma'lumotini QO'LDA kiritmaydi:
/// u faqat STIR yozadi va admin oldindan kiritgan yozuv chiqadi.</summary>
[ApiController]
[Route("api/companies")]
[Authorize]
public sealed class CompaniesController(ISender sender) : ControllerBase
{
    /// <summary>STIR bo'yicha FAOL korxonani topadi. Faolsizlantirilgan yoki arxivlangan korxona
    /// bu yerda umuman ko'rinmaydi → 404. STIR formati noto'g'ri bo'lsa → 400.</summary>
    [HttpGet("lookup")]
    [ProducesResponseType<CompanyLookupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyLookupDto>> Lookup(
        [FromQuery] string tin, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetCompanyByTinQuery(tin), cancellationToken));
}
