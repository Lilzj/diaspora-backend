using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Administration.AssignRole;
using Diaspora.Application.Identity.Commands.Administration.RepublishPhoneVerification;
using Diaspora.Application.Identity.Commands.Administration.RevokeMemberSessions;
using Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;
using Diaspora.Application.Identity.Commands.Lifecycle.ReinstateAccount;
using Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;
using Diaspora.Application.Identity.Queries.Administration.FindMember;
using Diaspora.Application.Identity.Queries.Administration.GetMemberHistory;
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

    /// <summary>
    /// Finds a member by identifier, email address or phone number.
    /// </summary>
    /// <remarks>
    /// A lookup rather than a search: staff come here holding a handle somebody gave them.
    /// Listing the membership is a different capability with different consequences, and it
    /// should be asked for on purpose rather than arrive as a side effect of this one.
    /// </remarks>
    [HttpGet("lookup")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AdminMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupAsync(
        [FromQuery] string term,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new FindMemberQuery { Term = term }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>One member, as staff are entitled to see them.</summary>
    [HttpGet("{userId:guid}")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AdminMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new FindMemberQuery { Term = userId.ToString() }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>A member's security history: sign-ins, failures, changes to their account.</summary>
    [HttpGet("{userId:guid}/security-events")]
    [Authorize(Policy = DiasporaPolicies.ReadAnySecurityEvents)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SecurityEventResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoryAsync(
        Guid userId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMemberHistoryQuery { UserId = userId, Limit = limit ?? 50 }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Signs a member out of every device, without suspending them.
    /// </summary>
    /// <remarks>
    /// The move for an account that may be in somebody else's hands: the member keeps their
    /// account and can sign back in, and whoever is holding a live session stops being able
    /// to use it.
    /// </remarks>
    [HttpPost("{userId:guid}/sessions/revoke")]
    [Authorize(Policy = DiasporaPolicies.RevokeAnySession)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSessionsAsync(
        Guid userId,
        [FromBody] RevokeMemberSessionsRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RevokeMemberSessionsCommand { UserId = userId, Reason = request?.Reason },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Gives a member a platform role, or takes one away.
    /// </summary>
    /// <remarks>
    /// Their live sessions are revoked with the change, because permissions travel inside
    /// the access token: without it, somebody demoted keeps every permission they held until
    /// that token expires, and somebody promoted gains nothing until theirs does.
    /// </remarks>
    [HttpPut("{userId:guid}/role")]
    [Authorize(Policy = DiasporaPolicies.AssignRoles)]
    [ProducesResponseType<ApiResponse<AdminMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRoleAsync(
        Guid userId,
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(
            new AssignRoleCommand
            {
                UserId = userId,
                Role = request.Role,
                Reason = request.Reason
            },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Re-announces a member's verified phone to the rest of the platform.</summary>
    /// <remarks>
    /// For accounts verified before anything published the fact. Trust keeps its own record of
    /// a member's verifications and was never told about a phone, so its checklist showed
    /// "Phone Linked — Verify" to every member. New verifications announce themselves now;
    /// older ones cannot, because confirming an already-verified phone is a no-op.
    /// <para>
    /// Changes nothing here: no state moves and nothing is re-verified. A member whose phone is
    /// not verified is refused rather than announced.
    /// </para>
    /// </remarks>
    [HttpPost("{userId:guid}/republish-phone-verification")]
    [Authorize(Policy = DiasporaPolicies.RepublishMemberFacts)]
    [ProducesResponseType<ApiResponse<OperationAcknowledgedResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RepublishPhoneVerificationAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RepublishPhoneVerificationCommand { UserId = userId }, cancellationToken);

        return HandleResult(result);
    }
}
