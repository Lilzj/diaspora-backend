using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Maps the versioned policy catalogue.
/// <para>
/// <b>The unique index is on key, scope, scope value <em>and version</em>.</b> Versions are rows —
/// publishing version 2 of marketplace commission inserts rather than updates, and version 1 stays
/// in the table for ever because every ledger entry it priced names it.
/// </para>
/// </summary>
public sealed class FinancialPolicyConfiguration : IEntityTypeConfiguration<FinancialPolicy>
{
    public void Configure(EntityTypeBuilder<FinancialPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(FinancialPersistenceConstants.PoliciesTable, FinancialModuleMetadata.Schema);

        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id)
            .HasConversion(FinancialValueConverters.Id<FinancialPolicyId>());

        builder.Property(policy => policy.Revision).IsConcurrencyToken();

        builder.Property(policy => policy.PolicyKey)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(policy => policy.Kind)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(policy => policy.Scope)
            .HasConversion<string>()
            .HasMaxLength(FinancialPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(policy => policy.ScopeValue)
            .HasMaxLength(FinancialPersistenceConstants.CodeLength);

        builder.Property(policy => policy.Description)
            .HasMaxLength(FinancialPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.OwnsOne(policy => policy.Effective, window =>
        {
            window.Property(period => period.StartsAt)
                .HasColumnName("EffectiveFrom")
                .IsRequired();

            window.Property(period => period.EndsAt).HasColumnName("EffectiveTo");
        });

        builder.Navigation(policy => policy.Effective).IsRequired();

        builder.OwnsMany(policy => policy.Parameters, parameter =>
        {
            parameter.ToTable(
                FinancialPersistenceConstants.PolicyParametersTable, FinancialModuleMetadata.Schema);

            parameter.HasKey(value => value.ParameterId);
            parameter.Property(value => value.ParameterId).ValueGeneratedNever();

            parameter.Property(value => value.Key)
                .HasMaxLength(FinancialPersistenceConstants.CodeLength)
                .IsRequired();

            parameter.Property(value => value.Value)
                .HasMaxLength(FinancialPersistenceConstants.JoinedListLength);

            parameter.HasIndex("FinancialPolicyId", nameof(PolicyParameter.Key))
                .IsUnique()
                .HasDatabaseName("ix_financial_policy_parameters_unique");
        });

        builder.HasIndex(policy => new
        {
            policy.PolicyKey,
            policy.Scope,
            policy.ScopeValue,
            policy.Version
        })
            .IsUnique()
            .HasDatabaseName("ix_financial_policies_version");

        builder.HasIndex(policy => new { policy.PolicyKey, policy.IsActive })
            .HasDatabaseName("ix_financial_policies_active");

        builder.Ignore(policy => policy.DomainEvents);
        builder.Ignore(policy => policy.VersionLabel);
        builder.Ignore(policy => policy.Specificity);
    }
}
