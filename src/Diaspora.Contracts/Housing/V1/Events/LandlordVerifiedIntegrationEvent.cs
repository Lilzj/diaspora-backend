using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// Trust decided about a landlord, and Housing has applied it.
/// <para>
/// <b>This reports that Housing caught up with Trust, not that Housing decided anything.</b> It is
/// published from the outcome handler after the cached mark is written, so a consumer reading it
/// knows the badge Housing renders now matches the one Trust granted.
/// </para>
/// <para>
/// Revocations travel on this contract too, with <see cref="IsVerified"/> false. A consumer that
/// subscribed only to grants would keep showing a badge Trust had withdrawn - which is the platform
/// vouching for exactly the wrong party.
/// </para>
/// </summary>
public sealed record LandlordVerifiedIntegrationEvent : IntegrationEvent
{
    public required Guid LandlordId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required string LandlordName { get; init; }

    public required string LandlordKind { get; init; }

    /// <summary>Set when the decision was about one property rather than the landlord.</summary>
    public Guid? ListingId { get; init; }

    public required string PreviousState { get; init; }

    public required string CurrentState { get; init; }

    public required bool IsVerified { get; init; }

    public Guid? VerificationId { get; init; }

    /// <summary>The badge Trust granted, verbatim.</summary>
    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
