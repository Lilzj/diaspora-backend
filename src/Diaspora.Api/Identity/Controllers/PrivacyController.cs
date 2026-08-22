using Diaspora.Api.Common;
using Diaspora.Api.RateLimiting;
using Diaspora.Application.Identity.Commands.Privacy;
using Diaspora.Platform.Http;
using Diaspora.Platform.Operations.Privacy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// The two subject rights a member can exercise on themselves: take their data out, and leave.
/// <para>
/// <b>Why these are here and not on a module.</b> Neither belongs to any one module — both fan out
/// across all of them through <see cref="IDataExportProvider"/> and
/// <see cref="IDataErasureProvider"/> — and both are about the account rather than about anything the
/// account did. That puts them alongside <c>AuthController</c>, which owns the same subject.
/// </para>
/// <para>
/// <b>The staff-facing half already existed.</b> <c>GET /platform/operations/privacy-audit</c> reports
/// which modules can export and erase and which cannot; this is the member-facing side of the same
/// seam. The audit is what tells an operator whether the answers below are complete, and the
/// responses carry the same information so the member is told too.
/// </para>
/// </summary>
[ApiController]
[Route(ApiEndpoints.BaseUrl + "/privacy")]
[Authorize]
[EnableRateLimiting(RateLimitingRegistration.AuthenticatedPolicy)]
public sealed class PrivacyController : ApiController
{
    private readonly ISender _sender;

    public PrivacyController(ISender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        _sender = sender;
    }

    /// <summary>
    /// Everything this platform holds that the member gave it.
    /// <para>
    /// Returned inline rather than as a generated file for download. The client renders it, saves it
    /// or shares it — which keeps the platform out of the business of hosting a document containing a
    /// member's private messages at a URL, however short-lived that URL would be.
    /// </para>
    /// <para>
    /// The response names the modules that could not contribute, so a member reading it can tell the
    /// difference between "Diaspora holds nothing else about me" and "this export is incomplete".
    /// </para>
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType<ApiResponse<PersonalDataExport>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportAsync(CancellationToken cancellationToken) =>
        HandleResult(await _sender.Send(new ExportMyDataQuery(), cancellationToken));

    /// <summary>
    /// What erasing this account would do, module by module. Changes nothing.
    /// <para>
    /// Offered before the irreversible call because a member is entitled to know what "delete my
    /// account" means here before choosing it — including that some things are kept, and why.
    /// </para>
    /// </summary>
    [HttpGet("deletion/preview")]
    [ProducesResponseType<ApiResponse<AccountErasureReceipt>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PreviewDeletionAsync(CancellationToken cancellationToken) =>
        HandleResult(await _sender.Send(new PreviewMyAccountDeletionQuery(), cancellationToken));

    /// <summary>
    /// Erases the account. Irreversible.
    /// <para>
    /// Requires the member's current password and the literal confirmation word. A bearer token on an
    /// unlocked phone is enough to reach this endpoint; it is not enough to use it.
    /// </para>
    /// <para>
    /// The response is the receipt — what each module did, what was kept and why, and which modules
    /// could not be asked. It is the last thing this account will ever be shown, so it says everything
    /// rather than returning a bare 204.
    /// </para>
    /// </summary>
    [HttpPost("deletion")]
    [ProducesResponseType<ApiResponse<AccountErasureReceipt>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAsync(
        [FromBody] DeleteMyAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return HandleResult(await _sender.Send(
            new DeleteMyAccountCommand(request.CurrentPassword, request.Confirmation),
            cancellationToken));
    }
}

/// <summary>The body of an account erasure request.</summary>
/// <param name="CurrentPassword">The member's current password, re-entered.</param>
/// <param name="Confirmation">The literal word <c>DELETE</c>.</param>
public sealed record DeleteMyAccountRequest(string CurrentPassword, string Confirmation);
