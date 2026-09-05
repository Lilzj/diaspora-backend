using Diaspora.Modules.Creator.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence;

/// <summary>
/// Adds the Creator Platform's tables to the shared context.
/// <para>
/// Forty-two tables in the module's own <c>creator</c> schema, which the context verifies at startup.
/// Sixteen configurations rather than forty-two, because twenty-six of those tables are owned by an
/// aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>, and that is a decision rather than an omission.</b>
/// Every aggregate in this module is either a record of what somebody did or a catalogue entry a
/// record points at. A <b>creator profile</b> carries the history behind a claimed milestone and an
/// awarded grant; leaving the programme is a discoverability flag and a status, and it is those
/// precisely so the record survives. A <b>milestone progress</b> row that vanished would let a
/// reward be claimed twice. An <b>activity entry</b> that vanished would silently change a
/// leaderboard that had already been published. A <b>redemption</b> is the record of points
/// somebody spent. A <b>certificate</b> is a claim about the world somebody stood behind. And a
/// <b>grant application</b> is what makes a funding decision defensible. Retiring any of them is a
/// state — deactivated, expired, withdrawn — which is the honest shape for all of these.
/// </para>
/// </summary>
public sealed class CreatorDatabaseContributor : IDatabaseContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public string Schema => CreatorModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The creator projection and its tags. One root, two tables. Everything else in the module
        // hangs off the creator identifier this defines.
        modelBuilder.ApplyConfiguration(new CreatorProfileConfiguration());

        // The creator-programme application: evidence by reference, the reviewer's requests, and the
        // audit trail behind every decision.
        modelBuilder.ApplyConfiguration(new CreatorVerificationConfiguration());

        // The milestone catalogue and one creator's standing against each. Two aggregates, because
        // a platform with forty milestones and a hundred thousand creators has four million of the
        // second and would not want them on the first.
        modelBuilder.ApplyConfiguration(new CreatorMilestoneConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorMilestoneProgressConfiguration());

        // The challenge catalogue and one creator's participation, split for the same reason.
        modelBuilder.ApplyConfiguration(new CreatorChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorChallengeEnrollmentConfiguration());

        // The grant programme with its served form, and the applications against it.
        modelBuilder.ApplyConfiguration(new CommunityGrantConfiguration());
        modelBuilder.ApplyConfiguration(new GrantApplicationConfiguration());

        // The Academy. The quiz is its own aggregate so the answer key lives somewhere a lesson
        // projection cannot reach.
        modelBuilder.ApplyConfiguration(new AcademySectionConfiguration());
        modelBuilder.ApplyConfiguration(new AcademyCourseConfiguration());
        modelBuilder.ApplyConfiguration(new AcademyLessonConfiguration());
        modelBuilder.ApplyConfiguration(new AcademyQuizConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorLearningProgressConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorCertificateConfiguration());

        // Referrals, and the four states the register insists stay distinct.
        modelBuilder.ApplyConfiguration(new CreatorReferralCodeConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorReferralConfiguration());

        // The sign-up bonus, the rules catalogue, the redemption catalogue and the redemptions.
        // None of these is a points balance: that is B15's single RewardAccount.
        modelBuilder.ApplyConfiguration(new SignupBonusConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorPointRuleConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorRewardDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new CreatorRewardRedemptionConfiguration());

        // The recognised-activity ledger that four of the above read. Not analytics — see the
        // aggregate's own summary.
        modelBuilder.ApplyConfiguration(new CreatorActivityConfiguration());

        // What a creator earned and what it was for. B15 records that money moved; this is the only
        // place on the platform that records what a movement meant.
        modelBuilder.ApplyConfiguration(new CreatorEarningConfiguration());
    }
}
