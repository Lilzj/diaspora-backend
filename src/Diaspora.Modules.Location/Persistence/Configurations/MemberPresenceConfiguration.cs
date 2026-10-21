using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the presence setting — one row per member, and no column that could hold a coordinate.
/// <para>
/// The absence is the design: the table has an area identifier and timestamps, so nothing written
/// here can become a location history however it is used later.
/// </para>
/// </summary>
public sealed class MemberPresenceConfiguration : IEntityTypeConfiguration<MemberPresence>
{
    public void Configure(EntityTypeBuilder<MemberPresence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.MemberPresenceTable, LocationModuleMetadata.Schema);

        builder.HasKey(presence => presence.Id);

        builder.Property(presence => presence.Id)
            .HasConversion(LocationValueConverters.Id<MemberPresenceId>());

        builder.Property(presence => presence.UserId)
            .HasConversion(LocationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(presence => presence.IsEnabled).IsRequired();
        builder.Property(presence => presence.EnabledAt);
        builder.Property(presence => presence.LastAreaId);
        builder.Property(presence => presence.LastEvaluatedAt);
        builder.Property(presence => presence.ModifiedAt).IsRequired();

        builder.HasIndex(presence => presence.UserId)
            .IsUnique()
            .HasDatabaseName("ix_member_presence_user");

        builder.Ignore(presence => presence.IsBaseline);
        builder.Ignore(presence => presence.DomainEvents);
    }
}
