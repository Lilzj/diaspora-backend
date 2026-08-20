using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Sessions.RevokeAllOtherSessions;
using Diaspora.Application.Identity.Commands.Sessions.RevokeSession;
using Diaspora.Application.Identity.Queries.GetSecurityEvents;
using Diaspora.Application.Identity.Queries.GetSessions;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Session management and the member's own security history.
/// <para>
/// A member cannot defend an account they cannot see into. These endpoints exist so
/// "which devices am I signed in on" and "what happened to my account" have answers.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/auth/sessions")]
[Produces("application/json")]
[Authorize]
public sealed class SessionsController : ApiController
{
    private readonly ISender _sender;

    public SessionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Lists the member's sessions, newest activity first.</summary>
    [HttpGet]
    [Authorize(Policy = DiasporaPolicies.ReadOwnSessions)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SessionResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessionsAsync(
        [FromQuery] bool includeRevoked,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionsQuery { IncludeRevoked = includeRevoked }, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Signs out one other device.</summary>
    [HttpDelete("{sessionId:guid}")]
    [Authorize(Policy = DiasporaPolicies.RevokeOwnSessions)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RevokeSessionCommand { SessionId = sessionId }, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Signs out everywhere except the device making the request.</summary>
    [HttpDelete]
    [Authorize(Policy = DiasporaPolicies.RevokeOwnSessions)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeAllOtherSessionsAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RevokeAllOtherSessionsCommand(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Returns the member's recent security history.</summary>
    [HttpGet("~/" + ApiEndpoints.BaseUrl + "/auth/security-events")]
    [Authorize(Policy = DiasporaPolicies.ReadOwnSecurityEvents)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SecurityEventResponse>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecurityEventsAsync(
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetSecurityEventsQuery { Limit = limit <= 0 ? 50 : limit }, cancellationToken);

        return HandleResult(result);
    }
}
