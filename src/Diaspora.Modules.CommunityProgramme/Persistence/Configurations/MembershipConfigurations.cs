using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>
/// Maps the membership programme, its tiers and each tier's benefits and requirements.
/// <para>
/// <b>Four tables from one configuration, because three of them are owned.</b> A tier outside its
/// programme is not a thing, a benefit outside its tier is not a thing, and neither can be reached
/// without loading the root — which is the aggregate boundary made visible in the persistence layer.
/// </para>
/// <para>
/// <b>Every owned collection declares its key and generates none of them.</b> Without
/// <c>HasKey</c>, EF invents a store-generated shadow integer inside a composite primary key and
/// SQLite refuses the insert the moment a second row exists; without <c>ValueGeneratedNever</c>, EF
/// emits an UPDATE where an INSERT was meant and it surfaces as a bogus concurrency conflict. Both
/// were paid for on this platform already — B10 for the first, B6 for the second — and neither is
/// visible in a test that never writes two rows.
/// </para>
/// </summary>
public sealed class MembershipProgrammeConfiguration
    : IEntityTypeConfiguration<MembershipProgramme>
{
    public void Configure(EntityTypeBuilder<MembershipProgramme> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.ProgrammesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(programme => programme.Id);

        builder.Property(programme => programme.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<MembershipProgrammeId>())
            .ValueGeneratedNever();

        builder.Property(programme => programme.Revision).IsConcurrencyToken();

        builder.Property(programme => programme.OrganizationId).IsRequired();

        builder.Property(programme => programme.Summary)
            .HasMaxLength(CommunityProgrammeLimits.MaxSummaryLength);

        builder.Property(programme => programme.Visibility)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        ConfigureTiers(builder);

        // One programme per organization. The database fact behind "this body already runs one",
        // rather than a race between two administrators opening the form at once.
        builder.HasIndex(programme => programme.OrganizationId)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_programmes_organization");

        builder.Ignore(programme => programme.DomainEvents);
    }

    private static void ConfigureTiers(EntityTypeBuilder<MembershipProgramme> builder) =>
        builder.OwnsMany(programme => programme.Tiers, tier =>
        {
            tier.ToTable(
                CommunityProgrammePersistenceConstants.ProgrammeTiersTable,
                CommunityProgrammeModuleMetadata.Schema);

            tier.HasKey(value => value.Id);

            tier.Property(value => value.Id)
                .HasConversion(CommunityProgrammeValueConverters.Id<MembershipTierId>())
                .ValueGeneratedNever();

            tier.Property(value => value.Name)
                .HasMaxLength(CommunityProgrammeLimits.MaxNameLength)
                .IsRequired();

            tier.Property(value => value.Description)
                .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

            tier.Property(value => value.BillingPeriod)
                .HasConversion<string>()
                .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
                .IsRequired();

            // The price is an owned value object of an owned entity — three columns on the tier row,
            // all nullable together. Absent means free, and the register is explicit that the
            // absence is the meaning rather than a zero.
            tier.OwnsOne(value => value.Price, price =>
            {
                price.Property(money => money.MinorUnits).HasColumnName("price_minor_units");

                price.Property(money => money.Currency)
                    .HasColumnName("price_currency")
                    .HasMaxLength(CommunityProgrammeLimits.MaxCurrencyLength);

                price.Property(money => money.Exponent).HasColumnName("price_exponent");
            });

            tier.Ignore(value => value.IsFull);
            tier.Ignore(value => value.RenewalMonths);

            ConfigureBenefits(tier);
            ConfigureRequirements(tier);
        });

    private static void ConfigureBenefits(OwnedNavigationBuilder<MembershipProgramme, MembershipTier> tier) =>
        tier.OwnsMany(value => value.Benefits, benefit =>
        {
            benefit.ToTable(
                CommunityProgrammePersistenceConstants.TierBenefitsTable,
                CommunityProgrammeModuleMetadata.Schema);

            benefit.HasKey(value => value.Id);
            benefit.Property(value => value.Id).ValueGeneratedNever();

            benefit.Property(value => value.Title)
                .HasMaxLength(CommunityProgrammeLimits.MaxLabelLength)
                .IsRequired();

            benefit.Property(value => value.Description)
                .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

            benefit.Property(value => value.Icon)
                .HasMaxLength(CommunityProgrammeLimits.MaxIconLength);
        });

    private static void ConfigureRequirements(
        OwnedNavigationBuilder<MembershipProgramme, MembershipTier> tier) =>
        tier.OwnsMany(value => value.Requirements, requirement =>
        {
            requirement.ToTable(
                CommunityProgrammePersistenceConstants.TierRequirementsTable,
                CommunityProgrammeModuleMetadata.Schema);

            requirement.HasKey(value => value.Id);
            requirement.Property(value => value.Id).ValueGeneratedNever();

            requirement.Property(value => value.Label)
                .HasMaxLength(CommunityProgrammeLimits.MaxLabelLength)
                .IsRequired();

            requirement.Property(value => value.Description)
                .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

            requirement.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
                .IsRequired();

            requirement.Property(value => value.ActionRoute)
                .HasMaxLength(CommunityProgrammeLimits.MaxRouteLength);
        });
}

/// <summary>
/// Maps the fourteen-state programme membership and its history.
/// <para>
/// <b>The unique index on (organization, member) is this module's most important constraint</b> — the
/// same one B12 puts on its own membership edge, and for the same reason: "you already hold one of
/// these" has to be a database fact rather than a race between two taps on a join button. It is also
/// why the aggregate keeps <c>Cancelled</c> and <c>Expired</c> as states rather than deleting the
/// row, and why rejoining opens a fresh record only after the old one is terminal.
/// </para>
/// <para>
/// <b>Two idempotency indexes, both unique, both filtered by nothing.</b> A unique index over a
/// nullable column permits any number of nulls on every provider this platform targets, so the
/// overwhelming majority of memberships — which carry no key — are unaffected.
/// </para>
/// </summary>
public sealed class ProgrammeMembershipConfiguration
    : IEntityTypeConfiguration<ProgrammeMembership>
{
    public void Configure(EntityTypeBuilder<ProgrammeMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.MembershipsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<ProgrammeMembershipId>())
            .ValueGeneratedNever();

        builder.Property(membership => membership.Revision).IsConcurrencyToken();

        builder.Property(membership => membership.OrganizationId).IsRequired();

        builder.Property(membership => membership.TierId)
            .HasConversion(CommunityProgrammeValueConverters.Id<MembershipTierId>())
            .IsRequired();

        builder.Property(membership => membership.MemberUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(membership => membership.DecidedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(membership => membership.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(membership => membership.DecisionReason)
            .HasMaxLength(CommunityProgrammeLimits.MaxReasonLength);

        builder.Property(membership => membership.ApplicationIdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.Property(membership => membership.RenewalIdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.OwnsMany(membership => membership.History, entry =>
        {
            entry.ToTable(
                CommunityProgrammePersistenceConstants.MembershipHistoryTable,
                CommunityProgrammeModuleMetadata.Schema);

            entry.HasKey(value => value.Id);
            entry.Property(value => value.Id).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.Actor)
                .HasConversion<string>()
                .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.Reason)
                .HasMaxLength(CommunityProgrammeLimits.MaxReasonLength);

            entry.Property(value => value.OccurredAt).IsRequired();
        });

        // One membership per member per organization, whatever its state.
        builder.HasIndex(membership =>
                new { membership.OrganizationId, membership.MemberUserId })
            .IsUnique()
            .HasDatabaseName("ux_community_programme_memberships_member");

        // The member roll, filtered by state. The most-run query in the module.
        builder.HasIndex(membership =>
                new { membership.OrganizationId, membership.Status, membership.CreatedAt })
            .HasDatabaseName("ix_community_programme_memberships_roll");

        // "What do I belong to" — rendered on the hub's first paint.
        builder.HasIndex(membership => new { membership.MemberUserId, membership.Status })
            .HasDatabaseName("ix_community_programme_memberships_member_state");

        // The two sweep work lists. Without these, finding what expired today means reading every
        // membership ever created.
        builder.HasIndex(membership => membership.RenewalDueAt)
            .HasDatabaseName("ix_community_programme_memberships_renewal_due");

        builder.HasIndex(membership => membership.ExpiresAt)
            .HasDatabaseName("ix_community_programme_memberships_expires");

        // B15's idempotency primitive, one index per operation.
        builder.HasIndex(membership => membership.ApplicationIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_memberships_application_key");

        builder.HasIndex(membership => membership.RenewalIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_memberships_renewal_key");

        builder.Ignore(membership => membership.DomainEvents);
        builder.Ignore(membership => membership.GrantsBenefits);
        builder.Ignore(membership => membership.OccupiesCapacity);
        builder.Ignore(membership => membership.IsTerminal);
        builder.Ignore(membership => membership.IsRenewable);
    }
}
