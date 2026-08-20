using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the employment-domain catalogue. One table.
/// <para>
/// The key is unique because postings store it: two categories sharing a key would make every
/// posting under it ambiguous, and the seeder — which runs on every startup and upserts —
/// depends on the uniqueness to know whether it is inserting or updating.
/// </para>
/// </summary>
public sealed class JobCategoryConfiguration : IEntityTypeConfiguration<JobCategory>
{
    public void Configure(EntityTypeBuilder<JobCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.CategoriesTable, JobsModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(JobValueConverters.Id<JobCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(JobLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(JobLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(JobLimits.MaxBulletLength);

        builder.Property(category => category.ParentKey)
            .HasMaxLength(JobLimits.MaxCategoryKey);

        builder.Property(category => category.IconKey)
            .HasMaxLength(JobLimits.MaxDisplayName);

        builder.Property(category => category.Order).IsRequired();
        builder.Property(category => category.IsActive).IsRequired();
        builder.Property(category => category.PostingCount).IsRequired();

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ux_job_categories_key");

        builder.HasIndex(category => new { category.IsActive, category.Order })
            .HasDatabaseName("ix_job_categories_active_order");

        builder.Ignore(category => category.DomainEvents);
    }
}
