namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>Who adds money when the two things being swapped are not worth the same.</summary>
public enum SwapTopUpDirection
{
    /// <summary>An even swap. No money changes hands.</summary>
    None = 0,

    /// <summary>The member who listed the item adds the amount.</summary>
    SellerAdds = 1,

    /// <summary>The member taking the swap adds the amount.</summary>
    BuyerAdds = 2
}

/// <summary>
/// What a swap asks for in return, read from the listing's attributes.
/// <para>
/// <b>Two well-known attribute keys rather than new columns.</b> Attributes are already the
/// module's extensibility point — they are validated, stored, indexed and sent to the search
/// engine — and a swap's terms are exactly the kind of structured fact they exist for. The keys are
/// fixed here so the aggregate's publication bar, the mapper and the client all name the same two.
/// </para>
/// <para>
/// <b>The top-up amount is the listing's price.</b> A swap with no top-up has no price; a swap
/// where somebody adds money states how much, in the currency the price already carries. That keeps
/// price filters honest: an even swap never appears under "under 50" as though it cost nothing.
/// </para>
/// </summary>
public static class SwapTerms
{
    /// <summary>What the seller would take in exchange, in their own words.</summary>
    public const string WantsKey = "swap-wants";

    /// <summary><c>none</c>, <c>seller-adds</c> or <c>buyer-adds</c>.</summary>
    public const string TopUpKey = "swap-top-up";

    public const string WantsLabel = "Wants in exchange";

    public const string TopUpLabel = "Cash top-up";

    /// <summary>The wanted item, or null when the listing has not said.</summary>
    public static string? WantsOf(IEnumerable<ListingAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var wants = attributes
            .FirstOrDefault(attribute => attribute.Key == WantsKey)?
            .Value;

        return string.IsNullOrWhiteSpace(wants) ? null : wants.Trim();
    }

    /// <summary>
    /// The top-up direction. Anything unrecognised reads as an even swap, because a stray value
    /// must not invent an obligation to pay.
    /// </summary>
    public static SwapTopUpDirection TopUpOf(IEnumerable<ListingAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var value = attributes
            .FirstOrDefault(attribute => attribute.Key == TopUpKey)?
            .Value;

        return ParseTopUp(value);
    }

    /// <summary>The attribute value for a direction, as the client sends it.</summary>
    public static string ToAttributeValue(SwapTopUpDirection direction) => direction switch
    {
        SwapTopUpDirection.SellerAdds => "seller-adds",
        SwapTopUpDirection.BuyerAdds => "buyer-adds",
        _ => "none",
    };

    /// <summary>Reads <c>seller-adds</c> / <c>SellerAdds</c> and the like; anything else is none.</summary>
    public static SwapTopUpDirection ParseTopUp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SwapTopUpDirection.None;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);

        return Enum.TryParse<SwapTopUpDirection>(normalized, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : SwapTopUpDirection.None;
    }
}
