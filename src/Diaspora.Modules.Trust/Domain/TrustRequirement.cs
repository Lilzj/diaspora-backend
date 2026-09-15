namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// What a member must have before a capability unlocks.
/// <para>
/// This is the server-side home of the frontend's <c>GOVERNANCE_CONFIG</c>. That object
/// currently sits in <c>src/lib/trustEngine.ts</c>, which means the thresholds gating guide
/// publishing are shipped to every device and editable by anyone with a debugger. Moving
/// them here is the single most consequential change in the phase: the client keeps its copy
/// for rendering progress meters, but the server stops believing it.
/// </para>
/// <para>
/// Requirements are contributed, not hard-coded — a Jobs module declares what posting a job
/// costs. <see cref="DefaultTrustRequirements"/> holds only the four the frontend already
/// ships.
/// </para>
/// </summary>
public sealed record TrustRequirement
{
    /// <summary>Stable key — matches the frontend's <c>featureId</c> ("guides", "jobs").</summary>
    public required string CapabilityKey { get; init; }

    /// <summary>Display name — matches the frontend's <c>name</c> ("Guide Publishing").</summary>
    public required string Name { get; init; }

    /// <summary>Points needed in the target country community.</summary>
    public int MinimumCountryReputation { get; init; }

    /// <summary>Complete years of verified residence needed in the target country.</summary>
    public int MinimumResidenceYears { get; init; }

    public bool RequiresPhone { get; init; }

    public bool RequiresIdentity { get; init; }

    public bool RequiresCountryVerification { get; init; }

    /// <summary>Lowest tier that may use the capability at all.</summary>
    public TrustTier MinimumTier { get; init; } = TrustTier.Basic;

    /// <summary>How many times an hour the action may be taken. Zero means uncapped.</summary>
    public int RateLimitPerDay { get; init; }

    /// <summary>Minimum seconds between two of these actions. Zero means none.</summary>
    public int CooldownSeconds { get; init; }

    /// <summary>Whether a restricted account is blocked from this outright.</summary>
    public bool BlockedWhenRestricted { get; init; } = true;
}

/// <summary>
/// The capability keys Trust ships with. Others are contributed by the modules that own them.
/// </summary>
public static class TrustCapabilities
{
    public const string PublishGuide = "guides";
    public const string PostJob = "jobs";
    public const string ListHousing = "housing";
    public const string SellItem = "marketplace";
    public const string Comment = "comment";
    public const string PublishUpdate = "updates";
    public const string CreateEvent = "events";
    public const string CreateOrganization = "organizations";
    public const string BecomeGuide = "guide-programme";
    public const string Moderate = "moderation";
}

/// <summary>
/// The platform's default capability requirements.
/// <para>
/// Thresholds, rate limits and cooldowns are copied from the frontend's
/// <c>GOVERNANCE_CONFIG</c> and <c>TrustEngine.evaluateAction</c> so that moving enforcement
/// server-side does not change any member's experience on the day it ships. Tightening them
/// is a later, deliberate decision — not a side effect of relocating the check.
/// </para>
/// </summary>
public static class DefaultTrustRequirements
{
    public static IReadOnlyList<TrustRequirement> All { get; } =
    [
        new()
        {
            // ── Guides, homes and items: a verified account is enough — decided 15 Sep 2026 ──
            //
            // The same decision as job posting the day before, for the same reason. These asked
            // for reputation points, years of residence, a verified phone and — for guides and
            // homes — verified residency. The Guides, Housing and Marketplace services never
            // consulted any of it: `guides.publish`, `housing.listings.create` and
            // `marketplace.listings.create` require the Verified access tier and nothing else. No
            // member on a platform this new can hold the points or the years, so the thresholds'
            // only effect was to lock the app's create screens while the API accepted the post.
            //
            // The product owner chose to match what those services enforce. Trust adds no
            // threshold of its own for the three; rate limits and cooldowns are unchanged.
            CapabilityKey = TrustCapabilities.PublishGuide,
            Name = "Guide Publishing",
            MinimumCountryReputation = 0,
            MinimumResidenceYears = 0,
            RequiresPhone = false,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Untrusted,
            RateLimitPerDay = 3,
            CooldownSeconds = 60
        },
        new()
        {
            // ── A verified account is enough to post a job — decided 14 Sep 2026 ──
            //
            // This asked for a verified phone, government ID, verified residency, 300 points in
            // the country and a year's residence. Nothing on a new platform can have the last
            // two, no member held a Trust profile at all, and the Jobs service never consulted
            // any of it: `jobs:post:create` and the `jobs.post` feature require the Verified
            // access tier and nothing else. So the only thing these thresholds did was lock the
            // app's Post a job screen for every member while the API accepted the same post.
            //
            // The product owner chose to match what the Jobs service enforces. Trust adds no
            // threshold of its own here; the Verified tier is checked by Jobs, where the write
            // happens. Stronger anti-fraud checks for postings are a later, deliberate change.
            CapabilityKey = TrustCapabilities.PostJob,
            Name = "Job Posting",
            MinimumCountryReputation = 0,
            MinimumResidenceYears = 0,
            RequiresPhone = false,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Untrusted,
            RateLimitPerDay = 5,
            CooldownSeconds = 10
        },
        new()
        {
            // 15 Sep 2026: a verified account is enough. See guide publishing above.
            CapabilityKey = TrustCapabilities.ListHousing,
            Name = "Housing Listing",
            MinimumCountryReputation = 0,
            MinimumResidenceYears = 0,
            RequiresPhone = false,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Untrusted,
            RateLimitPerDay = 5,
            CooldownSeconds = 10
        },
        new()
        {
            // 15 Sep 2026: a verified account is enough. See guide publishing above.
            CapabilityKey = TrustCapabilities.SellItem,
            Name = "Marketplace Directory Selling",
            MinimumCountryReputation = 0,
            MinimumResidenceYears = 0,
            RequiresPhone = false,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Untrusted,
            RateLimitPerDay = 5,
            CooldownSeconds = 10
        },
        new()
        {
            CapabilityKey = TrustCapabilities.Comment,
            Name = "Commenting",
            MinimumCountryReputation = 10,
            MinimumResidenceYears = 0,
            RequiresPhone = true,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Verified,
            RateLimitPerDay = 30,
            CooldownSeconds = 5
        },
        new()
        {
            CapabilityKey = TrustCapabilities.PublishUpdate,
            Name = "Community Updates",
            MinimumCountryReputation = 10,
            MinimumResidenceYears = 0,
            RequiresPhone = true,
            RequiresIdentity = false,
            RequiresCountryVerification = false,
            MinimumTier = TrustTier.Verified,
            RateLimitPerDay = 10,
            CooldownSeconds = 30
        },
        new()
        {
            // Not in GOVERNANCE_CONFIG — the guide programme is a Trust concern the client
            // has no threshold for yet, so this one is the backend's own.
            CapabilityKey = TrustCapabilities.BecomeGuide,
            Name = "Community Guide Programme",
            MinimumCountryReputation = ReputationThresholds.CommunityExpert,
            MinimumResidenceYears = 3,
            RequiresPhone = true,
            RequiresIdentity = true,
            RequiresCountryVerification = true,
            MinimumTier = TrustTier.Trusted,
            RateLimitPerDay = 1,
            CooldownSeconds = 0
        }
    ];
}
