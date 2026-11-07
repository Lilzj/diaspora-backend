using Diaspora.Modules.Trust.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Trust.Persistence;

/// <summary>
/// Adds the Trust module's tables to the shared <c>DiasporaDbContext</c>.
/// <para>
/// Every table lands in the module's own <c>trust</c> schema, which the context verifies at
/// startup. That boundary matters more here than in most modules: verification documents and
/// their metadata carry retention and privacy obligations that Identity's credential tables
/// do not, and keeping them in a separate schema is what makes a separate retention policy —
/// or a later extraction into its own service — a schema-level operation rather than a
/// row-by-row migration.
/// </para>
/// </summary>
public sealed class TrustDatabaseContributor : IDatabaseContributor
{
    public string Module => TrustModuleMetadata.Name;

    public string Schema => TrustModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Verification
        modelBuilder.ApplyConfiguration(new VerificationRequestConfiguration());
        modelBuilder.ApplyConfiguration(new VerificationDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new VerificationReviewConfiguration());

        // Trust standing
        modelBuilder.ApplyConfiguration(new TrustProfileConfiguration());
        modelBuilder.ApplyConfiguration(new BadgeAwardConfiguration());
        modelBuilder.ApplyConfiguration(new CountryStandingConfiguration());

        // Reputation
        modelBuilder.ApplyConfiguration(new ReputationProfileConfiguration());
        modelBuilder.ApplyConfiguration(new ReputationEventConfiguration());

        // Appeals
        modelBuilder.ApplyConfiguration(new AppealConfiguration());
        modelBuilder.ApplyConfiguration(new AppealReviewConfiguration());

        // Timeline
        modelBuilder.ApplyConfiguration(new TrustEventConfiguration());

        // Moderation
        modelBuilder.ApplyConfiguration(new ModerationItemConfiguration());
        modelBuilder.ApplyConfiguration(new ModerationDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new ModerationCaseReportConfiguration());
        modelBuilder.ApplyConfiguration(new StrikeConfiguration());

        // Community confirmations
        modelBuilder.ApplyConfiguration(new CommunityContributionConfiguration());

        // Risk evidence
        modelBuilder.ApplyConfiguration(new RiskSignalConfiguration());
    }
}
