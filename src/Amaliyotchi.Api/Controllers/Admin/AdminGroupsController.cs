using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Groups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/groups")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminGroupsController(ISender sender) : ControllerBase
{
    /// <summary>Global ro'yxat — barcha guruhlar, filtrsiz. Marshrut o'zgarmaydi (frontend'da global
    /// "Guruhlar" menyusi olib tashlansa ham, backend endpoint saqlanadi).</summary>
    [HttpGet]
    [ProducesResponseType<Paged<GroupRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<GroupRow>>> List([FromQuery] GetGroupsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Breadcrumb uchun bitta guruh.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetGroupQuery(id), cancellationToken));

    /// <summary><c>{ name, course }</c> → 200. <c>id</c> route'dan; body'da bo'lmaydi.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDto>> Update(
        Guid id, UpdateGroupCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Soft delete. O'chirilmagan talabasi yoki faol tyutor biriktiruvi bo'lsa → 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteGroupCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ isActive }</c> → 200.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> SetStatus(
        Guid id, SetGroupStatusCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));
}
