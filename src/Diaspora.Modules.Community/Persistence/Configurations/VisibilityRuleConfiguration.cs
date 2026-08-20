using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Visibility;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps named audiences and their clauses.</summary>
public sealed class VisibilityRuleConfiguration : IEntityTypeConfiguration<VisibilityRule>
{
    public void Configure(EntityTypeBuilder<VisibilityRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.VisibilityRulesTable, CommunityModuleMetadata.Schema);

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id)
            .HasConversion(CommunityValueConverters.Id<VisibilityRuleId>());

        builder.Property(rule => rule.OwnerId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(rule => rule.Name)
            .HasMaxLength(VisibilityRule.MaxNameLength)
            .IsRequired();

        builder.Property(rule => rule.BaseLevel)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsMany(rule => rule.Clauses, clause =>
        {
            clause.ToTable(
                CommunityPersistenceConstants.VisibilityClausesTable,
                CommunityModuleMetadata.Schema);

            clause.HasKey(value => value.Id);

            clause.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<VisibilityClauseId>());

            clause.Property(value => value.Kind)
                .HasMaxLength(VisibilityClause.MaxKindLength)
                .IsRequired();

            clause.Property(value => value.Value)
                .HasMaxLength(VisibilityClause.MaxValueLength)
                .IsRequired();

            clause.WithOwner().HasForeignKey("RuleId");
            clause.Ignore(value => value.DomainEvents);
        });

        builder.HasIndex(rule => rule.OwnerId).HasDatabaseName("ix_visibility_rules_owner");

        builder.Ignore(rule => rule.DomainEvents);
    }
}
