using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Departments;
using Amaliyotchi.Application.Features.Admin.Directions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/departments")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminDepartmentsController(ISender sender) : ControllerBase
{
    /// <summary>Breadcrumb uchun bitta kafedra.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetDepartmentQuery(id), cancellationToken));

    /// <summary><c>{ name, code }</c> → 200. <c>id</c> route'dan; body'da bo'lmaydi.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Update(
        Guid id, UpdateDepartmentCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Soft delete. O'chirilmagan yo'nalishi bo'lsa → 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDepartmentCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ isActive }</c> → 200.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> SetStatus(
        Guid id, SetDepartmentStatusCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Kafedra ichidagi yo'nalishlar ro'yxati. Kafedra topilmasa → 404.</summary>
    [HttpGet("{id:guid}/directions")]
    [ProducesResponseType<Paged<DirectionRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Paged<DirectionRow>>> ListDirections(
        Guid id, [FromQuery] GetDirectionsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query with { DepartmentId = id }, cancellationToken));

    /// <summary><c>{ name, code }</c> → 201. Kafedra topilmasa → 404; kod takrorlansa → 409.</summary>
    [HttpPost("{id:guid}/directions")]
    [ProducesResponseType<DirectionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DirectionDto>> CreateDirection(
        Guid id, CreateDirectionCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { DepartmentId = id }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
