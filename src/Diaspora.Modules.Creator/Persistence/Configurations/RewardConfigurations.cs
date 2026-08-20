using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Referrals;
using Diaspora.Modules.Creator.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>Maps one creator's referral identity. One active code per creator, at the database.</summary>
public sealed class CreatorReferralCodeConfiguration : IEntityTypeConfiguration<CreatorReferralCode>
{
    public void Configure(EntityTypeBuilder<CreatorReferralCode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.ReferralCodesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(code => code.Id);

        builder.Property(code => code.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorReferralCodeId>())
            .ValueGeneratedNever();

        builder.Property(code => code.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(code => code.Code)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength)
            .IsRequired();

        // The code somebody types. Unique platform-wide, so a collision is refused rather than
        // silently attributing a signup to the wrong member.
        builder.HasIndex(code => code.Code)
            .IsUnique()
            .HasDatabaseName("ux_creator_referral_codes_code");

        // §12: "The same creator must not receive multiple active referral identities." This index
        // is what makes that a guarantee rather than a check the issuing handler happens to do.
        builder.HasIndex(code => code.CreatorId)
            .IsUnique()
            .HasDatabaseName("ux_creator_referral_codes_creator");

        builder.Ignore(code => code.DomainEvents);
    }
}

/// <summary>
/// Maps one person referred by one creator.
/// <para>
/// <b>The unique index on the invitee is the duplicate-referral barrier.</b> One person can be
/// referred once, by one member — which is both an anti-abuse rule and the thing that makes "who
/// gets the reward" answerable.
/// </para>
/// </summary>
public sealed class CreatorReferralConfiguration : IEntityTypeConfiguration<CreatorReferral>
{
    public void Configure(EntityTypeBuilder<CreatorReferral> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.ReferralsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(referral => referral.Id);

        builder.Property(referral => referral.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorReferralId>())
            .ValueGeneratedNever();

        builder.Property(referral => referral.ReferralCodeId)
            .HasConversion(CreatorValueConverters.Id<CreatorReferralCodeId>())
            .IsRequired();

        builder.Property(referral => referral.ReferrerCreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(referral => referral.Revision).IsConcurrencyToken();

        builder.Property(referral => referral.InviteeLabel)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(referral => referral.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(referral => referral.StateReason)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

        builder.Property(referral => referral.RewardGrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        builder.HasIndex(referral => referral.InviteeUserId)
            .IsUnique()
            .HasDatabaseName("ux_creator_referrals_invitee");

        // The member's own history, newest first, and the counts beside it.
        builder.HasIndex(referral => new { referral.ReferrerCreatorId, referral.InvitedAt })
            .HasDatabaseName("ix_creator_referrals_history");

        builder.HasIndex(referral => new { referral.ReferrerCreatorId, referral.State })
            .HasDatabaseName("ix_creator_referrals_state");

        // The expiry sweep's work list.
        builder.HasIndex(referral => new { referral.State, referral.ExpiresAt })
            .HasDatabaseName("ix_creator_referrals_expiry");

        builder.Ignore(referral => referral.DomainEvents);
    }
}

/// <summary>Maps one member's sign-up bonus.</summary>
public sealed class SignupBonusConfiguration : IEntityTypeConfiguration<SignupBonus>
{
    public void Configure(EntityTypeBuilder<SignupBonus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.SignupBonusesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(bonus => bonus.Id);

        builder.Property(bonus => bonus.Id)
            .HasConversion(CreatorValueConverters.Id<SignupBonusId>())
            .ValueGeneratedNever();

        builder.Property(bonus => bonus.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(bonus => bonus.Revision).IsConcurrencyToken();

        builder.Property(bonus => bonus.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(bonus => bonus.MemberSafeReason)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

        builder.Property(bonus => bonus.AcknowledgeIdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.Property(bonus => bonus.GrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        // One bonus per member. The whole of "AlreadyClaimed" is this index plus the state check.
        builder.HasIndex(bonus => bonus.UserId)
            .IsUnique()
            .HasDatabaseName("ux_creator_signup_bonuses_user");

        builder.HasIndex(bonus => bonus.AcknowledgeIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_signup_bonuses_key");

        // The expiry sweep's work list.
        builder.HasIndex(bonus => new { bonus.State, bonus.ExpiresAt })
            .HasDatabaseName("ix_creator_signup_bonuses_expiry");

        builder.Ignore(bonus => bonus.DomainEvents);
    }
}

/// <summary>Maps the rules catalogue: what an action pays, its cap and its cooldown.</summary>
public sealed class CreatorPointRuleConfiguration : IEntityTypeConfiguration<CreatorPointRule>
{
    public void Configure(EntityTypeBuilder<CreatorPointRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.PointRulesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorPointRuleId>())
            .ValueGeneratedNever();

        builder.Property(rule => rule.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.ActionType)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(rule => rule.Label)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(rule => rule.Description)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(rule => rule.ActionRoute)
            .HasMaxLength(CreatorPersistenceConstants.UrlLength);

        builder.Property(rule => rule.LimitLabel)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength);

        builder.Property(rule => rule.Category)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.HasIndex(rule => rule.Key)
            .IsUnique()
            .HasDatabaseName("ux_creator_point_rules_key");

        // The award path's own lookup: "what does a published guide pay".
        builder.HasIndex(rule => new { rule.IsActive, rule.ActionType })
            .HasDatabaseName("ix_creator_point_rules_action");

        builder.Ignore(rule => rule.DomainEvents);
    }
}

/// <summary>Maps the redemption catalogue.</summary>
public sealed class CreatorRewardDefinitionConfiguration
    : IEntityTypeConfiguration<CreatorRewardDefinition>
{
    public void Configure(EntityTypeBuilder<CreatorRewardDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.RewardsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(reward => reward.Id);

        builder.Property(reward => reward.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorRewardId>())
            .ValueGeneratedNever();

        builder.Property(reward => reward.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(reward => reward.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(reward => reward.Description)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(reward => reward.Category)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(reward => reward.Icon)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(reward => reward.GrantKind)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(reward => reward.MinimumStatus)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(reward => reward.Key)
            .IsUnique()
            .HasDatabaseName("ux_creator_rewards_key");

        builder.HasIndex(reward => new { reward.IsActive, reward.SortOrder })
            .HasDatabaseName("ix_creator_rewards_catalogue");

        // The availability window's own index.
        builder.HasIndex(reward => new { reward.AvailableFrom, reward.AvailableUntil })
            .HasDatabaseName("ix_creator_rewards_window");

        builder.Ignore(reward => reward.DomainEvents);
    }
}

/// <summary>
/// Maps one exchange that happened.
/// <para>
/// <b>The unique idempotency index is the concurrency barrier, and it is the important one.</b> Two
/// requests redeeming the same reward with one key produce one row and one debit; the second is
/// refused by the database rather than by a check that lost a race.
/// </para>
/// </summary>
public sealed class CreatorRewardRedemptionConfiguration
    : IEntityTypeConfiguration<CreatorRewardRedemption>
{
    public void Configure(EntityTypeBuilder<CreatorRewardRedemption> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.RedemptionsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(redemption => redemption.Id);

        builder.Property(redemption => redemption.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorRewardRedemptionId>())
            .ValueGeneratedNever();

        builder.Property(redemption => redemption.RewardId)
            .HasConversion(CreatorValueConverters.Id<CreatorRewardId>())
            .IsRequired();

        builder.Property(redemption => redemption.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(redemption => redemption.Revision).IsConcurrencyToken();

        builder.Property(redemption => redemption.RewardKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(redemption => redemption.RewardTitle)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(redemption => redemption.CatalogueVersion)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(redemption => redemption.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(redemption => redemption.GrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        builder.Property(redemption => redemption.PointsDebitReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        builder.Property(redemption => redemption.RejectionReason)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

        builder.Property(redemption => redemption.IdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.HasIndex(redemption => redemption.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_redemptions_key");

        // The member's own history, newest first.
        builder.HasIndex(redemption => new { redemption.CreatorId, redemption.CreatedAt })
            .HasDatabaseName("ix_creator_redemptions_history");

        // The per-creator cap and the cooldown both read this.
        builder.HasIndex(redemption => new { redemption.CreatorId, redemption.RewardId })
            .HasDatabaseName("ix_creator_redemptions_reward");

        builder.Ignore(redemption => redemption.DomainEvents);
    }
}
