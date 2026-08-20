using Diaspora.Modules.CommunityLife.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence;

/// <summary>
/// Adds the Community Life module's tables to the shared context.
/// <para>
/// Twenty-seven tables in the module's own <c>community_life</c> schema, which the context verifies
/// at startup. Nine configurations, not twenty-seven, because eighteen of those tables are owned by
/// an aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// <b>Three of the nine aggregates are <c>ISoftDeletable</c></b> — the organization, the event and
/// the series — so the shared context applies the global query filter and
/// <c>SoftDeleteConverter</c> handles the owned rows on delete. Nothing here declares either,
/// deliberately: B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// The six that are not are deliberate. A <b>membership</b> and an <b>RSVP</b> are records of what
/// somebody did and are ended by a status rather than a delete, because deleting either would erase
/// the history that makes readmission decisions possible and would leave the counters they settled
/// permanently wrong. A <b>report</b> is the record of a moderator's decision. The two
/// <b>catalogues</b> are reference data retired with a flag, because events and organizations store
/// their keys. And <b>analytics</b> outlives the thing it measures, because a category's attendance
/// rate is computed from events that finished years ago.
/// </para>
/// </summary>
public sealed class CommunityLifeDatabaseContributor : IDatabaseContributor
{
    public string Module => CommunityLifeModuleMetadata.Name;

    public string Schema => CommunityLifeModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The organization aggregate — owns its leaders, opening hours, languages, services, media
        // and guide references, and its moderation trail.
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());

        // Who belongs. Its own aggregate keyed by organization: a national association has tens of
        // thousands of members, and loading them to render a name would be absurd.
        modelBuilder.ApplyConfiguration(new OrganizationMembershipConfiguration());

        // The event aggregate — owns its agenda, speakers, media, guide and provider references,
        // linked resources and moderation trail.
        modelBuilder.ApplyConfiguration(new CommunityEventConfiguration());

        // B12.1. The recurring series — the rule that generates events, and the exception dates it
        // no longer owns. Its occurrences are rows in the events table above, not here: a series is
        // a generator, and an occurrence is an ordinary event that happens to have been generated.
        modelBuilder.ApplyConfiguration(new EventSeriesConfiguration());

        // Who is coming. Its own aggregate for the same reason membership is.
        modelBuilder.ApplyConfiguration(new EventRsvpConfiguration());

        // The extensible catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new OrganizationTypeConfiguration());
        modelBuilder.ApplyConfiguration(new EventCategoryConfiguration());

        // The safety surface. One table for both subjects, because the queue is one queue.
        modelBuilder.ApplyConfiguration(new CommunityLifeReportConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new EventAnalyticsConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationAnalyticsConfiguration());
    }
}
