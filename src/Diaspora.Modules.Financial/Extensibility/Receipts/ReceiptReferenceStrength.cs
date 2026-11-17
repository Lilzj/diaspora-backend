namespace Diaspora.Modules.Financial.Extensibility.Receipts;

/// <summary>
/// What a receipt reference has to withstand — BBL-004.
/// <para>
/// <b>A receipt reference does two jobs, and until BBL-004 one string tried to do both.</b> It is
/// the handle somebody reads to a support agent over a telephone, and for a gift with no owner it is
/// also the only credential that reaches the receipt. Those two requirements pull in opposite
/// directions: legibility wants it short, unguessability wants it long.
/// </para>
/// <para>
/// <b>They never apply to the same receipt.</b> A receipt that gets read aloud belongs to somebody
/// support can identify, and <c>GET /financial/receipts/{reference}</c> serves that one to its owner
/// and to nobody else — so its reference guards nothing and may be as short as a human can manage. A
/// receipt with no owner cannot be scoped to anybody, there is no account for support to tie it to,
/// and the reference is the entire access-control story; so it is minted long and is never read over
/// a telephone, because the only thing holding it is the client that made the gift.
/// </para>
/// <para>
/// Stated as a requirement at the call site rather than inferred inside the generator, because the
/// thing that knows whether a gift stored a giver is the handler creating it.
/// </para>
/// </summary>
public enum ReceiptReferenceStrength
{
    /// <summary>
    /// A reference somebody can read aloud. Access is decided by ownership, so this string is an
    /// identifier and not a secret.
    /// </summary>
    Handle = 0,

    /// <summary>
    /// A reference that <b>is</b> the credential, because the receipt has no owner to compare a
    /// caller against. At least 128 bits, and not expected to survive being dictated.
    /// </summary>
    Credential = 1
}
