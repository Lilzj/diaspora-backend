using Diaspora.Modules.Conversations.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Conversations.Persistence;

/// <summary>The module's five tables, in its own schema.</summary>
public sealed class ConversationsDatabaseContributor : IDatabaseContributor
{
    public string Module => ConversationsModuleMetadata.Name;

    public string Schema => ConversationsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The thread, its frozen context, its provenance and the people in it.
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());

        // What was said. Its own aggregate, and the only unbounded table here.
        modelBuilder.ApplyConfiguration(new ConversationMessageConfiguration());

        // One member's decision about another. Outlives every conversation between them.
        modelBuilder.ApplyConfiguration(new ConversationBlockConfiguration());

        // Reference data, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new ConversationContextTypeConfiguration());
    }
}
