using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps invoices.
/// <para>
/// <b>Three unique indexes, each guarding a different promise.</b> The payment: one invoice per
/// payment, so two requests issuing at once cannot invoice it twice. The (year, sequence) pair: two
/// issuers that read the same "highest so far" cannot both take the next number. The formatted
/// number: what a member quotes to support resolves to one document.
/// </para>
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.InvoicesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id)
            .HasConversion(FinancialValueConverters.Id<InvoiceId>());

        builder.Property(invoice => invoice.PaymentIntentId)
            .HasConversion(FinancialValueConverters.Id<PaymentIntentId>())
            .IsRequired();

        builder.Property(invoice => invoice.Revision).IsConcurrencyToken();

        builder.Property(invoice => invoice.Number)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(invoice => invoice.SourceKind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(invoice => invoice.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(invoice => invoice.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(invoice => invoice.PromotionCode)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(invoice => invoice.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength);

        builder.HasIndex(invoice => invoice.PaymentIntentId)
            .IsUnique()
            .HasDatabaseName("ux_financial_invoices_payment");

        builder.HasIndex(invoice => new { invoice.Year, invoice.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_financial_invoices_sequence");

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("ux_financial_invoices_number");

        builder.HasIndex(invoice => new { invoice.OwnerUserId, invoice.Year, invoice.Sequence })
            .HasDatabaseName("ix_financial_invoices_owner");

        builder.Ignore(invoice => invoice.DomainEvents);
        builder.Ignore(invoice => invoice.ListPrice);
        builder.Ignore(invoice => invoice.Discount);
        builder.Ignore(invoice => invoice.Total);
    }
}
