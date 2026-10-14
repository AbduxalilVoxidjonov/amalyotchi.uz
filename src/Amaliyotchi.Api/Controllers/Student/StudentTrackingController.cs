using Amaliyotchi.Api.Infrastructure;
using Amaliyotchi.Application.Common.Security;
using Amaliyotchi.Application.Features.Student.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Amaliyotchi.Api.Controllers.Student;

/// <summary><c>PUT /api/student/tracking/consent</c> tanasi.</summary>
public sealed record TrackingConsentRequest(bool? Consent);

/// <summary>Ish vaqtida fonda joylashuv kuzatuvi (docs/LIVE-JOYLASHUV.md). Ko'lam — faqat o'zi (Bearer'dan).</summary>
[ApiController]
[Route("api/student")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentTrackingController(ISender sender) : ControllerBase
{
    /// <summary>Fonda kuzatuvga rozilik: <c>{ "consent": true }</c> → 204; <c>false</c> — qaytarib olish.
    /// <c>consent</c> yo'q → 400 <c>errors.Consent</c>.</summary>
    [HttpPut("tracking/consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consent(TrackingConsentRequest body, CancellationToken cancellationToken)
    {
        await sender.Send(new SetTrackingConsentCommand(body.Consent), cancellationToken);
        return NoContent();
    }

    /// <summary>Joylashuv paketi (1..100 ping). 200 — <c>{ accepted, tracking, last }</c> (rozilik yo'q / kuzatuv o'chiq
    /// bo'lsa ham 200, <c>accepted: 0</c>); 400 — <c>pings</c> yo'q, bo'sh yoki 100 dan ko'p; 429 — daqiqasiga 30 so'rovdan ko'p.</summary>
    [HttpPost("location-pings")]
    [EnableRateLimiting(RateLimitPolicies.LocationPings)]
    [ProducesResponseType<LocationPingsResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LocationPingsResultDto>> Pings(RecordLocationPingsCommand command, CancellationToken cancellationToken)
        => Ok(await sender.Send(command, cancellationToken));
}
