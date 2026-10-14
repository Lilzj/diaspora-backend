using Diaspora.Modules.CommunityProgramme.Domain.Announcements;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>Maps the poll, its options and the people named on an invitation-only one.</summary>
public sealed class CommunityPollConfiguration : IEntityTypeConfiguration<CommunityPoll>
{
    public void Configure(EntityTypeBuilder<CommunityPoll> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.PollsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(poll => poll.Id);

        builder.Property(poll => poll.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityPollId>())
            .ValueGeneratedNever();

        builder.Property(poll => poll.Revision).IsConcurrencyToken();

        builder.Property(poll => poll.CreatedByUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(poll => poll.Question)
            .HasMaxLength(CommunityProgrammeLimits.MaxSummaryLength)
            .IsRequired();

        builder.Property(poll => poll.Description)
            .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

        builder.Property(poll => poll.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(poll => poll.VotingType)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(poll => poll.Audience)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(poll => poll.CountryCode)
            .HasMaxLength(CommunityProgrammeLimits.MaxCountryLength);

        // The town a neighbourhood poll is about (14 Oct 2026). Nullable: only that audience sets it.
        builder.Property(poll => poll.CityName)
            .HasMaxLength(CommunityProgrammeLimits.MaxCityNameLength);

        builder.OwnsMany(poll => poll.Options, option =>
        {
            option.ToTable(
                CommunityProgrammePersistenceConstants.PollOptionsTable,
                CommunityProgrammeModuleMetadata.Schema);

            option.HasKey(value => value.Id);

            option.Property(value => value.Id)
                .HasConversion(CommunityProgrammeValueConverters.Id<PollOptionId>())
                .ValueGeneratedNever();

            option.Property(value => value.Label)
                .HasMaxLength(CommunityProgrammeLimits.MaxLabelLength)
                .IsRequired();

            option.Property(value => value.Description)
                .HasMaxLength(CommunityProgrammeLimits.MaxSummaryLength);
        });

        builder.OwnsMany(poll => poll.Invitees, invitee =>
        {
            invitee.ToTable(
                CommunityProgrammePersistenceConstants.PollInviteesTable,
                CommunityProgrammeModuleMetadata.Schema);

            invitee.HasKey(value => value.Id);
            invitee.Property(value => value.Id).ValueGeneratedNever();

            invitee.Property(value => value.UserId)
                .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
                .IsRequired();

            // "Am I invited to this poll" runs on every read of an invitation-only poll.
            invitee.HasIndex(value => value.UserId)
                .HasDatabaseName("ix_community_programme_poll_invitees_user");
        });

        // The poll list, by audience and state.
        builder.HasIndex(poll => new { poll.Status, poll.Audience, poll.CreatedAt })
            .HasDatabaseName("ix_community_programme_polls_discovery");

        builder.HasIndex(poll => new { poll.OrganizationId, poll.Status })
            .HasDatabaseName("ix_community_programme_polls_organization");

        // The lifecycle sweep's two work lists.
        builder.HasIndex(poll => new { poll.Status, poll.OpensAt })
            .HasDatabaseName("ix_community_programme_polls_opens");

        builder.HasIndex(poll => new { poll.Status, poll.ClosesAt })
            .HasDatabaseName("ix_community_programme_polls_closes");

        builder.Ignore(poll => poll.DomainEvents);
        builder.Ignore(poll => poll.AcceptsVotes);
        builder.Ignore(poll => poll.IsDiscoverable);
        builder.Ignore(poll => poll.ResultsAreVisible);
    }
}

/// <summary>
/// Maps the response and its selections.
/// <para>
/// <b>The unique index on (poll, fingerprint) is one-member-one-vote.</b> Not a check in a handler,
/// which two devices voting at the same instant would both pass — a constraint the database enforces,
/// so the second insert fails and the handler turns the violation into the 409 the register asks for.
/// </para>
/// </summary>
public sealed class PollResponseConfiguration : IEntityTypeConfiguration<PollResponse>
{
    public void Configure(EntityTypeBuilder<PollResponse> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.PollResponsesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(response => response.Id);

        builder.Property(response => response.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<PollResponseId>())
            .ValueGeneratedNever();

        builder.Property(response => response.Revision).IsConcurrencyToken();

        builder.Property(response => response.PollId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityPollId>())
            .IsRequired();

        // Nullable, structurally, on an anonymous poll. See PollResponse's summary.
        builder.Property(response => response.VoterUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(response => response.VoterFingerprint)
            .HasMaxLength(CommunityProgrammePersistenceConstants.FingerprintLength)
            .IsRequired();

        builder.Property(response => response.OpenResponse)
            .HasMaxLength(CommunityProgrammeLimits.MaxOpenResponseLength);

        builder.Property(response => response.IdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.OwnsMany(response => response.Selections, selection =>
        {
            selection.ToTable(
                CommunityProgrammePersistenceConstants.PollResponseSelectionsTable,
                CommunityProgrammeModuleMetadata.Schema);

            selection.HasKey(value => value.Id);
            selection.Property(value => value.Id).ValueGeneratedNever();

            selection.Property(value => value.OptionId)
                .HasConversion(CommunityProgrammeValueConverters.Id<PollOptionId>())
                .IsRequired();

            // The tally groups by this column. Without the index it is a scan of every selection
            // ever made, on the one query in the module that must stay in the database.
            selection.HasIndex(value => value.OptionId)
                .HasDatabaseName("ix_community_programme_poll_selections_option");
        });

        // One member, one vote. The whole rule, in one index.
        builder.HasIndex(response => new { response.PollId, response.VoterFingerprint })
            .IsUnique()
            .HasDatabaseName("ux_community_programme_poll_responses_voter");

        builder.HasIndex(response => new { response.PollId, response.RespondedAt })
            .HasDatabaseName("ix_community_programme_poll_responses_poll");

        // "How many polls have I answered" — one of the impact figures.
        builder.HasIndex(response => new { response.VoterUserId, response.RespondedAt })
            .HasDatabaseName("ix_community_programme_poll_responses_voter_history");

        builder.HasIndex(response => response.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_poll_responses_key");

        builder.Ignore(response => response.DomainEvents);
    }
}

/// <summary>Maps the announcement and its media references.</summary>
public sealed class CommunityAnnouncementConfiguration
    : IEntityTypeConfiguration<CommunityAnnouncement>
{
    public void Configure(EntityTypeBuilder<CommunityAnnouncement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.AnnouncementsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(announcement => announcement.Id);

        builder.Property(announcement => announcement.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityAnnouncementId>())
            .ValueGeneratedNever();

        builder.Property(announcement => announcement.Revision).IsConcurrencyToken();

        builder.Property(announcement => announcement.Scope)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(announcement => announcement.PublishedByUserId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(announcement => announcement.Title)
            .HasMaxLength(CommunityProgrammeLimits.MaxNameLength)
            .IsRequired();

        builder.Property(announcement => announcement.Body)
            .HasMaxLength(CommunityProgrammeLimits.MaxDescriptionLength);

        builder.Property(announcement => announcement.CountryCode)
            .HasMaxLength(CommunityProgrammeLimits.MaxCountryLength);

        builder.Property(announcement => announcement.IdempotencyKey)
            .HasMaxLength(CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        builder.OwnsMany(announcement => announcement.Media, media =>
        {
            media.ToTable(
                CommunityProgrammePersistenceConstants.AnnouncementMediaTable,
                CommunityProgrammeModuleMetadata.Schema);

            media.HasKey(value => value.Id);
            media.Property(value => value.Id).ValueGeneratedNever();
            media.Property(value => value.MediaId).IsRequired();
        });

        // The feed: a scope's announcements, pinned first, then most recent.
        builder.HasIndex(announcement =>
                new { announcement.Scope, announcement.ScopeId, announcement.PublishedAt })
            .HasDatabaseName("ix_community_programme_announcements_scope");

        builder.HasIndex(announcement =>
                new { announcement.OrganizationId, announcement.PublishedAt })
            .HasDatabaseName("ix_community_programme_announcements_organization");

        builder.HasIndex(announcement => announcement.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_community_programme_announcements_key");

        builder.Ignore(announcement => announcement.DomainEvents);
    }
}
