using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>
/// A provider started offering something new.
/// <para>
/// The event with the clearest member-facing value of the eight: somebody following a caterer
/// wants to know a new dish is on the menu. B7 publishes it and stops; B16 decides who hears
/// about it.
/// </para>
/// </summary>
public sealed record ProviderServiceAddedIntegrationEvent : IntegrationEvent
{
    public required Guid ProviderId { get; init; }

    public required Guid ServiceId { get; init; }

    public required string Name { get; init; }

    public Guid? CategoryId { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}
