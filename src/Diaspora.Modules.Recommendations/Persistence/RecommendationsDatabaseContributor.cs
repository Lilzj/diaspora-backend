using Diaspora.Modules.Recommendations.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence;

/// <summary>
/// Adds the Recommendation Platform's tables to the shared context.
/// <para>
/// Twenty-two tables in the module's own <c>recommendations</c> schema, which the context verifies at
/// startup. Eight configurations, not twenty-two, because fourteen of those tables are owned by an
/// aggregate and configured from inside it — the aggregate boundary made visible in the persistence
/// layer.
/// </para>
/// <para>
/// <b>Two aggregates are <c>ISoftDeletable</c></b> — the candidate projection and the generated set —
/// so the shared context applies the global query filter and <c>SoftDeleteConverter</c> repairs the
/// owned graph on delete. Nothing here declares either: B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// <b>The six that are not are each deliberate.</b> The <b>signal</b> is deleted for exactly two
/// reasons — retention and erasure — and both mean the row must actually be gone; a soft delete
/// would leave a member's browsing history behind a query filter, which is the opposite of what an
/// erasure request asks for. The <b>feedback</b> record is the analytics denominator and outlives
/// what it measures. The <b>rollup</b> is a measurement, not a document. The <b>profile</b> is
/// derived and rebuildable, so hard-deleting it loses nothing. The <b>preference</b> record is a
/// member's stated wishes and is meaningless once they are gone. The two <b>catalogues</b> are
/// reference data retired with a flag, because strategies and policies are referenced by key.
/// </para>
/// </summary>
public sealed class RecommendationsDatabaseContributor : IDatabaseContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public string Schema => RecommendationsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The immutable record everything else is derived from.
        modelBuilder.ApplyConfiguration(new UserSignalConfiguration());

        // What the platform inferred — owns its interests, affinities and activity counters.
        modelBuilder.ApplyConfiguration(new PersonalizationProfileConfiguration());

        // What the member said — owns its categories, blocks, surfaces and favourite places.
        modelBuilder.ApplyConfiguration(new PersonalizationPreferenceConfiguration());

        // The shadow of other modules' data — owns its matchable attributes.
        modelBuilder.ApplyConfiguration(new RecommendationCandidateConfiguration());

        // The generated page — owns its items, and each item owns its score breakdown.
        modelBuilder.ApplyConfiguration(new RecommendationSetConfiguration());

        // What members did with it.
        modelBuilder.ApplyConfiguration(new RecommendationFeedbackConfiguration());

        // The two catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new RecommendationPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new RankingStrategyConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new RecommendationAnalyticsConfiguration());
    }
}
