using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Admin.Admins;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Amaliyotchi.Api.Controllers.Admin;

/// <summary>Body: <c>{ isActive }</c>.</summary>
public sealed record SetAdminStatusRequest(bool IsActive);

/// <summary>Adminlarni boshqarish — faqat bosh admin (<c>SuperAdminOnly</c>; oddiy admin → 403).</summary>
[ApiController]
[Route("api/admin/admins")]
[Authorize(Policy = Policies.SuperAdminOnly)]
public sealed class AdminAdminsController(ISender sender) : ControllerBase
{
    /// <summary>Barcha adminlar → <c>{ items }</c> (bosh adminlar birinchi, keyin FISH bo'yicha).</summary>
    [HttpGet]
    [ProducesResponseType<AdminListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminListDto>> List(CancellationToken cancellationToken)
        => Ok(await sender.Send(new GetAdminsQuery(), cancellationToken));

    /// <summary><c>{ fullName, login, phoneNumber?, password? }</c> → 201 (qator + <c>temporaryPassword</c>: parol berilmagan
    /// bo'lsa server yaratgani, aks holda <c>null</c>). Maydon xatosi → 400; login yoki telefon band → 409.</summary>
    [HttpPost]
    [ProducesResponseType<CreatedAdminDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedAdminDto>> Create(CreateAdminCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Created($"/api/admin/admins/{result.Id}", result);
    }

    /// <summary><c>{ fullName, phoneNumber? }</c> → 200. Topilmasa → 404; telefon band → 409.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<AdminAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminAccountDto>> Update(
        Guid id, UpdateAdminCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command with { Id = id }, cancellationToken));

    /// <summary><c>{ isActive }</c> → 200. Bosh adminni yoki o'zini bloklash → 409. Bloklanganda refresh tokenlar bekor qilinadi.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<AdminAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminAccountDto>> SetStatus(
        Guid id, SetAdminStatusRequest body, CancellationToken cancellationToken)
        => Ok(await sender.Send(new SetAdminStatusCommand(id, body.IsActive), cancellationToken));

    /// <summary>Vaqtinchalik parol yaratadi → 200 <c>{ temporaryPassword }</c>; admin keyingi kirishda parolni almashtiradi.</summary>
    [HttpPost("{id:guid}/password")]
    [ProducesResponseType<AdminPasswordResetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPasswordResetDto>> ResetPassword(Guid id, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ResetAdminPasswordCommand(id), cancellationToken));
}
