namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// What a seller declares itself to be.
/// <para>
/// <b>Two values, not five, and that is the whole design.</b> The brief names five seller types —
/// Individual, Verified Seller, Business, Verified Business, Provider-owned Store — but three of
/// those are compounds of something the seller declares and something only Trust or Providers can
/// confirm. A seller cannot set itself Verified, so "Verified Business" must never be a column a
/// seller writes. This enum holds the declaration; <see cref="SellerType"/> holds the composite
/// the client renders, and it is computed.
/// </para>
/// </summary>
public enum SellerKind
{
    /// <summary>A person selling their own things.</summary>
    Individual = 0,

    /// <summary>A registered trader, shop or organisation.</summary>
    Business = 1
}
