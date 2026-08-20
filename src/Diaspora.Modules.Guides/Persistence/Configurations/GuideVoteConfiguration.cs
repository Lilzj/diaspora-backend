using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Votes;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps helpfulness votes.
/// <para>
/// <b>The unique index on the denormalised voter key is the invariant.</b> "One vote per member
/// per guide" wants an index over the guide and the voter — but both are value-converted
/// identifiers, and an EF index cannot span what EF cannot compare. The aggregate derives
/// <c>VoterKey</c> deterministically from the pair, the database enforces uniqueness on it, and
/// a double-tap on a slow connection dies here rather than double-counting. B6's lesson,
/// applied as designed.
/// </para>
/// </summary>
public sealed class GuideVoteConfiguration : IEntityTypeConfiguration<GuideVote>
{
    public void Configure(EntityTypeBuilder<GuideVote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(GuidePersistenceConstants.GuideVotesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(vote => vote.Id);

        builder.Property(vote => vote.Id)
            .HasConversion(GuideValueConverters.Id<GuideVoteId>());

        builder.Property(vote => vote.GuideId)
            .HasConversion(GuideValueConverters.Id<GuideId>())
            .IsRequired();

        builder.Property(vote => vote.VoterId)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(vote => vote.Kind)
            .HasConversion<string>()
            .HasMaxLength(GuidePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(vote => vote.VoterKey)
            .HasMaxLength(GuidePersistenceConstants.VoterKeyLength)
            .IsRequired();

        builder.HasIndex(vote => vote.VoterKey)
            .IsUnique()
            .HasDatabaseName("ux_guide_votes_voter_key");

        builder.Ignore(vote => vote.DomainEvents);
    }
}
