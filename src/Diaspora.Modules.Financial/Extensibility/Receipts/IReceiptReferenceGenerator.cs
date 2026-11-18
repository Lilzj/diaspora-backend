namespace Diaspora.Modules.Financial.Extensibility.Receipts;

/// <summary>
/// Produces the reference a donor or contributor keeps.
/// <para>
/// <b>For a gift with no owner it is the only key its giver has.</b> Their identifier is not stored,
/// so nothing can compare a caller against it and the reference is the whole of the access decision
/// — which is why <see cref="ReceiptReferenceStrength.Credential"/> exists and why it states a
/// floor rather than a length.
/// </para>
/// <para>
/// <b>The entropy requirement is part of this contract, not an implementation detail.</b> BBL-004
/// was six characters from a 32-symbol alphabet — a uniform 30 bits — on a route that served any
/// caller holding a reference, which made a day's receipts enumerable in about a million requests.
/// The number was chosen for legibility with no unguessability requirement written down anywhere, so
/// nothing in the code or its tests objected. It is written down here now:
/// </para>
/// <list type="bullet">
/// <item><see cref="ReceiptReferenceStrength.Handle"/> — no floor. Ownership is the credential.</item>
/// <item><see cref="ReceiptReferenceStrength.Credential"/> — <b>at least 128 bits</b>, uniform.</item>
/// </list>
/// <para>
/// The third parameter is deliberately not optional. A default would let an implementation written
/// before BBL-004 keep minting the short form for ownerless gifts and still compile, which is the
/// failure this interface now exists to make impossible.
/// </para>
/// </summary>
public interface IReceiptReferenceGenerator
{
    /// <summary>
    /// <paramref name="prefix"/> distinguishes a donation from a contribution.
    /// <paramref name="strength"/> says whether the result has to be unguessable; see
    /// <see cref="ReceiptReferenceStrength"/> for why that is the caller's question to answer.
    /// </summary>
    string Next(string prefix, DateTimeOffset now, ReceiptReferenceStrength strength);
}
