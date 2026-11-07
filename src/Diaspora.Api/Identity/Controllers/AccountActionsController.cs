using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Administration.AccountActions;
using Diaspora.Application.Identity.Queries.Administration.AccountActions;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Account actions a second member of staff decides (7 Nov 2026).
/// <para>
/// Suspending, closing and reopening an account, and granting Admin or SuperAdmin, are proposed at
/// <c>/admin/accounts/{id}/…</c> and land here. Whoever confirms or declines must not be the
/// proposer, must hold the permission the action itself needs, and must be above the account in
/// rank: 403 otherwise. Confirming carries the action out. The proposer may withdraw.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/admin/account-actions")]
[Produces("application/json")]
[Authorize]
public sealed class AccountActionsController : ApiController
{
    private readonly ISender _sender;

    public AccountActionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Proposed account actions, newest first.</summary>
    /// <param name="status"><c>Pending</c> for the queue; <c>Confirmed</c>, <c>Declined</c>, <c>Withdrawn</c>; all when absent.</param>
    /// <param name="userId">Only proposals about this account.</param>
    /// <param name="limit">At most 200; 50 when absent.</param>
    /// <param name="cancellationToken">The request's cancellation.</param>
    [HttpGet]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AccountActionProposalResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status,
        [FromQuery] Guid? userId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ListAccountActionsQuery { Status = status, TargetUserId = userId, Limit = limit ?? 50 },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>One proposed account action.</summary>
    [HttpGet("{proposalId:guid}")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AccountActionProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid proposalId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAccountActionQuery { ProposalId = proposalId }, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>A second member of staff agrees, and the action is carried out.</summary>
    /// <remarks>
    /// 200 with the confirmed proposal. 403 for the proposer, for a member of staff without the
    /// action's permission or not above the account in rank, and for anyone but a SuperAdmin on a
    /// SuperAdmin grant; 409 when it is no longer pending or the account refuses the action.
    /// </remarks>
    [HttpPost("{proposalId:guid}/confirm")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AccountActionProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAsync(
        Guid proposalId,
        [FromBody] DecideAccountActionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ConfirmAccountActionCommand { ProposalId = proposalId, Note = request?.Note },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>A second member of staff disagrees; nothing is done to the account.</summary>
    /// <remarks>200 with the declined proposal. <c>note</c> is required. The same 403s as confirming.</remarks>
    [HttpPost("{proposalId:guid}/decline")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AccountActionProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeclineAsync(
        Guid proposalId,
        [FromBody] DecideAccountActionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new DeclineAccountActionCommand { ProposalId = proposalId, Note = request?.Note ?? string.Empty },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// A member of staff takes up a suspension the platform raised after a third strike, as their
    /// own proposal, so a second member of staff can confirm it.
    /// </summary>
    /// <remarks>
    /// 200 with the proposal, now in the caller's name. <c>note</c> is required and replaces the
    /// platform's: it is what the member reads. 403 without the suspend permission or a higher rank;
    /// 409 when somebody has already taken it up or it is no longer pending.
    /// </remarks>
    [HttpPost("{proposalId:guid}/adopt")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AccountActionProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdoptAsync(
        Guid proposalId,
        [FromBody] DecideAccountActionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AdoptAccountActionCommand { ProposalId = proposalId, Note = request?.Note ?? string.Empty },
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>The proposer takes it back before anybody has decided.</summary>
    /// <remarks>200 with the withdrawn proposal; 403 for anyone but the proposer.</remarks>
    [HttpPost("{proposalId:guid}/withdraw")]
    [Authorize(Policy = DiasporaPolicies.ReadAnyUser)]
    [ProducesResponseType<ApiResponse<AccountActionProposalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawAsync(
        Guid proposalId,
        [FromBody] DecideAccountActionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new WithdrawAccountActionCommand { ProposalId = proposalId, Note = request?.Note },
            cancellationToken);

        return HandleResult(result);
    }
}
