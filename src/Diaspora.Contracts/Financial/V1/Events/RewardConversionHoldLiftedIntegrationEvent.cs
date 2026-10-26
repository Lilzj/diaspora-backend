using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Published when staff lift the hold on a member's points (CS-D4). Lifting is the review a system
/// hold waits for (D-13), so Creator clears its shared-device flag on it.
/// <para>No reason travels: a hold's note is an operator's, and never leaves the Financial Platform.</para>
/// </summary>
public sealed record RewardConversionHoldLiftedIntegrationEvent : IntegrationEvent
{
    public required Guid MemberUserId { get; init; }

    /// <summary>The staff member who lifted it.</summary>
    public Guid? LiftedByUserId { get; init; }

    public required DateTimeOffset LiftedAt { get; init; }
}
