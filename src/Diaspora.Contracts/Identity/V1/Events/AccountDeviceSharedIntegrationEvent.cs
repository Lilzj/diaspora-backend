using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// Published when a member registers a device other accounts already use (D-13). Financial holds the
/// member's points for review on it, and Creator holds a referral the member was invited by.
/// <para>
/// <b>A count, never the other members.</b> Who else uses the device, and the device itself, are
/// Identity's to know. Behind <c>Identity:EnableSharedDeviceSignal</c>, off until the privacy notice
/// says the platform does this.
/// </para>
/// </summary>
public sealed record AccountDeviceSharedIntegrationEvent : IntegrationEvent
{
    /// <summary>The member who registered the device.</summary>
    public required Guid UserId { get; init; }

    /// <summary>How many other accounts had already registered it. At least one.</summary>
    public required int OtherAccounts { get; init; }

    public required DateTimeOffset DetectedAt { get; init; }
}
