using Diaspora.Application.Identity.Commands.Verification.ApplyTrustDecision;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Api.Identity.Trust;

/// <summary>
/// Applies a trust decision in this process, for a deployment that has no external Trust engine.
/// <para>
/// <b>Why this lives in the API project and not in Trust.</b> ADR-002 keeps Trust free of every
/// Identity type, and that is worth keeping: it is what makes extracting B3 into its own service
/// a configuration change. The composition root is the one place already entitled to know both
/// modules, so the bridge sits here and Trust still sees nothing but
/// <see cref="ITrustDecisionPublisher"/>. Switching back to HTTP is
/// <c>Trust:Decisions:Mode = "Http"</c>.
/// </para>
/// <para>
/// <b>Why it invokes the handler rather than sending the command.</b>
/// <c>ApplyTrustDecisionCommand</c> is gated on <c>identity:trust:apply-decision</c>, a permission
/// granted to no human role in any access state — the point of which is that no signed-in member
/// can move their own access state. Sending it through the pipeline here would be refused,
/// because the ambient caller is the member who submitted the verification. Calling the handler
/// directly is the honest expression of what is happening: the platform is deciding, not the
/// member, and the request never crossed a network boundary to be authorised at.
/// </para>
/// <para>
/// Nothing that matters is skipped. The handler owns the transition, the security event and the
/// save; the behaviours it bypasses are validation of a command this class constructs itself,
/// and an audit entry the security event already records.
/// </para>
/// </summary>
public sealed class LoopbackTrustDecisionPublisher : ITrustDecisionPublisher
{
    private readonly IRequestHandler<ApplyTrustDecisionCommand, Result<AccessStateResponse>> _handler;
    private readonly ILogger<LoopbackTrustDecisionPublisher> _logger;

    public LoopbackTrustDecisionPublisher(
        IRequestHandler<ApplyTrustDecisionCommand, Result<AccessStateResponse>> handler,
        ILogger<LoopbackTrustDecisionPublisher> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task<bool> PublishAsync(
        UserId userId,
        TrustDecisionOutcome outcome,
        string? reason,
        string? decidedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var command = new ApplyTrustDecisionCommand
        {
            UserId = userId.Value,
            Outcome = outcome.ToString(),
            Reason = reason,
            DecidedBy = decidedBy
        };

        try
        {
            var result = await _handler.Handle(command, cancellationToken);

            if (result.IsFailure)
            {
                // A refused transition is the common case and is not an error: a decision that
                // does not fit the member's current rung means the ladder and the queue have
                // drifted apart, and that is worth seeing rather than swallowing.
                _logger.LogWarning(
                    "Trust decision '{Outcome}' for user {UserId} was not applied: {Code} {Description}",
                    outcome, userId.Value, result.Error.Code, result.Error.Description);

                return false;
            }

            _logger.LogInformation(
                "Applied trust decision '{Outcome}' for user {UserId}; access state is now {State}.",
                outcome, userId.Value, result.Value.State);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Same contract as the HTTP publisher: the caller has already committed its own
            // decision and must not be rolled back because this leg failed.
            _logger.LogError(
                exception,
                "Trust decision '{Outcome}' for user {UserId} could not be applied.",
                outcome, userId.Value);

            return false;
        }
    }
}
