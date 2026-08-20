namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The point boundaries between reputation levels.
/// <para>
/// Copied deliberately from the frontend's <c>REPUTATION_LEVEL_THRESHOLDS</c> so the level a
/// member sees on the client and the level the server gates on are the same number. If these
/// two ever disagree, a member is shown a level that does not unlock what it says it does —
/// which is worse than showing no level at all.
/// </para>
/// <para>
/// These are constants rather than configuration on purpose: a threshold change silently
/// promotes or demotes every member at once, and that should be a reviewed code change with
/// a migration behind it, not a config toggle. Per-capability requirements — which *do* need
/// tuning — live in <c>TrustRequirement</c> and are contributed, not hard-coded.
/// </para>
/// </summary>
public static class ReputationThresholds
{
    public const int Member = 0;
    public const int VerifiedResident = 100;
    public const int TrustedContributor = 350;
    public const int CommunityExpert = 1000;
    public const int CommunityAmbassador = 2500;

    /// <summary>
    /// What verifying residency in a country is worth on its own. Matches the baseline the
    /// frontend seeds a freshly verified country with (<c>verification/country.tsx</c>).
    /// </summary>
    public const int VerifiedResidentBaseline = 100;

    /// <summary>The level a given combined score earns.</summary>
    public static ReputationLevel LevelFor(int score) => score switch
    {
        >= CommunityAmbassador => ReputationLevel.CommunityAmbassador,
        >= CommunityExpert => ReputationLevel.CommunityExpert,
        >= TrustedContributor => ReputationLevel.TrustedContributor,
        >= VerifiedResident => ReputationLevel.VerifiedResident,
        _ => ReputationLevel.Member
    };

    /// <summary>The score at which a level begins.</summary>
    public static int MinimumFor(ReputationLevel level) => level switch
    {
        ReputationLevel.CommunityAmbassador => CommunityAmbassador,
        ReputationLevel.CommunityExpert => CommunityExpert,
        ReputationLevel.TrustedContributor => TrustedContributor,
        ReputationLevel.VerifiedResident => VerifiedResident,
        _ => Member
    };

    /// <summary>The next rung up, or null at the top of the ladder.</summary>
    public static ReputationLevel? NextAfter(ReputationLevel level) => level switch
    {
        ReputationLevel.Member => ReputationLevel.VerifiedResident,
        ReputationLevel.VerifiedResident => ReputationLevel.TrustedContributor,
        ReputationLevel.TrustedContributor => ReputationLevel.CommunityExpert,
        ReputationLevel.CommunityExpert => ReputationLevel.CommunityAmbassador,
        _ => null
    };
}
