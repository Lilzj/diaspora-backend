using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// A shop in the Providers directory that stands behind this listing.
/// <para>
/// <b>An identifier and editorial text, and nothing B7 answers for.</b> The shop's name, badge,
/// rating and logo are resolved at read time, so a shop that renames itself or loses its
/// verification is never misrepresented by a listing written six months ago. Copying the name
/// onto this row is exactly the mistake ADR-005 and B7's directory contract both exist to prevent.
/// </para>
/// <para>
/// Distinct from the seller profile's own storefront link: that says "this seller <em>is</em> that
/// shop", and this says "this item is <em>from</em> that shop" — a repair service, a warranty
/// partner, a delivery agent the seller uses.
/// </para>
/// </summary>
public sealed class ListingProviderReference
{
    private ListingProviderReference(Guid providerId, string? role, string? note, int order)
    {
        ProviderId = providerId;
        Role = role;
        Note = note;
        Order = order;
    }

    private ListingProviderReference() { }

    /// <summary>B7's provider identifier.</summary>
    public Guid ProviderId { get; private set; }

    /// <summary>What the shop is to this listing — "supplier", "repairs", "delivery".</summary>
    public string? Role { get; private set; }

    public string? Note { get; private set; }

    public int Order { get; private set; }

    public static ListingProviderReference Create(
        Guid providerId, string? role, string? note, int order)
    {
        var trimmedRole = Clamp(role, MarketplaceLimits.MaxDisplayName);
        var trimmedNote = Clamp(note, MarketplaceLimits.MaxNoteLength);

        return new ListingProviderReference(providerId, trimmedRole, trimmedNote, order);
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }
}
