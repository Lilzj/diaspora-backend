using Diaspora.Modules.Creator.Domain.Grants;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>
/// Maps the grant programme and the form somebody applies to it on.
/// <para>
/// <b>Six tables for one aggregate, and every one of them is the served form.</b> The register calls
/// <c>GET /grants/{id}/form</c> load-bearing because it makes "add a grant with eleven questions and
/// a dropdown" a backend change; these rows are what the route reads, and the same rows are what the
/// submission is validated against.
/// </para>
/// </summary>
public sealed class CommunityGrantConfiguration : IEntityTypeConfiguration<CommunityGrant>
{
    public void Configure(EntityTypeBuilder<CommunityGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.GrantsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(grant => grant.Id);

        builder.Property(grant => grant.Id)
            .HasConversion(CreatorValueConverters.Id<CommunityGrantId>())
            .ValueGeneratedNever();

        builder.Property(grant => grant.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(grant => grant.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(grant => grant.Summary)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(grant => grant.Description)
            .HasMaxLength(CreatorPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(grant => grant.Category)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength)
            .IsRequired();

        // Minor units and a currency code, the platform's shape for money. No decimal, ever.
        builder.Property(grant => grant.Currency)
            .HasMaxLength(CreatorPersistenceConstants.CurrencyLength)
            .IsRequired();

        builder.Property(grant => grant.MinimumStatus)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(grant => grant.Key)
            .IsUnique()
            .HasDatabaseName("ux_community_grants_key");

        // The discovery page: published grants in a category, soonest deadline first.
        builder.HasIndex(grant => new { grant.IsPublished, grant.Category, grant.DeadlineAt })
            .HasDatabaseName("ix_community_grants_discovery");

        builder.OwnsMany(grant => grant.Fields, field =>
        {
            field.ToTable(
                CreatorPersistenceConstants.GrantFieldsTable, CreatorModuleMetadata.Schema);

            field.HasKey("CommunityGrantId", nameof(GrantApplicationField.Key));

            field.Property(value => value.Key)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            field.Property(value => value.Label)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength)
                .IsRequired();

            field.Property(value => value.HelpText)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

            field.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();
        });

        builder.OwnsMany(grant => grant.FieldOptions, option =>
        {
            option.ToTable(
                CreatorPersistenceConstants.GrantFieldOptionsTable, CreatorModuleMetadata.Schema);

            option.HasKey(
                "CommunityGrantId",
                nameof(GrantFieldOption.FieldKey),
                nameof(GrantFieldOption.Value));

            option.Property(value => value.FieldKey)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            option.Property(value => value.Value)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            option.Property(value => value.Label)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength)
                .IsRequired();
        });

        builder.OwnsMany(grant => grant.Declarations, declaration =>
        {
            declaration.ToTable(
                CreatorPersistenceConstants.GrantDeclarationsTable, CreatorModuleMetadata.Schema);

            declaration.HasKey("CommunityGrantId", nameof(GrantDeclaration.Key));

            declaration.Property(value => value.Key)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            declaration.Property(value => value.Text)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();
        });

        builder.OwnsMany(grant => grant.Deliverables, deliverable =>
        {
            deliverable.ToTable(
                CreatorPersistenceConstants.GrantDeliverablesTable, CreatorModuleMetadata.Schema);

            deliverable.HasKey("CommunityGrantId", nameof(GrantDeliverable.SortOrder));

            deliverable.Property(value => value.SortOrder).ValueGeneratedNever();

            deliverable.Property(value => value.Text)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();
        });

        builder.OwnsMany(grant => grant.Countries, country =>
        {
            country.ToTable(
                CreatorPersistenceConstants.GrantCountriesTable, CreatorModuleMetadata.Schema);

            country.HasKey("CommunityGrantId", nameof(GrantCountry.CountryCode));

            country.Property(value => value.CountryCode)
                .HasMaxLength(CreatorPersistenceConstants.CountryLength)
                .IsRequired();
        });

        builder.Ignore(grant => grant.DomainEvents);
    }
}

/// <summary>
/// Maps one application, its answers, its reports and its audit trail.
/// <para>
/// <b>The audit table is why a decision is defensible.</b> §29 of the phase brief asks for who
/// reviewed, who changed state, when, the previous state, the new state and the reason — one row
/// per transition, indexed by actor, and never served on an applicant endpoint.
/// </para>
/// </summary>
public sealed class GrantApplicationConfiguration : IEntityTypeConfiguration<GrantApplication>
{
    public void Configure(EntityTypeBuilder<GrantApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CreatorPersistenceConstants.GrantApplicationsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(application => application.Id);

        builder.Property(application => application.Id)
            .HasConversion(CreatorValueConverters.Id<GrantApplicationId>())
            .ValueGeneratedNever();

        builder.Property(application => application.GrantId)
            .HasConversion(CreatorValueConverters.Id<CommunityGrantId>())
            .IsRequired();

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

        builder.Property(application => application.Currency)
            .HasMaxLength(CreatorPersistenceConstants.CurrencyLength);

        builder.Property(application => application.SubmitIdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.Property(application => application.FundingReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        // One live application per creator per grant, and the index that enforces it. A second row
        // would let one applicant be approved twice for the same money.
        builder.HasIndex(application => new { application.GrantId, application.CreatorId })
            .IsUnique()
            .HasDatabaseName("ux_grant_applications_creator");

        builder.HasIndex(application => application.SubmitIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_grant_applications_submit_key");

        // The reviewer's queue.
        builder.HasIndex(application => new { application.State, application.SubmittedAt })
            .HasDatabaseName("ix_grant_applications_queue");

        // The applicant's own list.
        builder.HasIndex(application => new { application.CreatorId, application.State })
            .HasDatabaseName("ix_grant_applications_creator_state");

        builder.OwnsMany(application => application.Answers, answer =>
        {
            answer.ToTable(
                CreatorPersistenceConstants.GrantAnswersTable, CreatorModuleMetadata.Schema);

            answer.HasKey("GrantApplicationId", nameof(GrantAnswer.FieldKey));

            answer.Property(value => value.FieldKey)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            answer.Property(value => value.Value)
                .HasMaxLength(CreatorLimits.MaxAnswerLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.Attachments, attachment =>
        {
            attachment.ToTable(
                CreatorPersistenceConstants.GrantAttachmentsTable, CreatorModuleMetadata.Schema);

            attachment.HasKey("GrantApplicationId", nameof(GrantAttachment.Reference));

            attachment.Property(value => value.Reference)
                .HasMaxLength(CreatorPersistenceConstants.ReferenceLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.AcceptedDeclarations, accepted =>
        {
            accepted.ToTable(
                CreatorPersistenceConstants.GrantAcceptedDeclarationsTable,
                CreatorModuleMetadata.Schema);

            accepted.HasKey("GrantApplicationId", nameof(GrantAcceptedDeclaration.Key));

            accepted.Property(value => value.Key)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.InformationRequests, request =>
        {
            request.ToTable(
                CreatorPersistenceConstants.GrantRequestsTable, CreatorModuleMetadata.Schema);

            request.HasKey(
                "GrantApplicationId",
                nameof(GrantInformationRequest.RequestedAt),
                nameof(GrantInformationRequest.Text));

            request.Property(value => value.Text)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();
        });

        builder.OwnsMany(application => application.Reports, report =>
        {
            report.ToTable(
                CreatorPersistenceConstants.GrantReportsTable, CreatorModuleMetadata.Schema);

            report.HasKey(value => value.ReportId);
            report.Property(value => value.ReportId).ValueGeneratedNever();

            report.Property(value => value.Title)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength)
                .IsRequired();

            report.Property(value => value.State)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            report.Property(value => value.Summary)
                .HasMaxLength(CreatorLimits.MaxAnswerLength);

            // The overdue sweep's work list.
            report.HasIndex(value => new { value.State, value.DueAt })
                .HasDatabaseName("ix_grant_application_reports_due");
        });

        builder.OwnsMany(application => application.Decisions, decision =>
        {
            decision.ToTable(
                CreatorPersistenceConstants.GrantDecisionsTable, CreatorModuleMetadata.Schema);

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

            decision.HasIndex(value => new { value.ActorUserId, value.OccurredAt })
                .HasDatabaseName("ix_grant_application_decisions_actor");
        });

        builder.Ignore(application => application.DomainEvents);
        builder.Ignore(application => application.CanWithdraw);
        builder.Ignore(application => application.CanResubmit);
    }
}
