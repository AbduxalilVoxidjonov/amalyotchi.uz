using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.PracticePeriods;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/practice-periods")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminPracticePeriodsController(ISender sender) : ControllerBase
{
    /// <summary>Barcha davrlar (sahifalanmagan), <c>startDate</c> kamayish tartibida.
    /// <c>?status=planned|active|closed</c> — hisoblangan holat bo'yicha ixtiyoriy filtr.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PracticePeriodListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PracticePeriodListItem>>> List(
        [FromQuery] GetPracticePeriodsQuery query, CancellationToken cancellationToken)
        => Ok(await sender.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PracticePeriodDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticePeriodDetail>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPracticePeriodQuery(id), cancellationToken));

    /// <summary>Davr statistikasi: har guruh va jami ko'rsatkichlar (faqat shu davr yozuvlari bo'yicha).</summary>
    [HttpGet("{id:guid}/stats")]
    [ProducesResponseType<PracticePeriodStats>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticePeriodStats>> Stats(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPracticePeriodStatsQuery(id), cancellationToken));

    /// <summary>Guruh talabalarining shu davrdagi natijalari. Guruh davrga biriktirilmagan → 404.</summary>
    [HttpGet("{id:guid}/groups/{groupId:guid}/students")]
    [ProducesResponseType<PeriodGroupStudents>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeriodGroupStudents>> GroupStudents(
        Guid id, Guid groupId, CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetPeriodGroupStudentsQuery(id, groupId), cancellationToken));

    /// <summary>Yangi davr. Vaqt qoidalari global sozlamalardan nusxalanadi. Guruh noto'g'ri → 400;
    /// guruh sanalari kesishadigan boshqa davrda → 409.</summary>
    [HttpPost]
    [ProducesResponseType<PracticePeriodDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PracticePeriodDetail>> Create(
        CreatePracticePeriodCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Nom va sanalar (<c>id</c> route'dan; body'da bo'lmaydi). Yopilgan → 409; faol davrda <c>startDate</c> o'zgarsa → 400.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PracticePeriodDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PracticePeriodDetail>> Update(
        Guid id, UpdatePracticePeriodCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Guruhlar ro'yxatini to'liq almashtirish. Davomati bor guruhni ajratish → 409.</summary>
    [HttpPut("{id:guid}/groups")]
    [ProducesResponseType<PracticePeriodDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PracticePeriodDetail>> SetGroups(
        Guid id, SetPracticePeriodGroupsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary>Davrni yopish. Allaqachon yopilgan → 409.</summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType<PracticePeriodDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PracticePeriodDetail>> Close(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ClosePracticePeriodCommand(id), cancellationToken));

    /// <summary>Soft delete. Davomat yozuvi bor davr → 409 (yopish kerak).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePracticePeriodCommand(id), cancellationToken);
        return NoContent();
    }
}
