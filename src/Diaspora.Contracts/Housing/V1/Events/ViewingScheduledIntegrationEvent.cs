using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A landlord agreed a time to show a property.
/// <para>
/// <b>This is the event a future Communication module will notify on</b>, and it is also the moment
/// the member becomes entitled to the full address. It carries no address itself for exactly that
/// reason: the disclosure decision belongs to a request with a viewer identity attached, not to a
/// payload on a bus.
/// </para>
/// </summary>
public sealed record ViewingScheduledIntegrationEvent : IntegrationEvent
{
    public required Guid ViewingId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required Guid SeekerUserId { get; init; }

    public required DateTimeOffset ScheduledFor { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>A video walk-through rather than a visit.</summary>
    public required bool IsVirtual { get; init; }

    /// <summary>Hours from the request to this answer. The landlord responsiveness measure.</summary>
    public double? ResponseHours { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }
}
