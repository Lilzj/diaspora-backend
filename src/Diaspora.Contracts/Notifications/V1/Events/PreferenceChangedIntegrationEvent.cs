using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Notifications.V1.Events;

/// <summary>
/// A member changed how they want to be contacted.
/// <para>
/// <b>It carries what changed, never the new settings.</b> A member's quiet hours are a statement
/// about when they sleep and their muted categories are a statement about what they are avoiding;
/// broadcasting either would turn a preference screen into a profile. Consumers that need the
/// settings ask this module for them with the member's own authority.
/// </para>
/// </summary>
public sealed record PreferenceChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>
    /// Which part moved: <c>Channels</c>, <c>Categories</c>, <c>QuietHours</c>, <c>Digest</c>,
    /// <c>Language</c>, <c>TimeZone</c>, <c>DoNotDisturb</c> or <c>Marketing</c>.
    /// </summary>
    public required IReadOnlyList<string> ChangedSections { get; init; }

    /// <summary>True when the member switched every channel off. The signal worth watching.</summary>
    public required bool IsFullyOptedOut { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
