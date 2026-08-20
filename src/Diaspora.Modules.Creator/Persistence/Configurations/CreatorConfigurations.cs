using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>
/// Maps the creator projection and the tags a directory searches on.
/// <para>
/// <b>The indexes here are the public directory's, and each one answers a filter the register
/// names.</b> A discovery query that had to scan would be a discovery query that got turned off.
/// </para>
/// <para>
/// <b><c>IsDiscoverable</c> leads the composite index deliberately.</b> Every public read filters on
/// it first — the register: "a creator who opted out is <em>absent from the response</em> rather than
/// filtered by the client" — so it is the most selective column in practice and the one the planner
/// should reach for.
/// </para>
/// </summary>
public sealed class CreatorProfileConfiguration : IEntityTypeConfiguration<CreatorProfile>
{
    public void Configure(EntityTypeBuilder<CreatorProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.CreatorsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(creator => creator.Id);

        builder.Property(creator => creator.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .ValueGeneratedNever();

        builder.Property(creator => creator.Revision).IsConcurrencyToken();

        builder.Property(creator => creator.UserId).IsRequired();

        builder.Property(creator => creator.DisplayName)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(creator => creator.Handle)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength);

        builder.Property(creator => creator.AvatarUrl)
            .HasMaxLength(CreatorPersistenceConstants.UrlLength);

        builder.Property(creator => creator.CoverImageUrl)
            .HasMaxLength(CreatorPersistenceConstants.UrlLength);

        builder.Property(creator => creator.Bio)
            .HasMaxLength(CreatorLimits.MaxBioLength);

        builder.Property(creator => creator.CountryCode)
            .HasMaxLength(CreatorPersistenceConstants.CountryLength);

        builder.Property(creator => creator.Status)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(creator => creator.EarnedStatus)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(creator => creator.StandingReason)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

        // One creator record per member, refused at the database. Two rows would mean two answers to
        // "what is this person's status", and the directory would show both.
        builder.HasIndex(creator => creator.UserId)
            .IsUnique()
            .HasDatabaseName("ux_creator_profiles_user");

        // The directory's main predicate: visible creators in a country, ranked by contribution.
        builder.HasIndex(creator => new
            {
                creator.IsDiscoverable,
                creator.CountryCode,
                creator.WeightedContributionScore
            })
            .HasDatabaseName("ix_creator_profiles_discovery");

        // The verified filter, and the leaderboard's own ordering.
        builder.HasIndex(creator => new
            {
                creator.IsDiscoverable,
                creator.IsVerifiedCreator,
                creator.Status
            })
            .HasDatabaseName("ix_creator_profiles_status");

        // The lifetime leaderboard reads this directly rather than aggregating the activity ledger.
        builder.HasIndex(creator => creator.WeightedContributionScore)
            .HasDatabaseName("ix_creator_profiles_score");

        // A creator's own list of tags, and the directory's category, expertise and language filters.
        builder.OwnsMany(creator => creator.Tags, tag =>
        {
            tag.ToTable(
                CreatorPersistenceConstants.CreatorTagsTable, CreatorModuleMetadata.Schema);

            // A declared composite key rather than a shadow integer — DSP3002, and the trap it
            // exists to catch: EF invents a store-generated shadow key inside a composite primary
            // key that SQLite cannot generate, and the insert fails on the second row rather than
            // the first.
            // "CreatorProfileId" rather than "CreatorId": EF names an owned type's shadow foreign
            // key after the *owning CLR type* and its key property, not after the strongly typed
            // identifier the owner happens to use. Getting it wrong fails the model build with a
            // message about a property that "cannot be added ... because no property type was
            // specified", several steps from the line that caused it.
            tag.HasKey(
                "CreatorProfileId", nameof(CreatorTag.Kind), nameof(CreatorTag.NormalisedValue));

            tag.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            tag.Property(value => value.Value)
                .HasMaxLength(CreatorLimits.MaxTagLength)
                .IsRequired();

            tag.Property(value => value.NormalisedValue)
                .HasMaxLength(CreatorLimits.MaxTagLength)
                .IsRequired();

            // The filter's own index: "every creator whose expertise includes HOUSING".
            tag.HasIndex(value => new { value.Kind, value.NormalisedValue })
                .HasDatabaseName("ix_creator_profile_tags_lookup");
        });

        builder.Ignore(creator => creator.DomainEvents);
    }
}

/// <summary>
/// Maps the creator-programme application, its evidence and its audit trail.
/// <para>
/// <b>Nothing in this table is identity evidence.</b> Trust owns documents; this holds a
/// server-issued reference and a server-produced label, and the schema has nowhere to put anything
/// else — no blob column, no URI column, no document number column.
/// </para>
/// </summary>
public sealed class CreatorVerificationConfiguration
    : IEntityTypeConfiguration<CreatorVerificationApplication>
{
    public void Configure(EntityTypeBuilder<CreatorVerificationApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.VerificationsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(application => application.Id);

        builder.Property(application => application.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorVerificationApplicationId>())
            .ValueGeneratedNever();

        builder.Property(application => application.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(application => application.Revision).IsConcurrencyToken();

        builder.Property(application => application.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(application => application.DecisionReason)
            .HasMaxLength(CreatorPersistenceConstants.DescriptionLength);

        builder.Property(application => application.Note)
            .HasMaxLength(CreatorLimits.MaxNoteLength);

        builder.Property(application => application.IdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        // One application per creator. A second row would let two reviewers decide two different
        // answers to one question.
        builder.HasIndex(application => application.CreatorId)
            .IsUnique()
            .HasDatabaseName("ux_creator_verifications_creator");

        // The idempotency barrier for submit — B15's primitive, copied.
        builder.HasIndex(application => application.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_verifications_key");

        // The reviewer's queue.
        builder.HasIndex(application => new { application.State, application.SubmittedAt })
            .HasDatabaseName("ix_creator_verifications_queue");

        // The expiry sweep's work list.
        builder.HasIndex(application => application.ExpiresAt)
            .HasDatabaseName("ix_creator_verifications_expiry");

        builder.OwnsMany(application => application.Evidence, evidence =>
        {
            evidence.ToTable(
                CreatorPersistenceConstants.VerificationEvidenceTable, CreatorModuleMetadata.Schema);

            evidence.HasKey(value => value.EvidenceId);

            // Caller-supplied, so EF must not expect the store to generate it — the defect that
            // otherwise surfaces as a bogus concurrency conflict on the second write.
            evidence.Property(value => value.EvidenceId).ValueGeneratedNever();

            evidence.Property(value => value.Kind)
                .HasMaxLength(CreatorPersistenceConstants.CodeLength)
                .IsRequired();

            evidence.Property(value => value.Label)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength)
                .IsRequired();

            evidence.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            evidence.Property(value => value.RejectionReason)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength);
        });

        builder.OwnsMany(application => application.Acknowledged, acknowledged =>
        {
            acknowledged.ToTable(
                CreatorPersistenceConstants.VerificationAcknowledgedTable,
                CreatorModuleMetadata.Schema);

            acknowledged.HasKey("CreatorVerificationApplicationId", nameof(AcknowledgedRequirement.Key));

            acknowledged.Property(value => value.Key)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.InformationRequests, request =>
        {
            request.ToTable(
                CreatorPersistenceConstants.VerificationRequestsTable, CreatorModuleMetadata.Schema);

            request.HasKey(
                "CreatorVerificationApplicationId",
                nameof(InformationRequest.RequestedAt),
                nameof(InformationRequest.Text));

            request.Property(value => value.Text)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.Decisions, decision =>
        {
            decision.ToTable(
                CreatorPersistenceConstants.VerificationDecisionsTable, CreatorModuleMetadata.Schema);

            decision.HasKey(value => value.RecordId);
            decision.Property(value => value.RecordId).ValueGeneratedNever();

            decision.Property(value => value.PreviousState)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            decision.Property(value => value.CurrentState)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            decision.Property(value => value.Reason)
                .HasMaxLength(CreatorPersistenceConstants.DescriptionLength);

            // "Who decided what, when" — the audit query §30 asks for.
            decision.HasIndex(value => new { value.ActorUserId, value.OccurredAt })
                .HasDatabaseName("ix_creator_verification_decisions_actor");
        });

        builder.Ignore(application => application.DomainEvents);
        builder.Ignore(application => application.CanResubmit);
        builder.Ignore(application => application.CanAppeal);
    }
}
