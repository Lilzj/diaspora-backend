using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>
/// A community's rules — 21 Oct 2026.
/// <para>
/// Indexed on the community and position together, because the only read is "this community's rules,
/// in order". Not unique: renumbering after a move writes several rows in one save, and a unique index
/// would refuse the intermediate state on a provider that checks constraints per statement.
/// </para>
/// </summary>
public sealed class CommunityRuleConfiguration : IEntityTypeConfiguration<CommunityRule>
{
    public void Configure(EntityTypeBuilder<CommunityRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityRulesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityRuleId>())
            .ValueGeneratedNever();

        builder.Property(rule => rule.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(rule => rule.Position).IsRequired();

        builder.Property(rule => rule.Title)
            .HasMaxLength(CommunityRule.TitleMaximumLength)
            .IsRequired();

        builder.Property(rule => rule.Body)
            .HasMaxLength(CommunityRule.BodyMaximumLength)
            .IsRequired();

        builder.Property(rule => rule.Consequence)
            .HasMaxLength(CommunityRule.ConsequenceMaximumLength);

        builder.HasIndex(rule => new { rule.CommunityId, rule.Position })
            .HasDatabaseName("ix_community_rules_community_position");

        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(rule => rule.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// A community's notices — 21 Oct 2026.
/// <para>
/// Indexed for the one list there is: one community's notices, newest first, with lapsed ones
/// filtered on <c>ExpiresAt</c>.
/// </para>
/// </summary>
public sealed class CommunityNoticeConfiguration : IEntityTypeConfiguration<CommunityNotice>
{
    public void Configure(EntityTypeBuilder<CommunityNotice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityNoticesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(notice => notice.Id);

        builder.Property(notice => notice.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityNoticeId>())
            .ValueGeneratedNever();

        builder.Property(notice => notice.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(notice => notice.PublishedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(notice => notice.PublishedByRole)
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(notice => notice.Title)
            .HasMaxLength(CommunityNotice.TitleMaximumLength)
            .IsRequired();

        builder.Property(notice => notice.Body)
            .HasMaxLength(CommunityNotice.BodyMaximumLength)
            .IsRequired();

        builder.Property(notice => notice.PublishedAt).IsRequired();
        builder.Property(notice => notice.IsPinned).IsRequired();

        builder.HasIndex(notice => new { notice.CommunityId, notice.PublishedAt })
            .HasDatabaseName("ix_community_notices_community_published");

        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(notice => notice.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// A member's community preferences — 21 Oct 2026. One row per member, and the database says so.
/// </summary>
public sealed class CommunityMemberPreferencesConfiguration
    : IEntityTypeConfiguration<CommunityMemberPreferences>
{
    public void Configure(EntityTypeBuilder<CommunityMemberPreferences> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityMemberPreferencesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(preferences => preferences.Id);

        builder.Property(preferences => preferences.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityMemberPreferencesId>())
            .ValueGeneratedNever();

        builder.Property(preferences => preferences.MemberId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        // Stored as its name, as every enum in this module is. Private is the zero, so a row written
        // without this column reads as private rather than as consent to publish.
        builder.Property(preferences => preferences.DefaultMembershipVisibility)
            .HasConversion<string>()
            .HasMaxLength(CommunityProgrammePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(preferences => preferences.PreferredContentLanguage)
            .HasMaxLength(CommunityMemberPreferences.LanguageMaximumLength);

        builder.Property(preferences => preferences.NotificationsJson).IsRequired();

        builder.Property(preferences => preferences.Revision).IsConcurrencyToken();

        builder.Ignore(preferences => preferences.Notifications);

        builder.HasIndex(preferences => preferences.MemberId)
            .IsUnique()
            .HasDatabaseName("ux_community_member_preferences_member");
    }
}
