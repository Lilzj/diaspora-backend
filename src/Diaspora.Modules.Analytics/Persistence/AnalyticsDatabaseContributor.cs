using Diaspora.Modules.Analytics.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Analytics.Persistence;

/// <summary>
/// Adds the Analytics Platform's tables to the shared context.
/// <para>
/// Seven tables in the module's own <c>analytics</c> schema, which the context verifies at startup.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>, and for analytics that is not a preference.</b> A soft
/// delete on a fact would leave a row the aggregation must remember to exclude, and the day somebody
/// forgets, a deleted event starts counting again. Analytics rows are either present or gone:
/// retention deletes them, erasure deletes them, and a recomputation over what is left is correct by
/// construction.
/// </para>
/// <para>
/// <b>The storage decision, stated.</b> §23 of the phase brief asks whether a time-series database
/// belongs here. It does not, and the reason is the shape of the read rather than the shape of the
/// write: every query this module serves is bounded by one owner or one item and by a date range,
/// which a composite B-tree index answers in a seek. The platform runs PostgreSQL, the daily rollup
/// is a narrow table with a unique key, and the raw store is retained for ninety days — the largest
/// table on a deployment with a million views a day is under a hundred million rows before
/// retention, which is ordinary. Introducing a second database technology would add an operational
/// surface, a second backup story and a second consistency model to answer a query the existing one
/// answers in single-digit milliseconds. Revisit when the platform serves platform-wide time series
/// over years, which is FRONTEND-PHASE15-GAP-001 and is not this phase.
/// </para>
/// </summary>
public sealed class AnalyticsDatabaseContributor : IDatabaseContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public string Schema => AnalyticsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The raw store. Everything else in the module is derived from it and can be rebuilt.
        modelBuilder.ApplyConfiguration(new AnalyticsEventConfiguration());

        // What analytics knows about a piece of content: whose it is, and what to call it.
        modelBuilder.ApplyConfiguration(new AnalyticsContentDimensionConfiguration());

        // The two rollups. Separate tables because an owner's figures include facts no piece of
        // content can carry — a follow names a person.
        modelBuilder.ApplyConfiguration(new ContentDailyRollupConfiguration());
        modelBuilder.ApplyConfiguration(new OwnerDailyRollupConfiguration());

        // The registry that makes an unknown event a visible refusal rather than a silent drop.
        modelBuilder.ApplyConfiguration(new AnalyticsEventSchemaConfiguration());

        // The projection's position, and the administrative rebuilds that rewind it.
        modelBuilder.ApplyConfiguration(new AnalyticsProjectionCheckpointConfiguration());
        modelBuilder.ApplyConfiguration(new AnalyticsBackfillRunConfiguration());

        // What members keep: saved report definitions, and the exports they asked for.
        modelBuilder.ApplyConfiguration(new AnalyticsSavedReportConfiguration());
        modelBuilder.ApplyConfiguration(new AnalyticsExportConfiguration());
    }
}
