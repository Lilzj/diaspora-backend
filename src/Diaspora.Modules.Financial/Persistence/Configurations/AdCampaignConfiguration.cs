using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps advertising campaigns.
/// <para>
/// <b>The table is <c>financial_ad_campaigns</c>, and <c>financial_campaigns</c> is taken.</b> That
/// one holds B15.1's fundraising campaigns — a different entity that has had the shorter name since
/// it shipped. Two tables, one noun, and the one that arrived second takes the longer name.
/// </para>
/// <para>
/// <b>Money is three columns, not an owned type.</b> Minor units, currency and exponent, exactly as
/// Donation, Contribution and every other amount-bearing aggregate in this module store it. An owned
/// <c>Money</c> here would put one table's amounts in a shape no existing projection or
/// reconciliation report knows how to read.
/// </para>
/// <para>
/// <b>Two indexes, and each answers a question somebody actually asks.</b> An advertiser opening
/// their dashboard asks "my campaigns, by state"; a reviewer opening the queue asks "everything
/// waiting to be reviewed, oldest first". Neither can afford to scan a table that grows with every
/// campaign the platform ever sold.
/// </para>
/// </summary>
public sealed class AdCampaignConfiguration : IEntityTypeConfiguration<AdCampaign>
{
    public void Configure(EntityTypeBuilder<AdCampaign> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.AdCampaignsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Id)
            .HasConversion(FinancialValueConverters.Id<AdCampaignId>())
            .ValueGeneratedNever();

        builder.Property(campaign => campaign.PurchaseId)
            .HasConversion(FinancialValueConverters.Id<MonetizationPurchaseId>());

        builder.Property(campaign => campaign.Revision).IsConcurrencyToken();

        builder.Property(campaign => campaign.OwnerUserId).IsRequired();

        builder.Property(campaign => campaign.Name)
            .HasMaxLength(AdCampaign.NameMaximumLength)
            .IsRequired();

        // Stored as names. An integer would be smaller and would make reordering either enum a
        // silent reassignment of every campaign on the platform.
        builder.Property(campaign => campaign.Objective)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(campaign => campaign.ReviewDecision)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        // ── Money ───────────────────────────────────────────────────────────────────────────

        builder.Property(campaign => campaign.BudgetMinorUnits).IsRequired();
        builder.Property(campaign => campaign.SpendMinorUnits).IsRequired();
        builder.Property(campaign => campaign.SettledMinorUnits);

        builder.Property(campaign => campaign.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(campaign => campaign.Exponent).IsRequired();

        // The computed accessors. EF stores the three columns above and nothing else.
        builder.Ignore(campaign => campaign.Budget);
        builder.Ignore(campaign => campaign.Spend);
        builder.Ignore(campaign => campaign.SettledAmount);
        builder.Ignore(campaign => campaign.RemainingBudget);

        // ── Creative ────────────────────────────────────────────────────────────────────────

        builder.Property(campaign => campaign.Headline)
            .HasMaxLength(AdCampaign.HeadlineMaximumLength);

        builder.Property(campaign => campaign.Body)
            .HasMaxLength(AdCampaign.BodyMaximumLength);

        builder.Property(campaign => campaign.ProductCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(campaign => campaign.ReviewNote)
            .HasMaxLength(AdCampaign.ReasonMaximumLength);

        builder.Property(campaign => campaign.AppealNote)
            .HasMaxLength(AdCampaign.ReasonMaximumLength);

        builder.Property(campaign => campaign.EndReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        // ── Subject and schedule ────────────────────────────────────────────────────────────
        //
        // Owned, matching Placement, so the two read identically when somebody is chasing what a
        // campaign booked.

        builder.OwnsOne(campaign => campaign.Subject, subject =>
        {
            subject.Property(reference => reference.SubjectType)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .HasColumnName("SubjectType");

            subject.Property(reference => reference.SubjectId)
                .HasColumnName("SubjectId");
        });

        builder.OwnsOne(campaign => campaign.Schedule, schedule =>
        {
            schedule.Property(window => window.StartsAt)
                .HasColumnName("StartsAt")
                .IsRequired();

            schedule.Property(window => window.EndsAt).HasColumnName("EndsAt");

            // The sweep that completes a campaign whose window has closed reads this, once a
            // minute, over every live campaign.
            schedule.HasIndex(nameof(Domain.ValueObjects.DateWindow.EndsAt))
                .HasDatabaseName("ix_financial_ad_campaigns_ends");
        });

        builder.Navigation(campaign => campaign.Schedule).IsRequired();

        // ── Audience ────────────────────────────────────────────────────────────────────────
        //
        // Owned rather than five join tables. Nothing queries a single selected country in
        // isolation, nothing references one, and the delivery engine reads the whole audience or
        // none of it — five tables would buy five joins for a value object that is always loaded
        // whole.

        builder.OwnsOne(campaign => campaign.Audience, audience =>
        {
            audience.PrimitiveCollection(selection => selection.CountryCodes)
                .HasColumnName("AudienceCountryCodes");

            audience.PrimitiveCollection(selection => selection.CommunityIds)
                .HasColumnName("AudienceCommunityIds");

            audience.PrimitiveCollection(selection => selection.NeighbourhoodIds)
                .HasColumnName("AudienceNeighbourhoodIds");

            audience.PrimitiveCollection(selection => selection.Interests)
                .HasColumnName("AudienceInterests");

            audience.PrimitiveCollection(selection => selection.Categories)
                .HasColumnName("AudienceCategories");
        });

        builder.Navigation(campaign => campaign.Audience).IsRequired();

        // ── Placements ──────────────────────────────────────────────────────────────────────
        //
        // A table of its own, because a campaign books many. The placement itself stays in
        // financial_placements and outlives this row; only the join is owned here.

        builder.OwnsMany(campaign => campaign.Placements, placement =>
        {
            placement.ToTable(
                FinancialPersistenceConstants.AdCampaignPlacementsTable,
                FinancialModuleMetadata.Schema);

            placement.WithOwner().HasForeignKey("AdCampaignId");

            // The key is the placement, declared rather than left to EF, which is what every
            // OwnsMany in this module does. Without it EF invents a shadow integer key it does not
            // populate, and the first insert dies on a NOT NULL constraint — not on the second
            // one, which is what a test written for the uniqueness rule expects to see.
            placement.HasKey(link => link.PlacementId);

            placement.Property(link => link.PlacementId)
                .HasConversion(FinancialValueConverters.Id<PlacementId>())
                .HasColumnName("PlacementId")
                .ValueGeneratedNever()
                .IsRequired();

            placement.Property(link => link.Kind)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .IsRequired();

            // No unique index here, and the absence is the point: the key above *is* the
            // constraint. One placement belongs to one campaign — two campaigns claiming the same
            // booked slot would make the per-campaign spend rollup double-count it — and a primary
            // key on PlacementId alone already refuses that. A unique index beside it would be a
            // second index on the same column, paid for on every write, enforcing nothing new.
        });

        // ── Indexes ─────────────────────────────────────────────────────────────────────────

        builder.HasIndex(campaign => new { campaign.OwnerUserId, campaign.Status })
            .HasDatabaseName("ix_financial_ad_campaigns_owner");

        builder.HasIndex(campaign => new { campaign.Status, campaign.CreatedAt })
            .HasDatabaseName("ix_financial_ad_campaigns_status");

        builder.Ignore(campaign => campaign.DomainEvents);
        builder.Ignore(campaign => campaign.IsEditable);
        builder.Ignore(campaign => campaign.IsTerminal);
        builder.Ignore(campaign => campaign.IsDelivering);
    }
}
