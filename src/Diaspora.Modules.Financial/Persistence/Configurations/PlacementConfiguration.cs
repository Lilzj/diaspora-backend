using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Placements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps purchased placements.
/// <para>
/// <b>The index on subject type, subject id and status is the one that matters.</b> "Is this listing
/// paid for, and until when" is the question every consuming surface asks, and it is asked in
/// batches — a list page resolves fifty subjects at once. Indexing it is what keeps the Financial
/// Platform out of the critical path of somebody scrolling.
/// </para>
/// </summary>
public sealed class PlacementConfiguration : IEntityTypeConfiguration<Placement>
{
    public void Configure(EntityTypeBuilder<Placement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.PlacementsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(placement => placement.Id);

        builder.Property(placement => placement.Id)
            .HasConversion(FinancialValueConverters.Id<PlacementId>());

        builder.Property(placement => placement.PurchaseId)
            .HasConversion(FinancialValueConverters.Id<MonetizationPurchaseId>());

        builder.Property(placement => placement.Revision).IsConcurrencyToken();

        builder.Property(placement => placement.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(placement => placement.Status)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(placement => placement.PriorityWeight)
            .HasPrecision(
                FinancialPersistenceConstants.RatePrecision,
                FinancialPersistenceConstants.RateScale);

        builder.Property(placement => placement.ProductCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(placement => placement.PolicyVersion)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(placement => placement.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(placement => placement.EndReason)
            .HasMaxLength(FinancialPersistenceConstants.ReasonLength);

        builder.OwnsOne(placement => placement.Subject, subject =>
        {
            subject.Property(reference => reference.SubjectType)
                .HasConversion<string>()
                .HasMaxLength(FinancialPersistenceConstants.EnumLength)
                .HasColumnName("SubjectType")
                .IsRequired();

            subject.Property(reference => reference.SubjectId)
                .HasColumnName("SubjectId")
                .IsRequired();

            subject.HasIndex(
                    nameof(Domain.ValueObjects.SubjectReference.SubjectType),
                    nameof(Domain.ValueObjects.SubjectReference.SubjectId))
                .HasDatabaseName("ix_financial_placements_subject");
        });

        builder.Navigation(placement => placement.Subject).IsRequired();

        builder.OwnsOne(placement => placement.Window, window =>
        {
            window.Property(period => period.StartsAt)
                .HasColumnName("StartsAt")
                .IsRequired();

            window.Property(period => period.EndsAt).HasColumnName("EndsAt");

            window.HasIndex(nameof(Domain.ValueObjects.DateWindow.EndsAt))
                .HasDatabaseName("ix_financial_placements_ends");
        });

        builder.Navigation(placement => placement.Window).IsRequired();

        builder.HasIndex(placement => new { placement.PurchasedByUserId, placement.Status })
            .HasDatabaseName("ix_financial_placements_purchaser");

        builder.HasIndex(placement => placement.Status)
            .HasDatabaseName("ix_financial_placements_status");

        builder.Ignore(placement => placement.DomainEvents);
    }
}
