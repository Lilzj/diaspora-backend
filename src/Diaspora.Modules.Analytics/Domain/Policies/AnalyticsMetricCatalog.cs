using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Domain.Policies;

/// <summary>
/// One metric, fully described.
/// <para>
/// <b>Every field is served.</b> §14 of the Phase 7 register — "do not invent metric definitions" —
/// is kept by the platform owning the sentence and the client rendering it, rather than by the app
/// shipping a glossary that drifts from what the backend measures.
/// </para>
/// </summary>
/// <param name="Key">The catalogue key. Phase 7's spelling, which is what the client switches on.</param>
/// <param name="CatalogueKey">Phase 15's dotted key for the same metric, for the shared dataset.</param>
/// <param name="Label">The tile label.</param>
/// <param name="Definition">The platform's own sentence describing what the number counts.</param>
/// <param name="Formula">How it is calculated, where a formula is meaningful. Null where it is a count.</param>
/// <param name="Format">How to render it.</param>
/// <param name="HigherIsBetter">Whether an increase is good news. Not every metric is.</param>
/// <param name="Privacy">Who may see it.</param>
/// <param name="Trust">Which trust classes contribute rows to it.</param>
/// <param name="Owner">Which backend module owns the truth behind it.</param>
/// <param name="Availability">Whether this deployment can answer it, and why not when it cannot.</param>
public sealed record AnalyticsMetricDefinition(
    string Key,
    string CatalogueKey,
    string Label,
    string Definition,
    string? Formula,
    AnalyticsMetricFormat Format,
    bool HigherIsBetter,
    AnalyticsPrivacyClass Privacy,
    AnalyticsTrustClass? Trust,
    string Owner,
    AnalyticsMetricAvailability Availability);

/// <summary>
/// Whether a metric can be answered, and by what.
/// <para>
/// <b>An unavailable metric is named and explained rather than served as zero.</b> A zero and an
/// unmeasured quantity render identically and mean opposite things; §21 of the Wave 6 brief forbade
/// inventing a reach figure from lifetime counters for exactly this reason, and B18 shipped
/// <c>reach: 0, reachAvailable: false</c> rather than a plausible number. This is the same decision
/// with a place to write the reason down.
/// </para>
/// </summary>
public enum AnalyticsMetricAvailability
{
    /// <summary>Counted from stored analytics rows. Real.</summary>
    Available = 1,

    /// <summary>
    /// The ingestion contract exists and nothing has ever submitted the event it counts, so the
    /// figure is a true zero rather than a measurement. Reported as available with a zero and a
    /// definition that says what would have to happen for it to move.
    /// </summary>
    AwaitingSource = 2,

    /// <summary>
    /// This deployment records nothing this metric could be computed from, and no honest
    /// approximation exists. Reported in <c>unavailableMetrics</c> and omitted from the tiles.
    /// </summary>
    NotMeasured = 3
}

/// <summary>
/// The metric catalogue.
/// <para>
/// <b>This file is the answer to "where is the definition of engagement rate".</b> There is exactly
/// one, it is served on every metric and by <c>GET /api/v1/analytics/definitions</c>, and the
/// client ships none of its own. FRONTEND-PHASE15-GAP-030 exists precisely because two definitions
/// of a rate — one in a client file and one in an aggregation job — is the failure §35 names.
/// </para>
/// <para>
/// <b>The engagement-rate denominator is a contract decision, taken here, and stated.</b> The
/// client's shipped glossary says "engagements divided by <em>reach</em>". This deployment does not
/// measure reach: no impression is recorded anywhere, and dividing by views while calling it reach
/// would publish a number that is wrong by however much of the audience never opened the item —
/// invisibly, and in the flattering direction. So the rate is <b>engagements ÷ views</b>, the
/// definition served with it says so in the platform's own words, and the client renders that
/// sentence rather than its own. §16 of the phase brief: "If the frontend expects a metric but the
/// backend cannot establish a valid definition, document it as a contract decision rather than
/// producing misleading data."
/// </para>
/// </summary>
public static class AnalyticsMetricCatalog
{
    /// <summary>
    /// Bumped whenever a definition or a formula changes.
    /// <para>
    /// Served beside the figures so a change in what a word means is visible as a version rather
    /// than as a cliff in a chart, and so a report can state which definition it ran under.
    /// </para>
    /// </summary>
    public const string Version = "1.2.0";

    // ── Keys, as the Phase 7 client spells them ─────────────────────────────────────────────

    public const string ContentViews = "content_views";
    public const string UniqueViewers = "unique_viewers";
    public const string Reach = "reach";
    public const string Saves = "saves";
    public const string Shares = "shares";
    public const string Comments = "comments";
    public const string HelpfulVotes = "helpful_votes";
    public const string EngagementRate = "engagement_rate";
    public const string Followers = "followers";
    public const string FollowerGrowth = "follower_growth";
    public const string ProfileViews = "profile_views";
    public const string Conversion = "conversion";
    public const string ContentPublished = "content_published";
    public const string Revenue = "revenue";
    public const string ConversionRate = "conversion_rate";

    private const string OwnerAnalytics = "B19 Analytics";

    private static readonly AnalyticsMetricDefinition[] All =
    [
        new(
            ContentViews,
            "content.views",
            "Views",
            "People who opened something you published. One person counted once per item per day, "
                + "reported by the app they opened it in.",
            "Distinct (viewer, item, day) combinations in the period",
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Client,
            OwnerAnalytics,
            AnalyticsMetricAvailability.AwaitingSource),

        new(
            UniqueViewers,
            "content.uniqueViewers",
            "Unique viewers",
            "Distinct people who opened something you published at any point in the period. Somebody "
                + "who came back on three days counts once here and three times in Views.",
            "Distinct viewers across the whole period",
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Client,
            OwnerAnalytics,
            AnalyticsMetricAvailability.AwaitingSource),

        new(
            Reach,
            "content.reach",
            "Reach",
            "Distinct people who saw your work in a feed or a search result, whether or not they "
                + "opened it. This platform does not record impressions, so reach is not measured — "
                + "it is not zero, and it is not the same as views.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            Trust: null,
            OwnerAnalytics,
            AnalyticsMetricAvailability.NotMeasured),

        new(
            Saves,
            "content.saves",
            "Saves",
            "Times somebody saved one of your items to come back to.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community",
            AnalyticsMetricAvailability.Available),

        new(
            Shares,
            "content.shares",
            "Shares",
            "Times somebody shared one of your items.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community",
            AnalyticsMetricAvailability.Available),

        new(
            Comments,
            "content.comments",
            "Comments",
            "Comments left on your content by somebody other than you.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community and B8 Guides",
            AnalyticsMetricAvailability.Available),

        new(
            HelpfulVotes,
            "content.helpfulVotes",
            "Helpful votes",
            "Times somebody marked one of your items helpful. Only the helpful reaction counts here; "
                + "every other reaction counts towards engagement.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community",
            AnalyticsMetricAvailability.Available),

        new(
            EngagementRate,
            "content.engagementRate",
            "Engagement",
            "Engagements divided by views, as a percentage. An engagement is a save, a share, a "
                + "comment or a reaction on something you published. The denominator is views rather "
                + "than reach because this platform does not measure reach.",
            "(saves + shares + comments + reactions) ÷ views × 100",
            AnalyticsMetricFormat.Percentage,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            Trust: null,
            OwnerAnalytics,
            AnalyticsMetricAvailability.Available),

        new(
            Followers,
            "reach.followers",
            "Followers",
            "People following you at the end of the period. Not measured on this deployment: "
                + "analytics observes follows as they happen and has no total from before it started "
                + "watching, so a figure here would be a partial count wearing a complete count's "
                + "name. New followers, below, is the real measurement.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community",
            AnalyticsMetricAvailability.NotMeasured),

        new(
            FollowerGrowth,
            "growth.members.net",
            "New followers",
            "Net change in followers over the period: people who started following you, less people "
                + "who stopped.",
            "follows − unfollows in the period",
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B6 Community",
            AnalyticsMetricAvailability.Available),

        new(
            Conversion,
            "conversion.count",
            "Actions taken",
            "People who took the action your content asked for: an RSVP for an event, an application "
                + "for a job, a purchase for a marketplace listing, a viewing for a housing listing. "
                + "Content that asks for nothing reports nothing here.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "B9 Jobs, B10 Marketplace, B11 Housing and B12 Community Life",
            AnalyticsMetricAvailability.Available),

        new(
            ContentPublished,
            "content.published",
            "Published",
            "Items you published in the period. A republished edit counts once, on its first "
                + "publication.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            "Every publishing module",
            AnalyticsMetricAvailability.Available),

        new(
            ProfileViews,
            "provider.profileViews",
            "Profile views",
            "People who opened your creator profile. One person counted once a day. Not measured on "
                + "this deployment: the app's telemetry allow-list has no property that names a "
                + "creator, which is deliberate, so a profile view cannot yet be attributed to one.",
            Formula: null,
            AnalyticsMetricFormat.Count,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Client,
            OwnerAnalytics,
            AnalyticsMetricAvailability.NotMeasured),

        new(
            Revenue,
            "revenue.gross",
            "Revenue",
            "Money attributed to your work in the period, before any fee. Not measured on this "
                + "deployment: attributing an earning to a piece of content is CREATOR-GAP-008 and "
                + "belongs to the financial platform, which remains the only authority on money.",
            Formula: null,
            AnalyticsMetricFormat.Currency,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Financial,
            Trust: null,
            "B15 Financial",
            AnalyticsMetricAvailability.NotMeasured),

        new(
            ConversionRate,
            "conversion.rate",
            "Conversion",
            "Actions taken divided by views, as a percentage — for a marketplace listing, purchases "
                + "per view; for a housing listing, viewings booked per view. Computed on the server "
                + "from the same two counts shown beside it, so the rate and its parts cannot "
                + "disagree. A listing nobody has viewed has no rate rather than a rate of zero.",
            "actions taken ÷ views × 100",
            AnalyticsMetricFormat.Percentage,
            HigherIsBetter: true,
            AnalyticsPrivacyClass.Owner,
            AnalyticsTrustClass.Server,
            OwnerAnalytics,
            AnalyticsMetricAvailability.Available)
    ];

    /// <summary>Every definition, in the order the tiles are laid out.</summary>
    public static IReadOnlyList<AnalyticsMetricDefinition> Definitions => All;

    /// <summary>The definition for a key, or null when the key is not in the catalogue.</summary>
    public static AnalyticsMetricDefinition? Find(string key) =>
        All.FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The headline set a creator's overview shows, in order.
    /// <para>
    /// Includes the two that are not measured. They are served as unavailable rather than dropped,
    /// because a member who has been told "reach is not measured here" knows something a member
    /// looking at a dashboard with no reach tile does not.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> CreatorHeadline =>
    [
        ContentViews,
        UniqueViewers,
        Reach,
        Saves,
        HelpfulVotes,
        Comments,
        Shares,
        Conversion,
        ContentPublished,
        FollowerGrowth,
        Followers,
        EngagementRate,
        ProfileViews,
        Revenue
    ];

    /// <summary>The per-item set a content-performance row shows, in order.</summary>
    public static IReadOnlyList<string> ContentItem =>
    [
        ContentViews,
        Saves,
        HelpfulVotes,
        Comments,
        Shares,
        Conversion,
        EngagementRate
    ];

    /// <summary>
    /// The headline set a seller's overview shows, in order.
    /// <para>
    /// <b>Only what a marketplace listing actually records.</b> A listing's views arrive from the
    /// listing screen, its sales from <c>ListingSold</c>, its publication from
    /// <c>ListingPublished</c> — and nothing else writes to marketplace content. Saves, shares and
    /// comments are measured metrics that nothing records for a listing, so serving them here would
    /// put a confident zero on every seller's dashboard: absent, but indistinguishable from none.
    /// They are left out rather than served empty.
    /// </para>
    /// <para>
    /// <b>Revenue is in the set, and served as not measured.</b> A seller told "revenue is not
    /// measured here" knows to open their wallet; a seller whose dashboard simply has no revenue
    /// tile does not. The wallet is where money is, and the financial platform is its only
    /// authority.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> SellerHeadline =>
    [
        ContentViews,
        UniqueViewers,
        Conversion,
        ConversionRate,
        ContentPublished,
        Revenue
    ];

    /// <summary>
    /// The headline set a landlord's overview shows, in order.
    /// <para>
    /// <b>Only what a housing listing actually records.</b> A home's views arrive from the listing
    /// screen, its viewings from <c>ViewingScheduled</c>, its publication from
    /// <c>HousingPublished</c> — and nothing else writes to housing content. Saves, shares and
    /// comments are left out for the seller set's reason: measured metrics that nothing records for a
    /// home would put a confident zero on every landlord's dashboard.
    /// </para>
    /// <para>
    /// <b>No revenue tile, unlike the seller's.</b> Housing takes no payment through Diaspora — a
    /// reservation holds a unit and records no amount — so there is no wallet to point a landlord at,
    /// and "revenue is not measured here" would suggest there was revenue here to measure.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> LandlordHeadline =>
    [
        ContentViews,
        UniqueViewers,
        Conversion,
        ConversionRate,
        ContentPublished
    ];

    /// <summary>Which metrics carry a sparkline on the creator overview.</summary>
    public static IReadOnlyList<string> CreatorSeries =>
    [
        ContentViews,
        Saves,
        Comments,
        Followers
    ];
}
