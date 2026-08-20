using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// Where the conversation came from, as the client reported it.
/// <para>
/// <b>Separate from <see cref="ConversationContext"/> because it answers a different question and
/// has a different audience.</b> The context is what the two people were talking about and is
/// rendered to them; this is provenance — which screen, which campaign, which app build — and is
/// read by nobody but analytics and a support engineer reconstructing a bug report. Folding them
/// together would put a campaign code in a value object the client renders and make every equality
/// comparison of "same context" sensitive to which banner somebody tapped.
/// </para>
/// <para>
/// Every field is optional and caller-supplied. Nothing here is trusted or acted on: an app version
/// is a claim by the app, and a decision taken on it would be a decision taken on a header.
/// </para>
/// </summary>
public sealed class ConversationMetadata : ValueObject
{
    public const int MaxFieldLength = 64;

    /// <summary>
    /// An empty record, freshly built on every read.
    /// <para>
    /// <b>Expression-bodied, not an auto-property initialised once, and the difference is a real
    /// defect this platform has already paid for.</b> A cached owned instance is tracked by EF
    /// against whichever aggregate referenced it first; the second aggregate to use the same object
    /// writes nulls into its own NOT NULL columns and the failure surfaces nowhere near the cause.
    /// An architecture test asserts this shape across every owned type in the platform — and it
    /// caught this very property, which had been written as a cached singleton in the same file that
    /// documents why not to.
    /// </para>
    /// </summary>
    public static ConversationMetadata Empty => new(null, null, null);

    private ConversationMetadata(string? campaignSource, string? referralCode, string? appVersion)
    {
        CampaignSource = campaignSource;
        ReferralCode = referralCode;
        AppVersion = appVersion;
    }

    private ConversationMetadata() { }

    public string? CampaignSource { get; private set; }

    public string? ReferralCode { get; private set; }

    public string? AppVersion { get; private set; }

    public bool IsEmpty =>
        CampaignSource is null && ReferralCode is null && AppVersion is null;

    /// <summary>
    /// Builds a metadata record.
    /// <para>
    /// <b>Never returns the <see cref="Empty"/> singleton.</b> A cached instance shared between two
    /// aggregates is the owned-type defect this platform has already paid for twice: EF tracks one
    /// object, writes it under the first owner and leaves the second owner's columns null. The
    /// singleton exists for comparisons and for callers that never persist.
    /// </para>
    /// </summary>
    public static ConversationMetadata Create(
        string? campaignSource = null, string? referralCode = null, string? appVersion = null) =>
        new(Clamp(campaignSource), Clamp(referralCode), Clamp(appVersion));

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= MaxFieldLength ? trimmed : trimmed[..MaxFieldLength];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CampaignSource;
        yield return ReferralCode;
        yield return AppVersion;
    }
}
