using Diaspora.Modules.Community.Domain.Members;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps Community's member projection.
/// <para>
/// <b>Keyed on the member.</b> There is no surrogate identifier because there is nothing to
/// identify beyond the member themselves, and a surrogate would make it possible to hold the same
/// person twice.
/// </para>
/// <para>
/// <b>No audit columns, and that is a choice.</b> This table holds no member decision — it is a
/// copy of a fact Identity already owns and audits. Auditing it would record the outbox as the
/// actor for every row.
/// </para>
/// </summary>
public sealed class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    public void Configure(EntityTypeBuilder<CommunityMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CommunityPersistenceConstants.MembersTable, CommunityModuleMetadata.Schema);

        builder.HasKey(member => member.UserId);

        builder.Property(member => member.UserId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .ValueGeneratedNever();

        builder.Property(member => member.DisplayName)
            .HasMaxLength(CommunityPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(member => member.CountryCode)
            .HasMaxLength(CommunityPersistenceConstants.CountryCodeLength);

        builder.Property(member => member.RegisteredAt)
            .IsRequired();
    }
}
