namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// The seller badge a buyer actually sees. The brief's five seller types, exactly.
/// <para>
/// <b>Computed, never stored, and never settable.</b> It is a projection of three facts the
/// module does not all own: the seller's declared <see cref="SellerKind"/>, the verification state
/// Trust decided, and whether a Providers storefront stands behind it. Persisting it would mean a
/// row that says "Verified Business" surviving Trust revoking the verification — the platform
/// vouching for exactly the wrong party, which is the single worst thing this module could get
/// wrong.
/// </para>
/// </summary>
public enum SellerType
{
    /// <summary>A person, unverified.</summary>
    Individual = 0,

    /// <summary>A person whose identity Trust has confirmed.</summary>
    VerifiedSeller = 1,

    /// <summary>A trader, unverified.</summary>
    Business = 2,

    /// <summary>A trader whose business Trust has confirmed.</summary>
    VerifiedBusiness = 3,

    /// <summary>Selling from a shop in the Providers directory. The strongest signal there is.</summary>
    ProviderStore = 4
}
