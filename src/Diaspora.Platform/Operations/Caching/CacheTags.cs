namespace Diaspora.Platform.Operations.Caching;

/// <summary>
/// The invalidation tags the platform ships.
/// <para>
/// A tag is the answer to "what else should be dropped when this changes". A guide being published
/// invalidates the guide's own entry, the country feed it appears in, the trending list it may have
/// entered and the knowledge directory's counters — four families in three modules, none of which
/// knows about the others. Without tags, either every one of them holds a stale answer until it
/// expires, or the publishing module is edited every time a new consumer starts caching.
/// </para>
/// <para>
/// Tags are coarse on purpose. A tag that names one entity is a key; the value of a tag is that it
/// names a class of entries whose truth depends on the same fact.
/// </para>
/// </summary>
public static class CacheTags
{
    /// <summary>Countries, categories, currencies, routing rules — anything a seeder owns.</summary>
    public const string ReferenceData = "reference-data";

    /// <summary>Everything the Recommendation Platform computes.</summary>
    public const string Recommendations = "recommendations";

    /// <summary>Per-member feeds. Dropped when a member's affinities move materially.</summary>
    public const string PersonalizedFeed = "personalized-feed";

    /// <summary>Shared per-country feeds. Dropped when anything publishable is published in a country.</summary>
    public const string CountryFeed = "country-feed";

    /// <summary>Trending windows.</summary>
    public const string Trending = "trending";

    public const string Providers = "providers";
    public const string Guides = "guides";
    public const string CountryIntelligence = "country-intelligence";
    public const string Community = "community";
    public const string Jobs = "jobs";
    public const string Marketplace = "marketplace";
    public const string Housing = "housing";
    public const string CommunityLife = "community-life";
    public const string Media = "media";
    public const string Identity = "identity";
    public const string Trust = "trust";
    public const string Location = "location";
    public const string Notifications = "notifications";

    /// <summary>The financial catalogue. Never balances, never ledger entries.</summary>
    public const string Financial = "financial";

    /// <summary>Flag evaluations. Dropped on every flag change and on the kill switch.</summary>
    public const string FeatureFlags = "feature-flags";

    /// <summary>Runtime configuration snapshots.</summary>
    public const string RuntimeConfiguration = "runtime-configuration";
}
