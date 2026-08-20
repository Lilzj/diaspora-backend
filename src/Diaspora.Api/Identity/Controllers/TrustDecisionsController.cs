using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Verification.ApplyTrustDecision;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// The inbound channel from the Trust &amp; Verification Engine (Phase B3).
/// <para>
/// This controller is the entire surface through which a verification verdict reaches
/// Identity, and the permission behind it is held by no human role — only by B3's service
/// credential. That is the architectural separation made concrete: Identity owns
/// authentication and access state, B3 owns whether someone is trustworthy, and neither
/// can quietly take over the other's job. Trust policy can be rewritten without touching a
/// line of the authentication subsystem.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/trust/decisions")]
[Produces("application/json")]
[Authorize(Policy = DiasporaPolicies.ApplyTrustDecision)]
public sealed class TrustDecisionsController : ApiController
{
    private readonly ISender _sender;

    public TrustDecisionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Applies a verification verdict to a member's access state.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<AccessStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyAsync(
        [FromBody] ApplyTrustDecisionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new ApplyTrustDecisionCommand
        {
            UserId = request.UserId,
            Outcome = request.Outcome,
            Reason = request.Reason,
            DecidedBy = request.DecidedBy
        }, cancellationToken);

        return HandleResult(result);
    }
}
