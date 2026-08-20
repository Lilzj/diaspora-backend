using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps conversation references.
/// <para>
/// <b>One table, four identifiers and a handle — and no message column anywhere.</b> That absence
/// is the design: Marketplace does not own chat, and a schema with nowhere to put a message body is
/// a schema nobody can quietly start putting messages into.
/// </para>
/// </summary>
public sealed class ListingConversationConfiguration
    : IEntityTypeConfiguration<ListingConversation>
{
    public void Configure(EntityTypeBuilder<ListingConversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.ConversationsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingConversationId>());

        builder.Property(conversation => conversation.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(conversation => conversation.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(conversation => conversation.BuyerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(conversation => conversation.SellerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(conversation => conversation.Status)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(conversation => conversation.ExternalConversationId)
            .HasMaxLength(MarketplacePersistenceLimits.ExternalReference);

        builder.Property(conversation => conversation.Provider)
            .HasMaxLength(MarketplacePersistenceLimits.ExternalReference);

        builder.Property(conversation => conversation.OpeningNote)
            .HasMaxLength(MarketplaceLimits.MaxNoteLength);

        // Asking twice returns the same reference rather than opening a second conversation about
        // the same item between the same two people.
        builder.HasIndex(conversation =>
                new { conversation.ListingId, conversation.BuyerUserId })
            .IsUnique()
            .HasDatabaseName("ux_marketplace_conversations_listing_buyer");

        builder.HasIndex(conversation =>
                new { conversation.BuyerUserId, conversation.LastActivityAt })
            .HasDatabaseName("ix_marketplace_conversations_buyer");

        builder.HasIndex(conversation =>
                new { conversation.SellerUserId, conversation.LastActivityAt })
            .HasDatabaseName("ix_marketplace_conversations_seller");

        builder.Ignore(conversation => conversation.DomainEvents);
        builder.Ignore(conversation => conversation.IsOpen);
    }
}
