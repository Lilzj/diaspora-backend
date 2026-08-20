using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>
/// Maps provider reviews.
/// <para>
/// <b>The unique index on (provider, reviewer) is the invariant.</b> One member, one review —
/// enforced by the database rather than by a check-then-insert in a handler, because
/// check-then-insert is a race by construction and the client's double-tap on a slow connection
/// is exactly the shape that triggers it. A member who wants to change their mind edits or
/// withdraws.
/// </para>
/// <para>
/// The index carries <c>IsDeleted</c>, so a withdrawn-and-deleted review does not permanently
/// block its author from ever reviewing that provider again.
/// </para>
/// </summary>
public sealed class ProviderReviewConfiguration : IEntityTypeConfiguration<ProviderReview>
{
    public void Configure(EntityTypeBuilder<ProviderReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ProviderPersistenceConstants.ProviderReviewsTable, ProvidersModuleMetadata.Schema);

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasConversion(ProviderValueConverters.Id<ProviderReviewId>());

        builder.Property(review => review.ProviderId)
            .HasConversion(ProviderValueConverters.Id<ProviderId>())
            .IsRequired();

        builder.Property(review => review.ReviewerId)
            .HasConversion(ProviderValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(review => review.ServiceId)
            .HasConversion(ProviderValueConverters.Id<ProviderServiceId>());

        builder.Property(review => review.RespondedBy)
            .HasConversion(ProviderValueConverters.Id<UserId>());

        builder.Property(review => review.Status)
            .HasConversion<string>()
            .HasMaxLength(ProviderPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(review => review.Title)
            .HasMaxLength(ProviderReview.MaxTitleLength);

        builder.Property(review => review.Body)
            .HasMaxLength(ProviderLimits.MaxReviewBodyLength);

        builder.Property(review => review.Response)
            .HasMaxLength(ProviderReview.MaxResponseLength);

        // One member, one review. Enforced here rather than in a handler, because
        // check-then-insert is a race and a double-tap is how it gets found.
        builder.HasIndex(review => new { review.ProviderId, review.ReviewerId, review.IsDeleted })
            .IsUnique()
            .HasDatabaseName("ux_provider_reviews_provider_reviewer");

        // The provider's review page: newest first, filtered by status.
        builder.HasIndex(review => new { review.ProviderId, review.Status, review.CreatedAt })
            .HasDatabaseName("ix_provider_reviews_provider_status_created");

        // The client's "My Reviews" screen.
        builder.HasIndex(review => new { review.ReviewerId, review.CreatedAt })
            .HasDatabaseName("ix_provider_reviews_reviewer_created");

        builder.Ignore(review => review.DomainEvents);
        builder.Ignore(review => review.CountsTowardsSummary);
    }
}
