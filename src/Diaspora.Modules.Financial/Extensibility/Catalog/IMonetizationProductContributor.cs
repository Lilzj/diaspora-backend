namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>
/// A module declaring what can be bought for the things it owns.
/// <para>
/// <b>This is how a vertical monetises without owning any financial state.</b> Jobs contributes a
/// "boost a job posting" product; Housing contributes a highlight; a module arriving in B16
/// contributes whatever it sells. None of them holds a price, a placement, a payment or a ledger
/// entry — they name a product, and the Financial Platform owns everything that happens when
/// somebody buys one.
/// </para>
/// <para>
/// Seeding is idempotent: an existing code is left alone, because a deployment that has repriced a
/// product must not have it reset on the next restart.
/// </para>
/// </summary>
public interface IMonetizationProductContributor
{
    string Module { get; }

    IEnumerable<MonetizationProductSeed> GetProducts();
}
