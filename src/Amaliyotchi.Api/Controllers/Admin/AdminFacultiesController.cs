using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Departments;
using Amaliyotchi.Application.Features.Admin.Faculties;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/faculties")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminFacultiesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Paged<FacultyRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<FacultyRow>>> List([FromQuery] GetFacultiesQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Breadcrumb uchun bitta fakultet.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<FacultyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacultyDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetFacultyQuery(id), cancellationToken));

    /// <summary><c>{ name, code }</c> → 201. Kod takrorlansa (faol fakultetlar orasida) → 409.</summary>
    [HttpPost]
    [ProducesResponseType<FacultyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacultyDto>> Create(CreateFacultyCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary><c>{ name, code }</c> → 200. <c>id</c> route'dan; body'da bo'lmaydi.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<FacultyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FacultyDto>> Update(
        Guid id, UpdateFacultyCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Soft delete. Bog'liq guruh/tyutor/talaba bo'lsa → 409.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteFacultyCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ isActive }</c> → 200. Faol emas fakultet ro'yxatda qoladi.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<FacultyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FacultyDto>> SetStatus(
        Guid id, SetFacultyStatusCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Fakultet ichidagi kafedralar ro'yxati. Fakultet topilmasa → 404.</summary>
    [HttpGet("{id:guid}/departments")]
    [ProducesResponseType<Paged<DepartmentRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Paged<DepartmentRow>>> ListDepartments(
        Guid id, [FromQuery] GetDepartmentsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query with { FacultyId = id }, cancellationToken));

    /// <summary><c>{ name, code }</c> → 201. Fakultet topilmasa → 404; kod takrorlansa → 409.</summary>
    [HttpPost("{id:guid}/departments")]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment(
        Guid id, CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command with { FacultyId = id }, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
