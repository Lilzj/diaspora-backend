using Diaspora.Modules.Integrations.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Integrations.Persistence;

/// <summary>
/// Adds the Integration Foundation's tables to the shared context.
/// <para>
/// Six tables in the module's own <c>integrations</c> schema, which the context verifies at startup.
/// No owned type and no owned collection anywhere in the module: every list is a delimited column
/// and every value object is composed at read time from flat columns. That is a deliberate
/// narrowing rather than a stylistic one — this platform has four recorded defects in owned-type
/// mapping (shadow keys in a composite primary key, caller-supplied keys producing an UPDATE
/// instead of an INSERT, a cached owned instance NULLing a second owner's columns, and a read-only
/// collection getter forcing EF through a field it finds by name), and none of them is reachable
/// from a schema with no owned types in it.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>, and that is a decision.</b> A connection's
/// disconnection is a state on the row because the register requires disconnecting twice to answer
/// the same thing, and a soft-deleted row disappears behind the global query filter — which would
/// make the second disconnect a create. An authorization request is deleted outright on expiry
/// because a consumed state hash has no evidential value. A received callback is append-only, like
/// a ledger entry. A calendar link is deleted on disconnect because it exists only to deduplicate
/// against a grant that no longer exists.
/// </para>
/// </summary>
public sealed class IntegrationsDatabaseContributor : IDatabaseContributor
{
    public string Module => IntegrationsModuleMetadata.Name;

    public string Schema => IntegrationsModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The registry, and the credential custody behind it.
        modelBuilder.ApplyConfiguration(new IntegrationConnectionConfiguration());

        // The single-use record that makes an OAuth callback verifiable.
        modelBuilder.ApplyConfiguration(new OAuthAuthorizationRequestConfiguration());

        // The duplicate-suppression mapping, and the member's preferences kept apart from it so a
        // disconnect does not lose them.
        modelBuilder.ApplyConfiguration(new CalendarEventLinkConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationCalendarSettingsConfiguration());

        // Synchronisation, and the replay barrier for inbound callbacks.
        modelBuilder.ApplyConfiguration(new IntegrationSyncRunConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedIntegrationWebhookConfiguration());
    }
}
