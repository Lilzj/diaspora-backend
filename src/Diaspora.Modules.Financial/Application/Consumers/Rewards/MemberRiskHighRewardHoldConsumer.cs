using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Financial.Application.Rewards;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Holds a member's points for review when Trust says their risk has become High (RWD-14, CS-4b).
/// <para>
/// <b>Signals flag, staff decide (D-13).</b> The hold is the platform's, decided by nobody, and goes
/// to the same review queue as the shared-device hold. A level that falls again lifts nothing: the
/// evidence ageing out is not a review. Staff lift it from the queue, or an approved appeal does
/// (CS-4a).
/// </para>
/// <para>
/// Trust announces the level only. The score and the factors behind it stay in Trust, staff-only, so
/// the note names no check.
/// </para>
/// </summary>
public sealed class MemberRiskHighRewardHoldConsumer
    : IntegrationEventHandler<MemberRiskLevelChangedIntegrationEvent>
{
    public const string HighLevel = "High";

    public const string Reason = "Trust risk is high; awaiting review";

    private readonly SystemRewardHolds _holds;

    public MemberRiskHighRewardHoldConsumer(SystemRewardHolds holds) => _holds = holds;

    protected override Task HandleAsync(
        MemberRiskLevelChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return string.Equals(integrationEvent.CurrentLevel, HighLevel, StringComparison.Ordinal)
            ? _holds.PlaceAsync(integrationEvent.UserId, Reason, cancellationToken)
            : Task.CompletedTask;
    }
}
