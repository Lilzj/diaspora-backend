using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the membership payment binding.
/// <para>
/// <b>The unique index on the payment intent is the concurrency control, not a hygiene index.</b>
/// Two simultaneous binding attempts for one payment both pass any handler-level check and both
/// reach <c>SaveChanges</c>; the database refuses the second, and the handler answers that caller
/// with the binding that won. Enforcing it in the handler alone would leave the window between the
/// read and the write, which is exactly where a double-tapped pay button lands.
/// </para>
/// </summary>
public sealed class MembershipPaymentBindingConfiguration
    : IEntityTypeConfiguration<MembershipPaymentBinding>
{
    public void Configure(EntityTypeBuilder<MembershipPaymentBinding> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.MembershipPaymentBindingsTable,
            FinancialModuleMetadata.Schema);

        builder.HasKey(binding => binding.Id);

        builder.Property(binding => binding.Id)
            .HasConversion(FinancialValueConverters.Id<MembershipPaymentBindingId>());

        builder.Property(binding => binding.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>())
            .IsRequired();

        builder.Property(binding => binding.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(binding => binding.Revision).IsConcurrencyToken();

        builder.HasIndex(binding => binding.PaymentIntentId)
            .IsUnique()
            .HasDatabaseName("ix_financial_membership_bindings_intent");

        builder.HasIndex(binding => new { binding.MembershipId, binding.CreatedAt })
            .HasDatabaseName("ix_financial_membership_bindings_membership");

        // The reconciliation sweep's predicate: bound a while ago, re-announced few times.
        builder.HasIndex(binding => new { binding.ReannounceCount, binding.CreatedAt })
            .HasDatabaseName("ix_financial_membership_bindings_reconciliation");

        builder.Ignore(binding => binding.DomainEvents);
        builder.Ignore(binding => binding.Amount);
    }
}

/// <summary>
/// Maps campaigns, with their milestones and fund-usage lines as owned tables.
/// <para>
/// <b>Both owned collections declare their key and generate none of it.</b> Without
/// <c>HasKey</c> EF invents a store-generated shadow integer inside a composite primary key, and
/// SQLite refuses the insert the moment a second row exists; without
/// <c>ValueGeneratedNever</c> a caller-supplied key makes EF emit an <c>UPDATE</c> where an
/// <c>INSERT</c> was meant, and it surfaces as a bogus concurrency conflict. Both have cost this
/// platform a debugging session before.
/// </para>
/// <para>
/// <b>The index on the cause code is unique.</b> One campaign per cause: a second campaign
/// collecting through the same cause would make "what did this campaign raise" unanswerable, since
/// the total belongs to the cause.
/// </para>
/// </summary>
public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.CampaignsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Id)
            .HasConversion(FinancialValueConverters.Id<CampaignId>());

        builder.Property(campaign => campaign.CauseId)
            .HasConversion(FinancialValueConverters.Id<DonationCauseId>());

        builder.Property(campaign => campaign.Revision).IsConcurrencyToken();

        builder.Property(campaign => campaign.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.Category)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.BeneficiaryType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.Urgency)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.Title)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(campaign => campaign.Summary)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(campaign => campaign.Story)
            .HasMaxLength(FinancialPersistenceConstants.StoryLength)
            .IsRequired();

        builder.Property(campaign => campaign.OrganizerName)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(campaign => campaign.BeneficiaryName)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(campaign => campaign.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(campaign => campaign.CauseCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(campaign => campaign.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(campaign => campaign.LocationLabel)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(campaign => campaign.CoverImageReference)
            .HasMaxLength(FinancialPersistenceConstants.MediaReferenceLength);

        builder.Property(campaign => campaign.Gallery)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(campaign => campaign.AcceptedKinds)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength)
            .IsRequired();

        builder.Property(campaign => campaign.ReviewNote)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength);

        builder.Property(campaign => campaign.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.OwnsMany(campaign => campaign.Milestones, milestone =>
        {
            milestone.ToTable(
                FinancialPersistenceConstants.CampaignMilestonesTable,
                FinancialModuleMetadata.Schema);

            milestone.HasKey(row => row.Id);
            milestone.Property(row => row.Id).ValueGeneratedNever();

            milestone.Property(row => row.Label)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength)
                .IsRequired();

            milestone.HasIndex("CampaignId", nameof(CampaignMilestone.TargetMinorUnits))
                .IsUnique()
                .HasDatabaseName("ix_financial_campaign_milestones_target");
        });

        builder.OwnsMany(campaign => campaign.FundUsage, line =>
        {
            line.ToTable(
                FinancialPersistenceConstants.CampaignFundUsageTable,
                FinancialModuleMetadata.Schema);

            line.HasKey(row => row.Id);
            line.Property(row => row.Id).ValueGeneratedNever();

            line.Property(row => row.Label)
                .HasMaxLength(FinancialPersistenceConstants.LabelLength)
                .IsRequired();

            line.Property(row => row.Note)
                .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

            line.HasIndex("CampaignId")
                .HasDatabaseName("ix_financial_campaign_fund_usage_campaign");
        });

        builder.HasIndex(campaign => campaign.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_financial_campaigns_idempotency");

        builder.HasIndex(campaign => campaign.CauseCode)
            .IsUnique()
            .HasDatabaseName("ix_financial_campaigns_cause");

        builder.HasIndex(campaign => new { campaign.Status, campaign.CreatedAt })
            .HasDatabaseName("ix_financial_campaigns_status");

        builder.HasIndex(campaign => new { campaign.OrganizationId, campaign.Status })
            .HasDatabaseName("ix_financial_campaigns_organization");

        builder.HasIndex(campaign => new { campaign.OrganizerUserId, campaign.CreatedAt })
            .HasDatabaseName("ix_financial_campaigns_organizer");

        builder.Ignore(campaign => campaign.DomainEvents);
        builder.Ignore(campaign => campaign.Target);
        builder.Ignore(campaign => campaign.AcceptedKindList);
        builder.Ignore(campaign => campaign.GalleryReferences);
        builder.Ignore(campaign => campaign.AllocatedMinorUnits);
        builder.Ignore(campaign => campaign.DisbursedMinorUnits);
    }
}

/// <summary>
/// Maps campaign updates.
/// <para>
/// Its own table rather than an owned collection, because a campaign that runs for a year
/// accumulates updates and every read of the campaign would otherwise load all of them.
/// </para>
/// </summary>
public sealed class CampaignUpdateConfiguration : IEntityTypeConfiguration<CampaignUpdate>
{
    public void Configure(EntityTypeBuilder<CampaignUpdate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.CampaignUpdatesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(update => update.Id);

        builder.Property(update => update.Id)
            .HasConversion(FinancialValueConverters.Id<CampaignUpdateId>());

        builder.Property(update => update.CampaignId)
            .HasConversion(FinancialValueConverters.Id<CampaignId>())
            .IsRequired();

        builder.Property(update => update.Revision).IsConcurrencyToken();

        builder.Property(update => update.Title)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(update => update.Body)
            .HasMaxLength(FinancialPersistenceConstants.StoryLength)
            .IsRequired();

        builder.Property(update => update.AuthorName)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(update => update.MediaReferences)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(update => update.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.HasIndex(update => update.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_financial_campaign_updates_idempotency");

        builder.HasIndex(update => new { update.CampaignId, update.PublishedAt })
            .HasDatabaseName("ix_financial_campaign_updates_campaign");

        builder.Ignore(update => update.DomainEvents);
        builder.Ignore(update => update.Media);
    }
}

/// <summary>
/// Maps recurring donation schedules.
/// <para>
/// <b>The index on the due date is what makes the worker's query a seek rather than a scan.</b> A
/// sweep runs every few minutes over a table that grows for ever, and the rows it wants are the
/// handful whose next charge has come round.
/// </para>
/// </summary>
public sealed class RecurringDonationConfiguration : IEntityTypeConfiguration<RecurringDonation>
{
    public void Configure(EntityTypeBuilder<RecurringDonation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.RecurringDonationsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(schedule => schedule.Id);

        builder.Property(schedule => schedule.Id)
            .HasConversion(FinancialValueConverters.Id<RecurringDonationId>());

        builder.Property(schedule => schedule.CauseId)
            .HasConversion(FinancialValueConverters.Id<DonationCauseId>())
            .IsRequired();

        builder.Property(schedule => schedule.LastDonationId)
            .HasConversion(FinancialValueConverters.Id<DonationId>());

        builder.Property(schedule => schedule.OriginDonationId)
            .HasConversion(FinancialValueConverters.Id<DonationId>());

        builder.Property(schedule => schedule.Revision).IsConcurrencyToken();

        builder.Property(schedule => schedule.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(schedule => schedule.CauseCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(schedule => schedule.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(schedule => schedule.LastFailureReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.Property(schedule => schedule.IdempotencyKey)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.HasIndex(schedule => schedule.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_financial_recurring_donations_idempotency");

        builder.HasIndex(schedule => new { schedule.Status, schedule.NextChargeAt })
            .HasDatabaseName("ix_financial_recurring_donations_due");

        builder.HasIndex(schedule => new { schedule.DonorUserId, schedule.CreatedAt })
            .HasDatabaseName("ix_financial_recurring_donations_donor");

        builder.HasIndex(schedule => schedule.CauseId)
            .HasDatabaseName("ix_financial_recurring_donations_cause");

        builder.Ignore(schedule => schedule.DomainEvents);
        builder.Ignore(schedule => schedule.Amount);
    }
}
