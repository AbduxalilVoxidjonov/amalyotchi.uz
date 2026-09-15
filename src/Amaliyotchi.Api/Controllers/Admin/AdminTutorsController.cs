using Amaliyotchi.Application.Common.Models;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Tutors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/tutors")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminTutorsController(ISender sender) : ControllerBase
{
    /// <summary><c>?q&amp;facultyId&amp;page&amp;pageSize</c> — <c>facultyId</c> ixtiyoriy filtr.</summary>
    [HttpGet]
    [ProducesResponseType<Paged<TutorRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Paged<TutorRow>>> List([FromQuery] GetTutorsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    /// <summary>Tyutor kartasi: hisob + faol guruh biriktiruvlari. Topilmasa yoki roli tyutor bo'lmasa → 404.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TutorDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TutorDetail>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorQuery(id), cancellationToken));

    /// <summary><c>{ fullName, hemisId, phone?, password, facultyId }</c> → 201 + Location. Fakultet topilmasa → 404;
    /// fakultet faol emas, HEMIS ID yoki telefon band → 409.</summary>
    [HttpPost]
    [ProducesResponseType<TutorDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorDetail>> Create(CreateTutorCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary><c>{ fullName, phone?, facultyId }</c> → 200. <c>id</c> route'dan; body'da bo'lmaydi.
    /// Fakultet o'zgarsa-yu faol biriktiruvlar bo'lsa → 409.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TutorDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorDetail>> Update(
        Guid id, UpdateTutorCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary><c>{ isActive }</c> → 204. <c>false</c> — hisob yopiladi, refresh tokenlari bekor qilinadi.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        Guid id, SetTutorStatusCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ password }</c> → 204. Barcha refresh tokenlari bekor qilinadi — tyutor qayta kiradi.</summary>
    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id, ResetTutorPasswordCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    /// <summary><c>{ groupIds }</c> → 200. Faol biriktiruvlar to'plamini almashtiradi (tarix saqlanadi).
    /// Guruh topilmasa → 404; faol emas / begona fakultet → 400; boshqa tyutorda yoki faol o'quv yili yo'q → 409.</summary>
    [HttpPut("{id:guid}/groups")]
    [ProducesResponseType<TutorDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TutorDetail>> SetGroups(
        Guid id, SetTutorGroupsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Tyutor fakultetidagi barcha faol guruhlar + hozirgi tyutori — biriktirish oynasi uchun.</summary>
    [HttpGet("{id:guid}/available-groups")]
    [ProducesResponseType<IReadOnlyList<AvailableGroupRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AvailableGroupRow>>> AvailableGroups(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetTutorAvailableGroupsQuery(id), cancellationToken));
}
