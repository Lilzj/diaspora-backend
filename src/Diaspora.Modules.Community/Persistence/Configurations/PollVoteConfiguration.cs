using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps poll votes.
/// <para>
/// Unique on poll, option and voter rather than on poll and voter, because a multi-choice poll
/// takes several votes from one member. One-per-poll on a single-choice poll is enforced by the
/// handler, which replaces rather than refuses.
/// </para>
/// </summary>
public sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.PollVotesTable, CommunityModuleMetadata.Schema);

        builder.HasKey(vote => vote.Id);

        builder.Property(vote => vote.Id)
            .HasConversion(CommunityValueConverters.Id<PollVoteId>());

        builder.Property(vote => vote.PollId)
            .HasConversion(CommunityValueConverters.Id<PollId>())
            .IsRequired();

        builder.Property(vote => vote.OptionId)
            .HasConversion(CommunityValueConverters.Id<PollOptionId>())
            .IsRequired();

        builder.Property(vote => vote.VoterId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.HasIndex(vote => new { vote.PollId, vote.OptionId, vote.VoterId })
            .IsUnique()
            .HasDatabaseName("ix_poll_votes_poll_option_voter");

        builder.HasIndex(vote => new { vote.PollId, vote.VoterId })
            .HasDatabaseName("ix_poll_votes_poll_voter");

        builder.Ignore(vote => vote.DomainEvents);
    }
}
