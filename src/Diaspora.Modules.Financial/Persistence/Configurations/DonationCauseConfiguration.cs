using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>Maps donation causes — the data behind the client's donation-causes screen.</summary>
public sealed class DonationCauseConfiguration : IEntityTypeConfiguration<DonationCause>
{
    public void Configure(EntityTypeBuilder<DonationCause> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.CausesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(cause => cause.Id);

        builder.Property(cause => cause.Id)
            .HasConversion(FinancialValueConverters.Id<DonationCauseId>());

        builder.Property(cause => cause.Revision).IsConcurrencyToken();

        builder.Property(cause => cause.Code)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(cause => cause.Title)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(cause => cause.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(cause => cause.BeneficiaryType)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(cause => cause.OrganizerName)
            .HasMaxLength(FinancialPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(cause => cause.Urgency)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(cause => cause.AcceptedKinds)
            .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

        builder.Property(cause => cause.Currency)
            .HasMaxLength(FinancialPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(cause => cause.CountryCode)
            .HasMaxLength(FinancialPersistenceConstants.CountryLength);

        builder.Property(cause => cause.LocationLabel)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(cause => cause.ContactPhone)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(cause => cause.ContactWhatsapp)
            .HasMaxLength(FinancialPersistenceConstants.LabelLength);

        builder.Property(cause => cause.MediaReference)
            .HasMaxLength(FinancialPersistenceConstants.MediaReferenceLength);

        builder.HasIndex(cause => cause.Code)
            .IsUnique()
            .HasDatabaseName("ix_financial_causes_code");

        builder.HasIndex(cause => new { cause.IsActive, cause.CountryCode })
            .HasDatabaseName("ix_financial_causes_active");

        builder.Ignore(cause => cause.DomainEvents);
        builder.Ignore(cause => cause.Raised);
        builder.Ignore(cause => cause.Target);
    }
}
