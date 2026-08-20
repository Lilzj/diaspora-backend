using Diaspora.Modules.Conversations.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Conversations.Persistence.Configurations;

/// <summary>
/// The context type catalogue.
/// <para>
/// Keyed by the string itself rather than by a surrogate, because the key is what conversations
/// store and what the client sends — a surrogate would mean a join to render a filter chip.
/// </para>
/// </summary>
public sealed class ConversationContextTypeConfiguration
    : IEntityTypeConfiguration<ConversationContextType>
{
    public void Configure(EntityTypeBuilder<ConversationContextType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ConversationPersistenceConstants.ContextTypesTable, ConversationsModuleMetadata.Schema);

        builder.HasKey(type => type.Id);

        builder.Property(type => type.Id)
            .HasColumnName("key")
            .HasMaxLength(ConversationContextType.MaxKeyLength)
            .ValueGeneratedNever();

        builder.Property(type => type.DisplayName)
            .HasMaxLength(ConversationContextType.MaxDisplayNameLength)
            .IsRequired();

        builder.Property(type => type.Description)
            .HasMaxLength(ConversationContextType.MaxDescriptionLength);

        builder.Property(type => type.OwningModule)
            .HasMaxLength(ConversationContextType.MaxModuleLength);

        builder.HasIndex(type => new { type.IsActive, type.Position })
            .HasDatabaseName("ix_conversation_context_types_order");

        builder.Ignore(type => type.DomainEvents);
        builder.Ignore(type => type.Key);
    }
}
