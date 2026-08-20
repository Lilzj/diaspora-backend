using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Trust.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="VerificationRequest"/> aggregate into the module's schema.
/// <para>
/// Enums are stored as strings rather than ordinals throughout this module. Ordinals are
/// smaller and faster, and they turn a reordered enum into a silent data corruption where
/// every <c>Rejected</c> row becomes <c>Approved</c>. For a table recording why people were
/// refused entry to a community, the readable-and-stable trade is not close.
/// </para>
/// </summary>
public sealed class VerificationRequestConfiguration : IEntityTypeConfiguration<VerificationRequest>
{
    public void Configure(EntityTypeBuilder<VerificationRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("verification_requests", TrustModuleMetadata.Schema);

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(new StronglyTypedIdValueConverter<VerificationRequestId>());

        builder.Property(request => request.UserId).IsRequired();

        builder.Property(request => request.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(request => request.Nationality)
            .HasMaxLength(VerificationRequest.MaxCountryCodeLength);

        builder.Property(request => request.ResidenceCountry)
            .HasMaxLength(VerificationRequest.MaxCountryCodeLength);

        builder.Property(request => request.TargetCountry)
            .HasMaxLength(VerificationRequest.MaxCountryCodeLength);

        builder.Property(request => request.SupportingNotes)
            .HasMaxLength(VerificationRequest.MaxSupportingNotesLength);

        builder.Property(request => request.SelfieStorageKey)
            .HasMaxLength(VerificationDocument.MaxStorageKeyLength);

        builder.Property(request => request.RejectionReason)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(request => request.RejectionDetails)
            .HasMaxLength(VerificationRequest.MaxRejectionDetailsLength);

        // ── B3.0.2 submission contract ──────────────────────────────────────
        // All nullable, all additive. A row written before this patch reads back with every
        // one of them null, which is exactly what "the member is the subject, and they
        // submitted it themselves" means.
        builder.Property(request => request.SubjectType)
            .HasMaxLength(VerificationRequest.MaxSubjectTypeLength);

        builder.Property(request => request.SubjectId);

        builder.Property(request => request.CorrelationId);

        builder.Property(request => request.RequestedBy)
            .HasMaxLength(VerificationRequest.MaxRequestedByLength);

        builder.Property(request => request.SubmissionMetadata)
            .HasMaxLength(VerificationRequest.MaxSubmissionMetadataLength);

        builder.Property(request => request.SubmittedAt).IsRequired();

        builder.Property(request => request.AssignedReviewerId);

        // Expertise areas are a small, bounded list read only with their parent — a join
        // table would cost a query on every read of the request to store at most ten strings.
        //
        // Joined on the ASCII unit separator rather than a comma: these are free text
        // ("visa applications, renewals") and any printable delimiter is a value someone
        // eventually types. A control character cannot survive the field validation.
        builder.Property<List<string>>("_expertiseAreas")
            .HasField("_expertiseAreas")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("expertise_areas")
            .HasConversion(
                areas => string.Join(TrustPersistenceConstants.ListSeparator, areas),
                value => value
                    .Split(TrustPersistenceConstants.ListSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .ToList(),
                new ValueComparer<List<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    areas => areas.Aggregate(0, (hash, area) => HashCode.Combine(hash, area.GetHashCode())),
                    areas => areas.ToList()))
            .HasMaxLength((VerificationRequest.MaxExpertiseAreaLength + 1)
                * VerificationRequest.MaxExpertiseAreas);

        builder.Ignore(request => request.ExpertiseAreas);

        builder.HasMany(request => request.Documents)
            .WithOne()
            .HasForeignKey(document => document.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(request => request.Reviews)
            .WithOne()
            .HasForeignKey(review => review.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(request => request.Documents)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_documents");

        builder.Navigation(request => request.Reviews)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_reviews");

        // The member's own list, and the duplicate check on submit.
        builder.HasIndex(request => new { request.UserId, request.Type, request.Status })
            .HasDatabaseName("ix_verification_requests_user_type_status");

        // The subject duplicate check: is there already a live case about this provider,
        // organisation or business? Leads with the subject because that is the selective
        // column — a deployment has many members and few claims per subject.
        builder.HasIndex(request => new
        {
            request.SubjectType, request.SubjectId, request.Type, request.Status
        })
            .HasDatabaseName("ix_verification_requests_subject_type_status");

        // The idempotency key. Unique rather than merely indexed, and filtered to rows that
        // have one: uniqueness is the guarantee that makes a retry safe, and enforcing it in
        // the database rather than only in the handler is what makes it hold under the
        // concurrent retry that is exactly when it matters.
        builder.HasIndex(request => request.CorrelationId)
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName("ux_verification_requests_correlation_id");

        // The review queue, which reads by status and orders by the SLA deadline.
        builder.HasIndex(request => new { request.Status, request.ReviewDueAt })
            .HasDatabaseName("ix_verification_requests_status_due");

        // The expiry sweep.
        builder.HasIndex(request => request.ExpiresAt)
            .HasDatabaseName("ix_verification_requests_expires_at");

        builder.Ignore(request => request.DomainEvents);
    }
}

public sealed class VerificationDocumentConfiguration : IEntityTypeConfiguration<VerificationDocument>
{
    public void Configure(EntityTypeBuilder<VerificationDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("verification_documents", TrustModuleMetadata.Schema);

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasConversion(new StronglyTypedIdValueConverter<VerificationDocumentId>());

        builder.Property(document => document.RequestId)
            .HasConversion(new StronglyTypedIdValueConverter<VerificationRequestId>())
            .IsRequired();

        builder.Property(document => document.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(document => document.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(document => document.StorageKey)
            .HasMaxLength(VerificationDocument.MaxStorageKeyLength)
            .IsRequired();

        // B3.0.2. Null for a document uploaded through the member's own wizard; set when the
        // evidence arrived as a Media reference from another module.
        builder.Property(document => document.MediaId);

        builder.Property(document => document.OriginalFileName)
            .HasMaxLength(VerificationDocument.MaxOriginalFileNameLength);

        builder.Property(document => document.ContentType)
            .HasMaxLength(VerificationDocument.MaxContentTypeLength);

        builder.Property(document => document.DocumentNumber)
            .HasMaxLength(VerificationDocument.MaxDocumentNumberLength);

        builder.Property(document => document.IssuingCountry).HasMaxLength(2);

        builder.Property(document => document.ReviewComment)
            .HasMaxLength(VerificationDocument.MaxReviewCommentLength);

        builder.Property(document => document.UploadedAt).IsRequired();

        builder.HasIndex(document => document.RequestId)
            .HasDatabaseName("ix_verification_documents_request");

        builder.Ignore(document => document.DomainEvents);
    }
}

public sealed class VerificationReviewConfiguration : IEntityTypeConfiguration<VerificationReview>
{
    public void Configure(EntityTypeBuilder<VerificationReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("verification_reviews", TrustModuleMetadata.Schema);

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasConversion(new StronglyTypedIdValueConverter<VerificationReviewId>());

        builder.Property(review => review.RequestId)
            .HasConversion(new StronglyTypedIdValueConverter<VerificationRequestId>())
            .IsRequired();

        builder.Property(review => review.ReviewerId);

        builder.Property(review => review.DecidedByName)
            .HasMaxLength(VerificationReview.MaxReviewerNameLength);

        builder.Property(review => review.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.DecisionMode)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.RejectionReason)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(review => review.Notes)
            .HasMaxLength(VerificationReview.MaxNotesLength);

        builder.Property(review => review.DecidedAt).IsRequired();

        builder.HasIndex(review => review.RequestId)
            .HasDatabaseName("ix_verification_reviews_request");

        builder.Ignore(review => review.DomainEvents);
    }
}
