namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A landlord as a card renders them.</summary>
public sealed record LandlordSummaryResponse
{
    public required Guid Id { get; init; }
    /// <summary>
    /// The member who owns this landlord profile, and therefore who a message about it reaches.
    /// <para>
    /// **A landlord profile is not a user.** Its identifier is a profile's, and the client was sending that
    /// as a conversation's recipient — so the thread was addressed to something that is not a
    /// person. Nothing could deliver to it, the other side could never open it, and it rendered
    /// with no name on it.
    /// </para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Individual, EstateAgent, PropertyManager, Hostel, Hotel and the rest.</summary>
    public required string Kind { get; init; }

    public required bool IsVerified { get; init; }

    /// <summary>Trust badge, verbatim. Null when unverified.</summary>
    public string? Badge { get; init; }

    public Guid? LogoMediaId { get; init; }

    /// <summary>The B7 provider behind an agency or hostel, where there is one.</summary>
    public Guid? ProviderId { get; init; }

    /// <summary>
    /// The star rating, or <b>null when nobody has reviewed this landlord</b>.
    /// <para>
    /// Null rather than a zeroed rollup, and the distinction is the point. The housing screen
    /// rendered <c>★ {listing.host.rating}</c> against a field no response has ever carried. A
    /// null that a client must handle is the only shape that cannot be quietly defaulted into a
    /// claim.
    /// </para>
    /// <para>
    /// Only a reservation that reached <c>Occupied</c> can produce a review, so this is a rating
    /// of tenancies rather than of opinions.
    /// </para>
    /// </summary>
    public LandlordRatingResponse? Rating { get; init; }
}
