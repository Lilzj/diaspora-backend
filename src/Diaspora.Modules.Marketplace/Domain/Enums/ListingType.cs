namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// What kind of transaction a listing is proposing.
/// <para>
/// <b><see cref="Auction"/> is declared and refused.</b> The brief asks for an auction-ready
/// abstraction and explicitly forbids auction mechanics, and the honest way to hold both is a
/// value the type system knows about which the aggregate rejects at creation. That gives a future
/// auction module a name that is already in the wire contract, in the database enum and in the
/// client's switch — and gives today's seller a clear "not open yet" instead of a listing that
/// quietly behaves like a fixed price.
/// </para>
/// <para>
/// <b>Featured is deliberately absent.</b> The brief lists "Featured Listing" beside the others,
/// but featuring is something done <em>to</em> a listing for a while and then undone — that is a
/// <c>ListingPromotion</c>, with a window and a grantor, not a kind of thing being sold. Making
/// it a type would mean a seller changing what their item <em>is</em> in order to buy placement.
/// </para>
/// </summary>
public enum ListingType
{
    /// <summary>A stated price, taken or left.</summary>
    FixedPrice = 0,

    /// <summary>A stated price that invites offers.</summary>
    Negotiable = 1,

    /// <summary>Reserved for a future auction module. Refused at creation today.</summary>
    Auction = 2,

    /// <summary>A buyer looking for something. Priced as a budget, if at all.</summary>
    Wanted = 3,

    /// <summary>Free to a good home. Priced at nothing, and validated as such.</summary>
    Giveaway = 4,

    /// <summary>Stock from a business or provider storefront, usually with a quantity.</summary>
    BusinessInventory = 5,

    /// <summary>
    /// An item offered in exchange for another rather than for money.
    /// <para>
    /// Added 15 Sep 2026. The app had a swap screen for months that sent a body the server could
    /// not bind and then told the member their swap was listed. What the seller wants back and any
    /// cash top-up are carried as two well-known attributes — see <c>SwapTerms</c> — so the type
    /// needed no new columns, and the stored value is the enum's name, so it needed no migration.
    /// </para>
    /// </summary>
    Swap = 6
}
