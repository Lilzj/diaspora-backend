namespace Diaspora.Contracts.Moderation.V1;

/// <summary>
/// The owner's numbers for staff due process (7 Nov 2026). Constants, not settings: changing one is a
/// policy decision, and it should arrive as a reviewed change, not a configuration edit.
/// </summary>
public static class ModerationPolicy
{
    /// <summary>How long an owner has to appeal a decision. Consequences wait for it.</summary>
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(14);

    /// <summary>How soon a second staff member must review an emergency hide.</summary>
    public static readonly TimeSpan EmergencySecondReview = TimeSpan.FromHours(24);

    /// <summary>How soon a second staff member must check a case staff opened with no report.</summary>
    public static readonly TimeSpan StaffInitiatedCheck = TimeSpan.FromDays(7);

    /// <summary>How long a strike counts.</summary>
    public static readonly TimeSpan StrikeLifetime = TimeSpan.FromDays(365);

    /// <summary>First strike: no posting in that area for this long.</summary>
    public static readonly TimeSpan FirstStrikeBlock = TimeSpan.FromDays(7);

    /// <summary>Second strike: no posting in that area for this long.</summary>
    public static readonly TimeSpan SecondStrikeBlock = TimeSpan.FromDays(30);

    /// <summary>Open reports, with at least one serious reason, that hide something pending review.</summary>
    public const int AutomaticHideReports = 10;

    /// <summary>Reports a non-fraud case needs before it is listed in the queue.</summary>
    public const int ReportsToQueue = 2;

    /// <summary>The shortest note a decision may carry. "spam" is not an explanation.</summary>
    public const int MinimumNoteLength = 10;

    public const int MaximumNoteLength = 1000;
}

/// <summary>The content types a moderation case can be about, as each module names them.</summary>
public static class ModerationContentTypes
{
    public const string Job = "job";
    public const string Provider = "provider";
    public const string MarketplaceListing = "marketplace-listing";
    public const string HousingListing = "housing-listing";
    public const string Guide = "guide";
    public const string CommunityPost = "community-post";
    public const string CommunityComment = "community-comment";
    public const string Event = "event";
    public const string Organization = "organization";
    public const string Fundraiser = "fundraiser";
    public const string Media = "media";
}

/// <summary>Where a member posts, for strikes that stop them posting in one place for a while.</summary>
public static class ModerationAreas
{
    public const string Jobs = "jobs";
    public const string Providers = "providers";
    public const string Marketplace = "marketplace";
    public const string Housing = "housing";
    public const string Guides = "guides";
    public const string Community = "community";
    public const string Events = "events";
    public const string Fundraisers = "fundraisers";
    public const string Media = "media";
}
