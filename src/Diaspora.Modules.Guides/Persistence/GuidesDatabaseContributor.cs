using Diaspora.Modules.Guides.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence;

/// <summary>
/// Adds the Guides module's tables to the shared context.
/// <para>
/// Every table lives in the module's own <c>guides</c> schema, which the context verifies at
/// startup. There are far fewer configurations than tables, because the rest are owned by aggregates
/// and are configured from inside them — the aggregate boundary made visible in the persistence
/// layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// The guide is <c>ISoftDeletable</c>, so the shared context applies the global query filter
/// and <c>SoftDeleteConverter</c> handles the owned rows on delete — nothing here declares
/// either, deliberately. Versions are not soft-deletable at all: history is permanent, even
/// when the guide it records is not.
/// </para>
/// <para>
/// <b>B8.1's Knowledge Directory tables are here rather than in a contributor of their own, and
/// that is the architectural claim of the phase made concrete.</b> The Country Knowledge Directory
/// curates, organises and presents guides; the Guides module owns them. One module, one schema, one
/// contributor, one migration. A second database contributor would have suggested a second owner
/// and made the boundary between content and curation a deployment concern rather than a design
/// one.
/// </para>
/// </summary>
public sealed class GuidesDatabaseContributor : IDatabaseContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public string Schema => GuidesModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The guide aggregate — owns sections and their blocks, tags, media references,
        // provider references, and the moderation trail.
        modelBuilder.ApplyConfiguration(new GuideConfiguration());

        // Separate aggregates, keyed by guide. See each aggregate for why it is not owned.
        modelBuilder.ApplyConfiguration(new GuideVersionConfiguration());
        modelBuilder.ApplyConfiguration(new GuideCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new GuideSuggestionConfiguration());
        modelBuilder.ApplyConfiguration(new GuideVoteConfiguration());
        modelBuilder.ApplyConfiguration(new GuideAnalyticsConfiguration());

        // The Trust projection — owns the badge rows.
        modelBuilder.ApplyConfiguration(new GuideAuthorStandingConfiguration());

        modelBuilder.ApplyConfiguration(new GuideDraftSnapshotConfiguration());

        ConfigureKnowledgeDirectory(modelBuilder);

        ConfigureCountryIntelligence(modelBuilder);
    }

    /// <summary>
    /// B8.1 — the Country Knowledge Directory: curation, organisation, navigation and presentation
    /// over the guides configured above.
    /// <para>
    /// Seven aggregates. Two are soft-deletable, because archiving a navigation row or a country
    /// directory is a reversible editorial act and the platform's global filter is exactly the right
    /// way to hide one. Learning paths deliberately are not: archived is their terminal status, and
    /// the steps record what the advice used to be. The three counter aggregates carry no
    /// concurrency token, because a lost increment is one view among thousands and a conflict on the
    /// most-read screen on the platform would be a much worse outcome than an approximate number.
    /// </para>
    /// </summary>
    private static void ConfigureKnowledgeDirectory(ModelBuilder modelBuilder)
    {
        // The navigation catalogue — owns its localizations, its guide-category sources and its
        // recommended provider categories.
        modelBuilder.ApplyConfiguration(new KnowledgeCategoryConfiguration());

        // One country's directory — owns its arrangement, its curated shelf and its translations.
        modelBuilder.ApplyConfiguration(new CountryKnowledgeDirectoryConfiguration());

        // Curated onboarding — owns its steps.
        modelBuilder.ApplyConfiguration(new LearningPathConfiguration());

        // Curated next reads — owns them.
        modelBuilder.ApplyConfiguration(new KnowledgeRecommendationConfiguration());

        // Usage counters — owns the per-category breakdown.
        modelBuilder.ApplyConfiguration(new CountryKnowledgeAnalyticsConfiguration());

        // Search phrases, counted per country. The commissioning signal.
        modelBuilder.ApplyConfiguration(new KnowledgeSearchTermConfiguration());

        // The cross-vertical counter projection, maintained entirely from published contracts.
        modelBuilder.ApplyConfiguration(new CountryContextSummaryConfiguration());
    }

    /// <summary>
    /// B8.2 — the Country Intelligence Platform: official notices, the authorities that issue them,
    /// the embassy directory, emergency information, visa advisories and the public holiday calendar.
    /// <para>
    /// <b>Here rather than in a contributor of its own, and that is the architectural claim of the
    /// phase made concrete.</b> The brief is explicit that country intelligence introduces no new
    /// bounded context: the Guides module already owns what a member needs to know about a country,
    /// and official information is the authoritative half of that same question. One module, one
    /// schema, one contributor. A second would suggest a second owner and make the boundary between
    /// what a community knows and what a government said a deployment concern rather than a design
    /// one.
    /// </para>
    /// <para>
    /// Seven aggregates. Six are soft-deletable, because withdrawing a notice, a category, an
    /// authority, a mission, an advisory or a calendar entry is a reversible editorial act and the
    /// platform's global filter is exactly the right way to hide one. <c>CountryEmergencyDirectory</c>
    /// deliberately is not: a hidden row holding a number that now rings somewhere else is a
    /// liability with no upside, and a country that should show nothing has an empty directory. The
    /// analytics row carries no concurrency token, on the same reasoning B8.1's counters do not.
    /// </para>
    /// </summary>
    private static void ConfigureCountryIntelligence(ModelBuilder modelBuilder)
    {
        // The notice — owns its translations, its media references, its related items and the
        // languages the authority issued it in.
        modelBuilder.ApplyConfiguration(new OfficialNoticeConfiguration());

        // The vocabulary a notice is filed under and a member subscribes to.
        modelBuilder.ApplyConfiguration(new NoticeCategoryConfiguration());

        // Who says so. The register that makes the word "official" mean something.
        modelBuilder.ApplyConfiguration(new IssuingAuthorityConfiguration());

        // The embassy directory — owns opening hours, services and translations. Managed content,
        // never a Provider; an architecture test fails the build if it grows an owner or a rating.
        modelBuilder.ApplyConfiguration(new EmbassyConfiguration());

        // One country's emergency information — owns its contacts and their translations.
        modelBuilder.ApplyConfiguration(new CountryEmergencyDirectoryConfiguration());

        // Standing descriptions of entry rules — owns its requirement lines, related guides and
        // translations.
        modelBuilder.ApplyConfiguration(new VisaAdvisoryConfiguration());

        // The calendar — owns its translations.
        modelBuilder.ApplyConfiguration(new PublicHolidayConfiguration());

        // Usage counters — owns the per-signal breakdown.
        modelBuilder.ApplyConfiguration(new CountryIntelligenceAnalyticsConfiguration());
    }
}
