using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>
/// A community's initiatives — 21 Oct 2026.
/// <para>
/// Organisers are a list of user ids with no identity of their own, held as a primitive collection
/// as a membership's roles are. Nothing joins to one organiser, and the list is at most ten long.
/// </para>
/// </summary>
public sealed class CommunityInitiativeConfiguration : IEntityTypeConfiguration<CommunityInitiative>
{
    public void Configure(EntityTypeBuilder<CommunityInitiative> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityInitiativesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(initiative => initiative.Id);

        builder.Property(initiative => initiative.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityInitiativeId>())
            .ValueGeneratedNever();

        builder.Property(initiative => initiative.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(initiative => initiative.ProposedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(initiative => initiative.Title)
            .HasMaxLength(CommunityInitiative.TitleMaximumLength)
            .IsRequired();

        builder.Property(initiative => initiative.Description)
            .HasMaxLength(CommunityInitiative.DescriptionMaximumLength)
            .IsRequired();

        builder.Property(initiative => initiative.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.PrimitiveCollection(initiative => initiative.OrganiserIds);

        builder.Ignore(initiative => initiative.IsClosed);
        builder.Ignore(initiative => initiative.AcceptsParticipants);
        builder.Ignore(initiative => initiative.AcceptsUpdates);

        builder.HasIndex(initiative => new { initiative.CommunityId, initiative.CreatedAt })
            .HasDatabaseName("ix_community_initiatives_community_created");

        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(initiative => initiative.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// One member taking part in an initiative — 21 Oct 2026. Once per member per initiative, and the
/// database says so, so a double tap on Join cannot write two rows.
/// </summary>
public sealed class CommunityInitiativeParticipantConfiguration
    : IEntityTypeConfiguration<CommunityInitiativeParticipant>
{
    public void Configure(EntityTypeBuilder<CommunityInitiativeParticipant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityInitiativeParticipantsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(participant => participant.Id);

        builder.Property(participant => participant.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityInitiativeParticipantId>())
            .ValueGeneratedNever();

        builder.Property(participant => participant.InitiativeId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityInitiativeId>())
            .IsRequired();

        builder.Property(participant => participant.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(participant => participant.MemberId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(participant => participant.JoinedAt).IsRequired();

        builder.HasIndex(participant => new { participant.InitiativeId, participant.MemberId })
            .IsUnique()
            .HasDatabaseName("ux_community_initiative_participants_initiative_member");

        builder.HasOne<CommunityInitiative>()
            .WithMany()
            .HasForeignKey(participant => participant.InitiativeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>A progress update on an initiative — 21 Oct 2026. Read newest first, per initiative.</summary>
public sealed class CommunityInitiativeUpdateConfiguration : IEntityTypeConfiguration<CommunityInitiativeUpdate>
{
    public void Configure(EntityTypeBuilder<CommunityInitiativeUpdate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityInitiativeUpdatesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(update => update.Id);

        builder.Property(update => update.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityInitiativeUpdateId>())
            .ValueGeneratedNever();

        builder.Property(update => update.InitiativeId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityInitiativeId>())
            .IsRequired();

        builder.Property(update => update.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(update => update.AuthorId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(update => update.Title)
            .HasMaxLength(CommunityInitiativeUpdate.TitleMaximumLength)
            .IsRequired();

        builder.Property(update => update.Body)
            .HasMaxLength(CommunityInitiativeUpdate.BodyMaximumLength)
            .IsRequired();

        builder.Property(update => update.PostedAt).IsRequired();

        builder.HasIndex(update => new { update.InitiativeId, update.PostedAt })
            .HasDatabaseName("ix_community_initiative_updates_initiative_posted");

        builder.HasOne<CommunityInitiative>()
            .WithMany()
            .HasForeignKey(update => update.InitiativeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
