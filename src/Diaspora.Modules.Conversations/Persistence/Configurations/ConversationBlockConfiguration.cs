using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Conversations.Persistence.Configurations;

/// <summary>The block list. Two columns, two indexes, and both indexes matter.</summary>
public sealed class ConversationBlockConfiguration : IEntityTypeConfiguration<ConversationBlock>
{
    public void Configure(EntityTypeBuilder<ConversationBlock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ConversationPersistenceConstants.BlocksTable, ConversationsModuleMetadata.Schema);

        builder.HasKey(block => block.Id);

        builder.Property(block => block.Id)
            .HasConversion(ConversationValueConverters.Id<ConversationBlockId>())
            .ValueGeneratedNever();

        builder.Property(block => block.BlockerUserId)
            .HasConversion(ConversationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(block => block.BlockedUserId)
            .HasConversion(ConversationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(block => block.Reason)
            .HasMaxLength(ConversationLimits.MaxBlockReasonLength);

        // Blocking twice is a member pressing the button twice, not a second block.
        builder.HasIndex(block => new { block.BlockerUserId, block.BlockedUserId })
            .IsUnique()
            .HasDatabaseName("ux_conversation_blocks_pair");

        // The reverse direction, and it is not redundant. "May A message B" has to find a row where
        // B blocked A, which the index above cannot serve — and that lookup runs on every send.
        builder.HasIndex(block => block.BlockedUserId)
            .HasDatabaseName("ix_conversation_blocks_blocked");

        builder.Ignore(block => block.DomainEvents);
    }
}
