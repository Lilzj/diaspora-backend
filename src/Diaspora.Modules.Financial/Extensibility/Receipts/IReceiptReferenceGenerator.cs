namespace Diaspora.Modules.Financial.Extensibility.Receipts;

/// <summary>
/// Produces the reference a donor or contributor keeps.
/// <para>
/// <b>It is the only key an anonymous donor has.</b> Their identifier is not stored, so the receipt
/// reference is what a support conversation, a refund request and a tax record all hang off — which
/// makes uniqueness and readability both requirements rather than niceties. A GUID satisfies the
/// first and fails the second badly when somebody has to read one over a phone.
/// </para>
/// </summary>
public interface IReceiptReferenceGenerator
{
    /// <summary><paramref name="prefix"/> distinguishes a donation from a contribution.</summary>
    string Next(string prefix, DateTimeOffset now);
}
