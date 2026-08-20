namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Which side of a negotiation acted.
/// <para>
/// Recorded on every history entry rather than inferred from the user identifier, because a
/// seller profile can outlive the member who opened it and a negotiation read back in two years
/// must still say who was buying.
/// </para>
/// </summary>
public enum OfferParty
{
    Buyer = 0,

    Seller = 1
}
