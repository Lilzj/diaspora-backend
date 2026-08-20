using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;
using Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;
using Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Moderation and administration of accounts.
/// <para>
/// Each action is gated on its own permission rather than on a blanket "admin" role, so
/// a moderator can suspend without also being able to close accounts.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/admin/accounts")]
[Produces("application/json")]
[Authorize]
public sealed class AccountAdministrationController : ApiController
{
    private readonly ISender _sender;

    public AccountAdministrationController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Suspends an account and revokes its live sessions.</summary>
    [HttpPost("{userId:guid}/suspend")]
    [Authorize(Policy = DiasporaPolicies.SuspendUsers)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendAsync(
        Guid userId,
        [FromBody] SuspendAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new SuspendAccountCommand
        {
            UserId = userId,
            Reason = request.Reason,
            SuspendedUntil = request.SuspendedUntil
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Lifts a suspension or hard lock, restoring the previous access rung.</summary>
    [HttpPost("{userId:guid}/reinstate")]
    [Authorize(Policy = DiasporaPolicies.ReinstateUsers)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReinstateAsync(
        Guid userId,
        [FromBody] ReinstateAccountRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReinstateAccountCommand
        {
            UserId = userId,
            Notes = request?.Notes
        }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Closes an account.</summary>
    [HttpPost("{userId:guid}/disable")]
    [Authorize(Policy = DiasporaPolicies.DisableUsers)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync(
        Guid userId,
        [FromBody] DisableAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new DisableAccountCommand
        {
            UserId = userId,
            Reason = request.Reason
        }, cancellationToken);

        return HandleResult(result);
    }
}
