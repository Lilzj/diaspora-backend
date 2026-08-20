using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Administration;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// The operator's console for reliable messaging.
/// <para>
/// <b>An outbox without this is a black box with a growing table.</b> Every endpoint here
/// exists because the alternative is a psql session against a production database, written from
/// memory, at three in the morning, by somebody who has been awake for nineteen hours — which
/// is how a replay becomes a deletion.
/// </para>
/// <para>
/// Reading is separated from acting by permission, because they are different risks. Listing
/// dead letters is something any on-call engineer should be able to do. Replaying them
/// recreates the load that produced them against a dependency that has only just recovered, and
/// skipping one stops a committed business event from ever reaching its consumers.
/// </para>
/// <para>
/// No endpoint returns a payload. The messages here carry members' posts, verifications and
/// business details, and an administration listing is the wrong place for that content to be
/// read casually or pasted into an incident channel. Everything needed to decide what to do
/// with a message is returned; the content of the message is not.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/platform/outbox")]
[Produces("application/json")]
[Authorize(Policy = OutboxPolicies.Read)]
public sealed class OutboxAdministrationController : ApiController
{
    private readonly IOutboxAdministration _outbox;
    private readonly IClock _clock;

    public OutboxAdministrationController(IOutboxAdministration outbox, IClock clock)
    {
        _outbox = outbox;
        _clock = clock;
    }

    /// <summary>Depth, ages, and the per-module and per-contract breakdown.</summary>
    [HttpGet("statistics")]
    [ProducesResponseType<ApiResponse<OutboxStatisticsResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var statistics = await _outbox.GetStatisticsAsync(cancellationToken);

        return Ok(new ApiResponse<OutboxStatisticsResponse>(
            statistics.ToResponse(), ApiMeta.For(HttpContext)));
    }

    /// <summary>
    /// Undelivered messages, oldest first.
    /// <para>
    /// <c>minAttempts=1</c> is the filter worth knowing: it shows only what has already failed,
    /// which during an incident is the difference between a page of noise and the list of
    /// things actually going wrong.
    /// </para>
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OutboxMessageResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingAsync(
        [FromQuery] string? module,
        [FromQuery] string? eventType,
        [FromQuery] string? correlationId,
        [FromQuery] int? minAttempts,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var messages = await _outbox.ListPendingAsync(
            new OutboxQuery(module, eventType, null, correlationId, minAttempts, limit, offset),
            cancellationToken);

        var now = _clock.UtcNow;

        return Ok(new ApiResponse<IReadOnlyList<OutboxMessageResponse>>(
            messages.Select(message => message.ToResponse(now)).ToArray(),
            ApiMeta.For(HttpContext)));
    }

    /// <summary>Dead letters, most recent first.</summary>
    [HttpGet("dead-letters")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OutboxDeadLetterResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDeadLettersAsync(
        [FromQuery] string? module,
        [FromQuery] string? eventType,
        [FromQuery] string? correlationId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var deadLetters = await _outbox.ListDeadLettersAsync(
            new OutboxQuery(module, eventType, null, correlationId, null, limit, offset),
            cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<OutboxDeadLetterResponse>>(
            deadLetters.Select(deadLetter => deadLetter.ToResponse()).ToArray(),
            ApiMeta.For(HttpContext)));
    }

    /// <summary>Every recorded attempt at one message, oldest first.</summary>
    [HttpGet("messages/{messageId:guid}/attempts")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OutboxDeliveryAttemptResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAttemptsAsync(
        Guid messageId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var attempts = await _outbox.ListAttemptsAsync(messageId, limit, cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<OutboxDeliveryAttemptResponse>>(
            attempts.Select(attempt => attempt.ToResponse()).ToArray(),
            ApiMeta.For(HttpContext)));
    }

    /// <summary>Puts one dead letter back on the queue under its original message identifier.</summary>
    [HttpPost("dead-letters/{deadLetterId:long}/replay")]
    [Authorize(Policy = OutboxPolicies.Manage)]
    [ProducesResponseType<ApiResponse<OutboxOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReplayDeadLetterAsync(
        long deadLetterId, CancellationToken cancellationToken)
    {
        var result = await _outbox.ReplayDeadLetterAsync(deadLetterId, cancellationToken);

        return result.Affected == 0
            ? NotFoundProblem(result.Description)
            : Ok(new ApiResponse<OutboxOperationResponse>(result.ToResponse(), ApiMeta.For(HttpContext)));
    }

    /// <summary>
    /// Replays every dead letter matching the filter, up to the batch limit.
    /// <para>
    /// The limit is capped by the service and the response says when the cap was hit, because a
    /// bulk replay recreates the original load: thirty thousand messages released at once
    /// against a dependency that came back four minutes ago will take it down again.
    /// </para>
    /// </summary>
    [HttpPost("dead-letters/replay")]
    [Authorize(Policy = OutboxPolicies.Manage)]
    [ProducesResponseType<ApiResponse<OutboxOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReplayDeadLettersAsync(
        [FromQuery] string? module,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _outbox.ReplayDeadLettersAsync(
            new OutboxQuery(module, eventType, Limit: limit), cancellationToken);

        return Ok(new ApiResponse<OutboxOperationResponse>(
            result.ToResponse(), ApiMeta.For(HttpContext)));
    }

    /// <summary>Clears a pending message's remaining backoff so the next pass claims it.</summary>
    [HttpPost("pending/{sequence:long}/retry")]
    [Authorize(Policy = OutboxPolicies.Manage)]
    [ProducesResponseType<ApiResponse<OutboxOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryNowAsync(long sequence, CancellationToken cancellationToken)
    {
        var result = await _outbox.RetryNowAsync(sequence, cancellationToken);

        return result.Affected == 0
            ? NotFoundProblem(result.Description)
            : Ok(new ApiResponse<OutboxOperationResponse>(result.ToResponse(), ApiMeta.For(HttpContext)));
    }

    /// <summary>
    /// Gives up on a pending message, moving it to the dead-letter table with the operator's
    /// reason.
    /// <para>
    /// The escape hatch for a poison message blocking its partition. It never deletes: the
    /// payload survives, so a decision made in a hurry can be undone with a replay.
    /// </para>
    /// </summary>
    [HttpPost("pending/{sequence:long}/skip")]
    [Authorize(Policy = OutboxPolicies.Manage)]
    [ProducesResponseType<ApiResponse<OutboxOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SkipAsync(
        long sequence,
        [FromBody] OutboxSkipRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _outbox.SkipAsync(sequence, request.Reason, cancellationToken);

        return result.Affected == 0
            ? NotFoundProblem(result.Description)
            : Ok(new ApiResponse<OutboxOperationResponse>(result.ToResponse(), ApiMeta.For(HttpContext)));
    }

    /// <summary>
    /// Removes delivered messages past their retention window, on demand.
    /// <para>
    /// The retention sweep does this on a timer. The endpoint exists for the case the sweep is
    /// meant to prevent and occasionally does not: a table that has grown while retention was
    /// misconfigured, and somebody who needs it smaller before the next claim query times out.
    /// </para>
    /// </summary>
    [HttpPost("purge-delivered")]
    [Authorize(Policy = OutboxPolicies.Manage)]
    [ProducesResponseType<ApiResponse<OutboxOperationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PurgeDeliveredAsync(CancellationToken cancellationToken)
    {
        var result = await _outbox.PurgeDeliveredAsync(cancellationToken);

        return Ok(new ApiResponse<OutboxOperationResponse>(
            result.ToResponse(), ApiMeta.For(HttpContext)));
    }

    private ObjectResult NotFoundProblem(string detail) =>
        StatusCode(
            StatusCodes.Status404NotFound,
            PlatformProblem.Create(
                HttpContext,
                StatusCodes.Status404NotFound,
                PlatformErrorCodes.NotFound,
                "Not found",
                detail));
}
