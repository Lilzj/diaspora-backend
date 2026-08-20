using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the skill catalogue and its aliases — two tables.
/// <para>
/// <b>Both the key and every alias are globally unique, and that is the whole mechanism.</b>
/// An alias resolving to two skills would make "React" ambiguous, which defeats the reason
/// aliases exist. Two unique indexes across two tables is not the same as one across both — a
/// key colliding with another skill's alias is not prevented here, and the resolver takes keys
/// first so the collision is decided rather than undefined.
/// </para>
/// </summary>
public sealed class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.SkillsTable, JobsModuleMetadata.Schema);

        builder.HasKey(skill => skill.Id);

        builder.Property(skill => skill.Id)
            .HasConversion(JobValueConverters.Id<JobSkillId>());

        builder.Property(skill => skill.Key)
            .HasMaxLength(JobLimits.MaxSkillKey)
            .IsRequired();

        builder.Property(skill => skill.Name)
            .HasMaxLength(JobLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(skill => skill.CategoryKey)
            .HasMaxLength(JobLimits.MaxCategoryKey);

        builder.Property(skill => skill.IsActive).IsRequired();
        builder.Property(skill => skill.UsageCount).IsRequired();

        builder.OwnsMany(skill => skill.Aliases, alias =>
        {
            alias.ToTable(
                JobPersistenceConstants.SkillAliasesTable, JobsModuleMetadata.Schema);

            alias.WithOwner().HasForeignKey("skill_id");
            alias.HasKey("skill_id", nameof(JobSkillAlias.AliasKey));

            alias.Property(value => value.AliasKey)
                .HasMaxLength(JobLimits.MaxSkillKey)
                .IsRequired();

            // Globally unique: an alias resolving to two skills would make the lookup
            // ambiguous, which defeats the reason aliases exist.
            alias.HasIndex(value => value.AliasKey)
                .IsUnique()
                .HasDatabaseName("ux_job_skill_aliases_key");
        });

        builder.HasIndex(skill => skill.Key)
            .IsUnique()
            .HasDatabaseName("ux_job_skills_key");

        // Autocomplete orders by popularity within the active set.
        builder.HasIndex(skill => new { skill.IsActive, skill.UsageCount })
            .HasDatabaseName("ix_job_skills_active_usage");

        builder.Ignore(skill => skill.DomainEvents);
    }
}
