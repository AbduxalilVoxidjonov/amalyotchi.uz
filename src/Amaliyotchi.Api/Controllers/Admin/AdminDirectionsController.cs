using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Directions;
using Amaliyotchi.Application.Features.Admin.Groups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/directions")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDirectionsController(ISender sender) : ControllerBase
{
    /// <summary>Breadcrumb uchun bitta yo'nalish.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DirectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DirectionDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDirectionQuery(id), cancellationToken));

    /// <summary><c>{ name, code }</c> → 200. <c>id</c> route'dan; body'da bo'lmaydi.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<DirectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DirectionDto>> Update(
        Guid id, UpdateDirectionCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Soft delete. O'chirilmagan guruhi bo'lsa → 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDirectionCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ isActive }</c> → 200.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<DirectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DirectionDto>> SetStatus(
        Guid id, SetDirectionStatusCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Yo'nalish ichidagi guruhlar ro'yxati. Yo'nalish topilmasa → 404.</summary>
    [HttpGet("{id:guid}/groups")]
    [ProducesResponseType<Paged<GroupRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Paged<GroupRow>>> ListGroups(
        Guid id, [FromQuery] GetDirectionGroupsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query with { DirectionId = id }, cancellationToken));

    /// <summary><c>{ name, course }</c> → 201. Yo'nalish topilmasa → 404; faol o'quv yili yo'q → 409;
    /// guruh nomi shu yo'nalishda takrorlansa → 409.</summary>
    [HttpPost("{id:guid}/groups")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDto>> CreateGroup(
        Guid id, CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { DirectionId = id }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
