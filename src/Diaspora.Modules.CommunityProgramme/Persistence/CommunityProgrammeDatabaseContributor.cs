using Diaspora.Modules.CommunityProgramme.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence;

/// <summary>
/// Adds the Community Programme module's tables to the shared context.
/// <para>
/// Twenty tables in the module's own <c>community_programme</c> schema, which the context verifies at
/// startup. Nine configurations rather than twenty, because eleven of those tables are owned by an
/// aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// <b>Three aggregates are <c>ISoftDeletable</c></b> — the opportunity, the poll and the announcement
/// — so the shared context applies the global query filter and <c>SoftDeleteConverter</c> handles the
/// owned rows on delete. Nothing here declares either, deliberately: B1.0.1 exists so a module never
/// has to.
/// </para>
/// <para>
/// <b>The six that are not are deliberate, and each for a different reason.</b> A <b>programme</b>
/// that vanished would orphan every membership sold under it. A <b>membership</b> is the record of a
/// payment somebody made; leaving is a status, and it is a status precisely so the record survives.
/// An <b>application</b> and an <b>assignment</b> are what makes a confirmed hour defensible. A
/// <b>certificate</b> is a claim about the world somebody stood behind. And a <b>poll response</b>
/// is a vote — a deleted vote would silently change a published result, which is the one thing a
/// tally must never do.
/// </para>
/// </summary>
public sealed class CommunityProgrammeDatabaseContributor : IDatabaseContributor
{
    public string Module => CommunityProgrammeModuleMetadata.Name;

    public string Schema => CommunityProgrammeModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // What an organization offers: the programme, its tiers, and each tier's benefits and
        // requirements. Four tables, one root.
        // The community aggregate. Registered first because a membership has a foreign key
        // to it and EF wants the principal known before the dependent.
        modelBuilder.ApplyConfiguration(new CommunityConfiguration());
        modelBuilder.ApplyConfiguration(new CommunityMembershipConfiguration());

        modelBuilder.ApplyConfiguration(new MembershipProgrammeConfiguration());

        // Who bought one, and where that is in its fourteen-state life. Its own aggregate for the
        // reason B12's membership is: a national association has tens of thousands of members.
        modelBuilder.ApplyConfiguration(new ProgrammeMembershipConfiguration());

        // The volunteer programme: the ask, the offer, the placement and the record.
        modelBuilder.ApplyConfiguration(new VolunteerOpportunityConfiguration());
        modelBuilder.ApplyConfiguration(new VolunteerApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new VolunteerAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new VolunteerCertificateConfiguration());

        // The question and the answers. Two aggregates, because a poll put to a country has as many
        // responses as there are members and the tally has to stay in the database.
        modelBuilder.ApplyConfiguration(new CommunityPollConfiguration());
        modelBuilder.ApplyConfiguration(new PollResponseConfiguration());

        // The scoped post. Delivery is B13's; this is only the record of what was said.
        modelBuilder.ApplyConfiguration(new CommunityAnnouncementConfiguration());
    }
}
