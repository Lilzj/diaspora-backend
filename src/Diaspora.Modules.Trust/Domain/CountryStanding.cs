using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A member's standing within one country community.
/// <para>
/// Diaspora reputation is per-country on purpose: someone who has lived in Mauritius for
/// seven years and moved to the UK last year is an expert on one and a newcomer to the
/// other, and a single global score would let their Mauritius standing vouch for UK advice
/// it has no business vouching for. The frontend already models this
/// (<c>CountryReputationScore</c> + <c>CountryExperienceInterval</c>); this entity is the
/// server-side truth behind both.
/// </para>
/// </summary>
public sealed class CountryStanding : Entity<CountryStandingId>
{
    public const int MaxCountryCodeLength = 2;
    public const int MaxRoleLength = 64;

    public TrustProfileId ProfileId { get; private set; } = default!;

    public string CountryCode { get; private set; } = default!;

    /// <summary>Points earned in this community. Never negative.</summary>
    public int Points { get; private set; }

    public ReputationLevel Level { get; private set; }

    /// <summary>Whether residency in this country has been verified.</summary>
    public bool IsVerified { get; private set; }

    /// <summary>
    /// When the member arrived. Drives the residence-duration requirement that gates guide
    /// publishing, so it is only trusted once <see cref="IsVerified"/> is true.
    /// </summary>
    public DateOnly? ArrivedOn { get; private set; }

    /// <summary>When the member left, if they have. Null means they are still there.</summary>
    public DateOnly? DepartedOn { get; private set; }

    /// <summary>Display role — "Verified Resident", "Community Expert".</summary>
    public string? Role { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private CountryStanding() { }

    private CountryStanding(
        CountryStandingId id,
        TrustProfileId profileId,
        string countryCode,
        DateTimeOffset updatedAt) : base(id)
    {
        ProfileId = profileId;
        CountryCode = countryCode;
        Level = ReputationLevel.Member;
        Role = "Diaspora Member";
        UpdatedAt = updatedAt;
    }

    internal static CountryStanding Create(
        TrustProfileId profileId,
        string countryCode,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(profileId);

        return new CountryStanding(
            CountryStandingId.New(), profileId, Normalize(countryCode), updatedAt);
    }

    internal void ConfirmResidency(DateOnly? arrivedOn, DateTimeOffset at)
    {
        IsVerified = true;
        ArrivedOn = arrivedOn ?? ArrivedOn;
        Role = "Verified Resident";
        UpdatedAt = at;

        // Verifying residency is itself worth the baseline the frontend seeds a newly
        // verified country with, so the two agree without the client inventing points.
        if (Points < ReputationThresholds.VerifiedResidentBaseline)
        {
            Points = ReputationThresholds.VerifiedResidentBaseline;
            Level = ReputationLevel.VerifiedResident;
        }
    }

    internal void RevokeResidency(DateTimeOffset at)
    {
        IsVerified = false;
        Role = "Diaspora Member";
        UpdatedAt = at;
    }

    internal void RecordDeparture(DateOnly departedOn, DateTimeOffset at)
    {
        DepartedOn = departedOn;
        UpdatedAt = at;
    }

    /// <summary>
    /// Adds points and re-derives the level. Points floor at zero — a member can lose
    /// standing but not go into debt, because a negative score would make every future
    /// contribution feel unrewarded and is not a punishment anyone designed.
    /// </summary>
    internal void AddPoints(int delta, DateTimeOffset at)
    {
        Points = Math.Max(0, Points + delta);
        Level = ReputationThresholds.LevelFor(Points);
        Role = RoleFor(Level, IsVerified);
        UpdatedAt = at;
    }

    /// <summary>
    /// Complete years of residence as at <paramref name="asOf"/>, counted only when residency
    /// is verified. An unverified claim contributes nothing to a gate that exists to check it.
    /// </summary>
    public int YearsOfResidence(DateOnly asOf)
    {
        if (!IsVerified || ArrivedOn is not { } arrived)
        {
            return 0;
        }

        var end = DepartedOn ?? asOf;
        if (end < arrived)
        {
            return 0;
        }

        var years = end.Year - arrived.Year;
        if (end.Month < arrived.Month || (end.Month == arrived.Month && end.Day < arrived.Day))
        {
            years--;
        }

        return Math.Max(0, years);
    }

    /// <summary>
    /// The 1–5 star rendering the trust centre shows beside each country. Derived rather than
    /// stored so it cannot drift from the level it is meant to summarise.
    /// </summary>
    public int Stars => Level switch
    {
        ReputationLevel.CommunityAmbassador => 5,
        ReputationLevel.CommunityExpert => 5,
        ReputationLevel.TrustedContributor => 4,
        ReputationLevel.VerifiedResident => 3,
        _ => IsVerified ? 2 : 1
    };

    private static string RoleFor(ReputationLevel level, bool isVerified) => level switch
    {
        ReputationLevel.CommunityAmbassador => "Community Ambassador",
        ReputationLevel.CommunityExpert => "Community Expert",
        ReputationLevel.TrustedContributor => "Trusted Contributor",
        ReputationLevel.VerifiedResident => "Verified Resident",
        _ => isVerified ? "Verified Resident" : "Diaspora Member"
    };

    private static string Normalize(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToUpperInvariant();
        return trimmed.Length <= MaxCountryCodeLength ? trimmed : trimmed[..MaxCountryCodeLength];
    }
}
