using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps payout destinations.
/// <para>
/// <b>There is no column here that could hold an account number.</b> The aggregate has no property
/// for one, so neither does the table — which is a guarantee a schema can be audited for rather than
/// a promise somebody has to keep.
/// </para>
/// </summary>
public sealed class PayoutDestinationConfiguration : IEntityTypeConfiguration<PayoutDestination>
{
    public void Configure(EntityTypeBuilder<PayoutDestination> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            FinancialPersistenceConstants.PayoutDestinationsTable, FinancialModuleMetadata.Schema);

        builder.HasKey(destination => destination.Id);

        builder.Property(destination => destination.Id)
            .HasConversion(FinancialValueConverters.Id<PayoutDestinationId>());

        builder.Property(destination => destination.AccountId)
            .HasConversion(FinancialValueConverters.Id<FinancialAccountId>())
            .IsRequired();

        builder.Property(destination => destination.Revision).IsConcurrencyToken();

        builder.Property(destination => destination.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(destination => destination.Label)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(destination => destination.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(destination => destination.Provider)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(destination => destination.ProviderToken)
            .HasMaxLength(FinancialPersistenceConstants.ReferenceLength);

        builder.Property(destination => destination.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.HasIndex(destination => new { destination.OwnerUserId, destination.IsRemoved })
            .HasDatabaseName("ix_financial_payout_destinations_owner");

        builder.Ignore(destination => destination.DomainEvents);
        builder.Ignore(destination => destination.IsUsable);
    }
}
