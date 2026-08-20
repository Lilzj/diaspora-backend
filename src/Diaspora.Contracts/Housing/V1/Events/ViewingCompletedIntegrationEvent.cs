using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A property visit finished, one way or another.
/// <para>
/// <b><see cref="WasNoShow"/> is why cancellation and completion share one contract.</b> A pattern
/// of arranged viewings nobody attended is the earliest fraud signal this platform has: it is what
/// an advance-fee rental scam looks like from the outside, and a consumer building a risk model
/// needs the misses as much as the hits.
/// </para>
/// </summary>
public sealed record ViewingCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid ViewingId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required Guid SeekerUserId { get; init; }

    /// <summary>Completed, NoShow, Cancelled or Expired.</summary>
    public required string Outcome { get; init; }

    public required bool WasNoShow { get; init; }

    /// <summary>On a no-show, which party failed to appear. Null otherwise.</summary>
    public bool? LandlordFailedToAttend { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
