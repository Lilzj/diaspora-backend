namespace Diaspora.Modules.Guides.Domain.Authors;

/// <summary>
/// One badge Trust awarded an author, as this module's projection of it.
/// <para>
/// The pair (badge, country) is the identity: "Community Guide" for Germany and for France are
/// two facts, awarded and revoked independently. <see cref="AwardedAt"/> is Trust's timestamp,
/// not ours — it is what the revocation guard compares against under unordered delivery.
/// </para>
/// </summary>
public sealed class GuideAuthorBadge
{
    private GuideAuthorBadge(string badge, string? countryCode, DateTimeOffset awardedAt)
    {
        Badge = badge;
        CountryCode = countryCode;
        AwardedAt = awardedAt;
    }

    private GuideAuthorBadge() { }

    public string Badge { get; private set; } = string.Empty;

    public string? CountryCode { get; private set; }

    /// <summary>When Trust awarded it — the event's timestamp, never the write's.</summary>
    public DateTimeOffset AwardedAt { get; private set; }

    internal static GuideAuthorBadge Create(string badge, string? countryCode, DateTimeOffset awardedAt) =>
        new(badge, countryCode, awardedAt);

    internal bool Matches(string badge, string? countryCode) =>
        string.Equals(Badge, badge, StringComparison.Ordinal)
        && string.Equals(CountryCode, countryCode, StringComparison.Ordinal);
}
