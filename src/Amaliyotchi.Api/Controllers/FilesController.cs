using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Files;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers;

/// <summary>Saqlangan fayllarni berish: <c>GET /api/files/{id}</c> (<c>StoredFile.Id</c>). Asl nom va content-type
/// bazadan; ko'lam tekshiruvi <see cref="GetFileQuery"/> da. Yuklash (upload) — har feature'ning o'z endpoint'ida.</summary>
[ApiController]
[Route("api/files")]
public sealed class FilesController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var download = await sender.Send(new GetFileQuery(id), cancellationToken);

        // Brauzer faylni "ochib" yubormasligi uchun (HTML/SVG ichida skript) — hamisha yuklab olish sifatida.
        return File(download.Content, download.File.ContentType, fileDownloadName: download.File.FileName, enableRangeProcessing: true);
    }
}
