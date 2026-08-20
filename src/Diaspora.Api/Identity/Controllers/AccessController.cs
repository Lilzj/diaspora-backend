using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Verification.SubmitVerificationRequest;
using Diaspora.Application.Identity.Queries.GetAccessState;
using Diaspora.Application.Identity.Queries.GetFeatures;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// The progressive access surface: where a member currently stands, what that unlocks, and
/// how to ask for more.
/// <para>
/// Answers come from the Access Gateway against stored state, not from the caller's token.
/// A token issued before a suspension still asserts the old state; these endpoints must not.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/access")]
[Produces("application/json")]
[Authorize]
public sealed class AccessController : ApiController
{
    private readonly ISender _sender;

    public AccessController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the member's access state and the routing decision that follows.</summary>
    [HttpGet("state")]
    [Authorize(Policy = DiasporaPolicies.ReadOwnProfile)]
    [ProducesResponseType<ApiResponse<AccessStateResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccessStateAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAccessStateQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Returns every feature gate and whether it is open for this member.</summary>
    [HttpGet("features")]
    [Authorize(Policy = DiasporaPolicies.ReadOwnProfile)]
    [ProducesResponseType<ApiResponse<FeatureAccessResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFeaturesQuery(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Asks to be considered for Diaspora verification.</summary>
    /// <remarks>
    /// Records the request and hands it to the Trust &amp; Verification Engine. Identity
    /// does not decide the outcome.
    /// </remarks>
    [HttpPost("verification-requests")]
    [Authorize(Policy = DiasporaPolicies.SubmitVerification)]
    [ProducesResponseType<ApiResponse<AccessStateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitVerificationAsync(
        [FromBody] SubmitVerificationRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SubmitVerificationRequestCommand
        {
            Notes = request?.Notes
        }, cancellationToken);

        return HandleResult(result);
    }
}
