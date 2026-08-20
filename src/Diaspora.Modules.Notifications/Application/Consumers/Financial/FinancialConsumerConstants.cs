using System.Globalization;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// The two things every Financial consumer in this folder needs and none of them should write twice.
/// <para>
/// <b>Money is rendered as "minor-units currency" and never formatted here.</b> The template declares
/// the currency format and the renderer applies the reader's grouping; a consumer that formatted
/// would be deciding, at dispatch time, that a Nigerian donation should be punctuated the British
/// way. The amount is a fact about the transaction and the punctuation is a fact about the reader,
/// and this is where the platform keeps those apart.
/// </para>
/// </summary>
internal static class FinancialConsumerConstants
{
    /// <summary>
    /// The module that published the contract. Recorded as the message's source, and it is
    /// <c>Financial</c> rather than <c>Notifications</c> because a member asking "why did I get this"
    /// is asking which part of the platform decided, not which part delivered.
    /// </summary>
    public const string SourceModule = "Financial";

    /// <summary>
    /// An amount as the currency-formatted variable expects it: the value and the ISO code, space
    /// separated, with the minor units resolved into major ones.
    /// <para>
    /// <b>Two decimal places for every currency the platform takes, which is a simplification with a
    /// known edge.</b> Zero-decimal currencies exist — the yen and the won among them — and rendering
    /// ¥1,200 as ¥12.00 would be wrong by two orders of magnitude. B15's <c>Money</c> holds the
    /// exponent; this contract does not carry it, so the honest thing is to state the limit here
    /// rather than to guess per currency from a table that would go stale. Recorded in the ledger.
    /// </para>
    /// </summary>
    public static string Money(long minorUnits, string currency) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{minorUnits / 100m:0.00} {currency}");

    /// <summary>
    /// The first value that is actually a value.
    /// <para>
    /// Exists because three of these contracts carry an optional provider message, an optional
    /// provider code and nothing else — and a template rendering an empty reason reads as a defect
    /// in the platform rather than as a decline nobody explained.
    /// </para>
    /// </summary>
    public static string FirstNonEmpty(params string?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return string.Empty;
    }
}
