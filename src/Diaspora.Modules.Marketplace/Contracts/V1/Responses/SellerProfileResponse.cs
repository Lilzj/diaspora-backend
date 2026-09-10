namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A seller's storefront, in full.
/// <para>
/// The contact details are present only when the seller published them and only when their contact
/// preference invites the channel. A seller who chose in-app messaging is not handing their phone
/// number to every visitor.
/// </para>
/// </summary>
public sealed record SellerProfileResponse
{
    public required Guid SellerId { get; init; }

    /// <summary>
    /// The member who owns this, and therefore who a message about it reaches.
    /// <para>
    /// It is a profile, not a user. The client was sending the profile's identifier as a
    /// conversation's recipient — so the thread was addressed to something that is not a person,
    /// nothing could deliver to it, and it rendered with no name on it.
    /// </para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    /// <summary><c>Individual</c> or <c>Business</c>. What the seller declared.</summary>
    public required string Kind { get; init; }

    /// <summary>The computed badge — the brief's five seller types.</summary>
    public required string SellerType { get; init; }

    public required bool IsVerified { get; init; }

    public string? Badge { get; init; }

    /// <summary><c>Active</c>, <c>Suspended</c> or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    public string? Bio { get; init; }

    public required string CountryCode { get; init; }

    public Guid? LogoMediaId { get; init; }

    public Guid? BannerMediaId { get; init; }

    /// <summary>The shop in the Providers directory this storefront is, when it is one.</summary>
    public Guid? ProviderId { get; init; }

    /// <summary>The shop's current name, from B7. Null when the directory could not answer.</summary>
    public string? ProviderName { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public string? Website { get; init; }

    public required string ContactPreference { get; init; }

    public required SellerStatisticsResponse Statistics { get; init; }

    public required DateTimeOffset MemberSince { get; init; }

    /// <summary>The optimistic concurrency token. Sent back on the next edit.</summary>
    public required int Revision { get; init; }

    /// <summary>Whether the caller may edit it. Null for an anonymous reader.</summary>
    public bool? CanManage { get; init; }

    /// <summary>Why the seller was suspended. Their own view and staff's only.</summary>
    public string? SuspensionReason { get; init; }
}
