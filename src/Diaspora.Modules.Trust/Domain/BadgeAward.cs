using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A badge a member has earned, held inside their <see cref="TrustProfile"/>.
/// <para>
/// Revocation is a state change, not a delete. A member who loses <c>CountryVerified</c>
/// because their permit lapsed should be re-awardable without the profile forgetting they
/// once held it — and a support agent asking "did they ever have this?" needs an answer.
/// </para>
/// </summary>
public sealed class BadgeAward : Entity<BadgeAwardId>
{
    public const int MaxReasonLength = 500;
    public const int MaxCountryCodeLength = 2;

    public TrustProfileId ProfileId { get; private set; } = default!;

    public BadgeType Badge { get; private set; }

    /// <summary>
    /// Set for country-scoped badges. <c>CountryVerified</c> is earned once per country, so
    /// the pair (badge, country) is the identity of the award rather than the badge alone.
    /// </summary>
    public string? CountryCode { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset AwardedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>
    /// The rule that awarded it, or the administrator who did. Badges awarded by hand and
    /// badges awarded by rule are indistinguishable to the member and must not be to an
    /// auditor.
    /// </summary>
    public string? AwardedBy { get; private set; }

    private BadgeAward() { }

    private BadgeAward(
        BadgeAwardId id,
        TrustProfileId profileId,
        BadgeType badge,
        string? countryCode,
        string? awardedBy,
        DateTimeOffset awardedAt) : base(id)
    {
        ProfileId = profileId;
        Badge = badge;
        CountryCode = countryCode;
        AwardedBy = awardedBy;
        AwardedAt = awardedAt;
        IsActive = true;
    }

    internal static BadgeAward Grant(
        TrustProfileId profileId,
        BadgeType badge,
        string? countryCode,
        string? awardedBy,
        DateTimeOffset awardedAt)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        return new BadgeAward(
            BadgeAwardId.New(), profileId, badge, Normalize(countryCode), awardedBy, awardedAt);
    }

    internal void Revoke(string reason, DateTimeOffset at)
    {
        IsActive = false;
        RevokedAt = at;
        RevokedReason = reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength];
    }

    internal void Reinstate(DateTimeOffset at)
    {
        IsActive = true;
        RevokedAt = null;
        RevokedReason = null;
        AwardedAt = at;
    }

    /// <summary>Whether this award is for the same thing as the given badge/country pair.</summary>
    internal bool Matches(BadgeType badge, string? countryCode) =>
        Badge == badge
        && string.Equals(CountryCode, Normalize(countryCode), StringComparison.OrdinalIgnoreCase);

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().ToUpperInvariant();
        return trimmed.Length <= MaxCountryCodeLength ? trimmed : trimmed[..MaxCountryCodeLength];
    }
}
