namespace Diaspora.Platform.Messaging.Outbox.Abstractions;

/// <summary>
/// Decides which tenant a message belongs to, and which tenants this worker may drain.
/// <para>
/// <b>A seam, not a tenancy implementation.</b> The platform is single-tenant today and this
/// phase does not change that — inventing a tenant model the product has not asked for would be
/// scope creep with a schema attached. What it does do is put the column, the index and the
/// filter in place now, because retrofitting a discriminator onto an outbox table already
/// holding millions of rows is a migration with downtime, and the cost of carrying a nullable
/// column that is always null is a few bytes a row.
/// </para>
/// <para>
/// The default implementation returns null for everything and drains all messages, which is
/// exactly single-tenant behaviour. A future tenancy module replaces this one registration and
/// gets tenant-scoped writes, tenant-filtered dispatch and per-tenant statistics without any
/// other component changing — including database-per-tenant, where each tenant's host resolves
/// its own connection and its own outbox rows never leave its own database.
/// </para>
/// </summary>
public interface IOutboxTenantResolver
{
    /// <summary>
    /// The tenant owning the event being enqueued, taken from the current request context.
    /// Null in a single-tenant deployment.
    /// </summary>
    string? ResolveForWrite();

    /// <summary>
    /// The tenants this dispatcher instance may claim messages for.
    /// <para>
    /// An empty result means "no filter" — drain everything. A non-empty result restricts the
    /// claim query, which is how a deployment pins a noisy tenant to dedicated workers, or how
    /// a shard drains only its own slice.
    /// </para>
    /// </summary>
    IReadOnlyCollection<string> ResolveForDispatch();
}
