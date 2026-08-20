using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>Maps the volunteer opportunity, its shifts and the skills it asks for.</summary>
public sealed class VolunteerOpportunityConfiguration
    : IEntityTypeConfiguration<VolunteerOpportunity>
{
    public void Configure(EntityTypeBuilder<VolunteerOpportunity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.OpportunitiesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(opportunity => opportunity.Id);

        builder.Property(opportunity => opportunity.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerOpportunityId>())
            .ValueGeneratedNever();

        builder.Property(opportunity => opportunity.Revision).IsConcurrencyToken();

        builder.Property(opportunity => opportunity.OrganizationId).IsRequired();

        builder.Property(opportunity => opportunity.CreatedByUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(opportunity => opportunity.Title)
            .HasMaxLength(CommunityProgrammeLimits.MaxNameLength)
            .IsRequired();

        builder.Property(opportunity => opportunity.Description)
            .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

        builder.Property(opportunity => opportunity.Cause)
            .HasMaxLength(CommunityProgrammeLimits.MaxLabelLength);

        builder.Property(opportunity => opportunity.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(opportunity => opportunity.CountryCode)
            .HasMaxLength(CommunityProgrammeLimits.MaxCountryLength);

        builder.Property(opportunity => opportunity.CityName)
            .HasMaxLength(CommunityProgrammeLimits.MaxNameLength);

        builder.Property(opportunity => opportunity.AreaName)
            .HasMaxLength(CommunityProgrammeLimits.MaxNameLength);

        builder.Property(opportunity => opportunity.AddressLine)
            .HasMaxLength(CommunityProgrammeLimits.MaxSummaryLength);

        builder.Property(opportunity => opportunity.IdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.OwnsMany(opportunity => opportunity.Shifts, shift =>
        {
            shift.ToTable(
                CommunityProgrammePersistenceConstants.ShiftsTable,
                CommunityProgrammeModuleMetadata.Schema);

            shift.HasKey(value => value.Id);

            shift.Property(value => value.Id)
                .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerShiftId>())
                .ValueGeneratedNever();

            shift.Property(value => value.Label)
                .HasMaxLength(CommunityProgrammeLimits.MaxLabelLength);

            shift.Property(value => value.StartsAt).IsRequired();
            shift.Property(value => value.EndsAt).IsRequired();

            shift.Ignore(value => value.IsFull);
        });

        // A backing-field navigation: the public surface is IReadOnlyList<string>, which EF cannot
        // map, so the collection is configured through the field it is projected from. EF finds a
        // backing field by name alone, and a mismatch surfaces only as "Collection is read-only" on
        // read-back — B11's lesson, applied here rather than rediscovered.
        builder.OwnsMany<OpportunitySkill>("_requiredSkills", skill =>
        {
            skill.ToTable(
                CommunityProgrammePersistenceConstants.OpportunitySkillsTable,
                CommunityProgrammeModuleMetadata.Schema);

            skill.HasKey(value => value.Id);
            skill.Property(value => value.Id).ValueGeneratedNever();

            skill.Property(value => value.Name)
                .HasMaxLength(CommunityProgrammeLimits.MaxSkillLength)
                .IsRequired();
        });

        // The discovery page: published opportunities in a place, most recent first.
        builder.HasIndex(opportunity =>
                new { opportunity.Status, opportunity.CountryCode, opportunity.CreatedAt })
            .HasDatabaseName("ix_community_programme_opportunities_discovery");

        // An organization's own list.
        builder.HasIndex(opportunity =>
                new { opportunity.OrganizationId, opportunity.Status })
            .HasDatabaseName("ix_community_programme_opportunities_organization");

        // The deadline sweep's work list.
        builder.HasIndex(opportunity => opportunity.ApplicationDeadline)
            .HasDatabaseName("ix_community_programme_opportunities_deadline");

        builder.HasIndex(opportunity => opportunity.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_opportunities_key");

        builder.Ignore(opportunity => opportunity.DomainEvents);
        builder.Ignore(opportunity => opportunity.RequiredSkills);
        builder.Ignore(opportunity => opportunity.IsFull);
        builder.Ignore(opportunity => opportunity.IsDiscoverable);
        builder.Ignore(opportunity => opportunity.AcceptsApplications);
    }
}

/// <summary>Maps the volunteer application and the answers it carries.</summary>
public sealed class VolunteerApplicationConfiguration
    : IEntityTypeConfiguration<VolunteerApplication>
{
    public void Configure(EntityTypeBuilder<VolunteerApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.ApplicationsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(application => application.Id);

        builder.Property(application => application.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerApplicationId>())
            .ValueGeneratedNever();

        builder.Property(application => application.Revision).IsConcurrencyToken();

        builder.Property(application => application.OpportunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerOpportunityId>())
            .IsRequired();

        builder.Property(application => application.OrganizationId).IsRequired();

        builder.Property(application => application.VolunteerUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(application => application.ShiftId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerShiftId>());

        builder.Property(application => application.DecidedByUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(application => application.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(application => application.DecisionReason)
            .HasMaxLength(CommunityProgrammeLimits.MaxReasonLength);

        builder.Property(application => application.IdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.OwnsMany(application => application.Answers, answer =>
        {
            answer.ToTable(
                CommunityProgrammePersistenceConstants.ApplicationAnswersTable,
                CommunityProgrammeModuleMetadata.Schema);

            answer.HasKey(value => value.Id);
            answer.Property(value => value.Id).ValueGeneratedNever();

            answer.Property(value => value.Question)
                .HasMaxLength(CommunityProgrammeLimits.MaxAnswerKeyLength)
                .IsRequired();

            answer.Property(value => value.Answer)
                .HasMaxLength(CommunityProgrammeLimits.MaxAnswerValueLength);
        });

        // One application per volunteer per opportunity. The same constraint the membership carries,
        // and for the same reason: two taps must not become two applications.
        builder.HasIndex(application =>
                new { application.OpportunityId, application.VolunteerUserId })
            .IsUnique()
            .HasDatabaseName("ux_community_programme_applications_volunteer");

        // The organizer's queue, and the waiting-list order the position is computed from.
        builder.HasIndex(application =>
                new { application.OpportunityId, application.Status, application.CreatedAt })
            .HasDatabaseName("ix_community_programme_applications_queue");

        builder.HasIndex(application =>
                new { application.VolunteerUserId, application.Status })
            .HasDatabaseName("ix_community_programme_applications_mine");

        builder.HasIndex(application =>
                new { application.OrganizationId, application.Status })
            .HasDatabaseName("ix_community_programme_applications_organization");

        builder.HasIndex(application => application.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_applications_key");

        builder.Ignore(application => application.DomainEvents);
        builder.Ignore(application => application.OccupiesPlace);
        builder.Ignore(application => application.IsWaiting);
        builder.Ignore(application => application.IsPending);
        builder.Ignore(application => application.IsTerminal);
    }
}

/// <summary>Maps the assignment. One row per approved volunteer per shift.</summary>
public sealed class VolunteerAssignmentConfiguration
    : IEntityTypeConfiguration<VolunteerAssignment>
{
    public void Configure(EntityTypeBuilder<VolunteerAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.AssignmentsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerAssignmentId>())
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.Revision).IsConcurrencyToken();

        builder.Property(assignment => assignment.ApplicationId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerApplicationId>())
            .IsRequired();

        builder.Property(assignment => assignment.OpportunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerOpportunityId>())
            .IsRequired();

        builder.Property(assignment => assignment.OrganizationId).IsRequired();

        builder.Property(assignment => assignment.VolunteerUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(assignment => assignment.ShiftId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerShiftId>());

        builder.Property(assignment => assignment.ConfirmedByUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(assignment => assignment.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        // Money-like precision on hours, because 2.5 hours is a real answer and a float would make
        // a sum of a hundred of them wrong in the last place.
        builder.Property(assignment => assignment.ConfirmedHours)
            .HasPrecision(6, 2);

        // One assignment per application. Assigning twice is an organizer pressing the button twice.
        builder.HasIndex(assignment => assignment.ApplicationId)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_assignments_application");

        builder.HasIndex(assignment =>
                new { assignment.VolunteerUserId, assignment.Status })
            .HasDatabaseName("ix_community_programme_assignments_volunteer");

        builder.HasIndex(assignment =>
                new { assignment.OpportunityId, assignment.Status })
            .HasDatabaseName("ix_community_programme_assignments_opportunity");

        // The confirmed-hours reads: a member's own total, and an organization's series.
        builder.HasIndex(assignment =>
                new { assignment.OrganizationId, assignment.ConfirmedAt })
            .HasDatabaseName("ix_community_programme_assignments_confirmed");

        builder.Ignore(assignment => assignment.DomainEvents);
        builder.Ignore(assignment => assignment.IsTerminal);
        builder.Ignore(assignment => assignment.OccupiesPlace);
    }
}

/// <summary>Maps the issued certificate.</summary>
public sealed class VolunteerCertificateConfiguration
    : IEntityTypeConfiguration<VolunteerCertificate>
{
    public void Configure(EntityTypeBuilder<VolunteerCertificate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CertificatesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(certificate => certificate.Id);

        builder.Property(certificate => certificate.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerCertificateId>())
            .ValueGeneratedNever();

        builder.Property(certificate => certificate.VolunteerUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(certificate => certificate.OrganizationId).IsRequired();

        builder.Property(certificate => certificate.OpportunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerOpportunityId>())
            .IsRequired();

        builder.Property(certificate => certificate.AssignmentId)
            .HasConversion(CommunityProgrammeValueConverters.Id<VolunteerAssignmentId>())
            .IsRequired();

        builder.Property(certificate => certificate.Title)
            .HasMaxLength(CommunityProgrammeLimits.MaxNameLength)
            .IsRequired();

        builder.Property(certificate => certificate.Hours).HasPrecision(6, 2);

        // One certificate per assignment. Confirming hours twice must not issue two.
        builder.HasIndex(certificate => certificate.AssignmentId)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_certificates_assignment");

        builder.HasIndex(certificate =>
                new { certificate.VolunteerUserId, certificate.IssuedAt })
            .HasDatabaseName("ix_community_programme_certificates_volunteer");

        builder.Ignore(certificate => certificate.DomainEvents);
    }
}
