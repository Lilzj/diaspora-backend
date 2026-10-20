namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// The record an invoice was issued from.
/// <para>
/// Every invoice is issued for exactly one captured payment. This says which record explains what
/// that payment bought, so the lines can be drawn from it rather than from the payment's free-text
/// description.
/// </para>
/// </summary>
public enum InvoiceSourceKind
{
    /// <summary>A catalogue product: a placement, a boost, a credit bundle, a renewal.</summary>
    Purchase = 1,

    /// <summary>A subscription charge: an activation, or the difference charged on an upgrade.</summary>
    Subscription = 2,

    /// <summary>
    /// Any other payment the platform took: a membership fee, a direct payment intent. Described by
    /// the payment's own description and nothing else.
    /// </summary>
    Payment = 3
}
