using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A grantor withdrew points it granted, because what earned them is gone (D-9, RWD-08).
/// <para>
/// <b>A request, not an instruction.</b> The Financial Platform takes back points that are still
/// pending and leaves vested points where they are: points that could have been spent stay earned,
/// and money is never taken back automatically. The grantor cannot know which applies, so it
/// publishes the fact and the Financial Platform decides, the same separation as the grant itself.
/// </para>
/// <para>
/// Idempotent on the grant: a second publish for one grant changes nothing.
/// </para>
/// </summary>
public sealed record RewardPointsReversedIntegrationEvent : IntegrationEvent
{
    public required Guid MemberUserId { get; init; }

    /// <summary>The <see cref="RewardPointsGrantedIntegrationEvent.GrantReference"/> being withdrawn.</summary>
    public required string GrantReference { get; init; }

    /// <summary>Why: <c>Guide deleted.</c>, <c>Comment removed.</c>. Recorded on the grant.</summary>
    public required string Reason { get; init; }
}
