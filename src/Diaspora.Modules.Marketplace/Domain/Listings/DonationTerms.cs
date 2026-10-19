namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// A donated item: a free listing (<see cref="Enums.ListingType.Giveaway"/>) that names the cause it
/// goes to, carried as an attribute like a swap's terms (no schema change).
/// <para>
/// Added 16 Oct 2026. "Donation" in Buy &amp; Sell created nothing; the product decision is that a
/// donated item must be linked to a cause. The attribute's value is the cause's identifier and its
/// display name is the cause's title, written by the server from Financial's register, never taken
/// from the client.
/// </para>
/// </summary>
public static class DonationTerms
{
    public const string CauseKey = "donation-cause";

    /// <summary>The linked cause's identifier, or null when the listing is not a donation.</summary>
    public static Guid? CauseIdOf(IEnumerable<ListingAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var value = attributes.FirstOrDefault(attribute => attribute.Key == CauseKey)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>The linked cause's title as recorded when it was linked.</summary>
    public static string? CauseTitleOf(IEnumerable<ListingAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        return attributes.FirstOrDefault(attribute => attribute.Key == CauseKey)?.DisplayName;
    }
}
