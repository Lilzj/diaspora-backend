namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Guides configuration shares.
/// <para>
/// Every table name is <c>guide</c>-prefixed, and not by accident: SQLite — which the
/// integration suite runs on — ignores schemas entirely, so two modules whose tables differ
/// only by schema collide there and nowhere else, which is the worst possible place for a name
/// clash to first appear. <c>categories</c>, <c>versions</c>, <c>votes</c>, <c>tags</c> and
/// <c>analytics</c> are all names some other module will eventually want; the prefix is what
/// keeps the collision from ever happening. B6 and B7 both record the same trap.
/// </para>
/// </summary>
internal static class GuidePersistenceConstants
{
    public const string GuidesTable = "guides";
    public const string GuideSectionsTable = "guide_sections";
    public const string GuideSectionBlocksTable = "guide_section_blocks";
    public const string GuideTagsTable = "guide_tags";
    public const string GuideMediaReferencesTable = "guide_media_references";
    public const string GuideProviderReferencesTable = "guide_provider_references";
    public const string GuideModerationNotesTable = "guide_moderation_notes";
    public const string GuideVersionsTable = "guide_versions";
    public const string GuideCategoriesTable = "guide_categories";
    public const string GuideSuggestionsTable = "guide_suggestions";
    public const string GuideVotesTable = "guide_votes";
    public const string GuideAnalyticsTable = "guide_analytics";
    public const string GuideAuthorStandingsTable = "guide_author_standings";
    public const string GuideAuthorBadgesTable = "guide_author_badges";
    public const string GuideDraftSnapshotsTable = "guide_draft_snapshots";

    public const int EnumLength = 32;
    public const int CountryCodeLength = 2;
    public const int LanguageCodeLength = 2;

    /// <summary>Trust's badge label, projected. Rendered, never interpreted.</summary>
    public const int BadgeLength = 100;

    /// <summary>Trust's level label on the author standing projection.</summary>
    public const int TrustLevelLength = 64;

    /// <summary>The reason on a trust mark. Shown to the author, never to readers.</summary>
    public const int ReasonLength = 500;

    /// <summary>
    /// The denormalised vote key: two 32-character N-format guids and a separator. See
    /// <c>GuideVote.VoterKey</c> for why the key exists at all.
    /// </summary>
    public const int VoterKeyLength = 65;
}
