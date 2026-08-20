using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A reservation ended without anybody moving in.
/// <para>
/// One contract for cancellation, refusal and expiry, because every consumer treats them
/// identically: the units are back and the property is available again. <see cref="Outcome"/>
/// preserves which it was for anybody who does care.
/// </para>
/// </summary>
public sealed record ReservationCancelledIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required Guid SeekerUserId { get; init; }

    public required int Units { get; init; }

    /// <summary>What is available again after the release.</summary>
    public required int RemainingUnits { get; init; }

    /// <summary>Cancelled, Declined or Expired.</summary>
    public required string Outcome { get; init; }

    /// <summary>Who ended it. Null on an expiry, which the sweep recorded.</summary>
    public Guid? CancelledBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }
}
