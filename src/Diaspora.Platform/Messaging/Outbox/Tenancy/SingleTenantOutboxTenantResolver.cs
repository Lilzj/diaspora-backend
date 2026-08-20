using Diaspora.Platform.Messaging.Outbox.Abstractions;

namespace Diaspora.Platform.Messaging.Outbox.Tenancy;

/// <summary>
/// The single-tenant default: no tenant on writes, no filter on dispatch.
/// <para>
/// Registered with <c>TryAdd</c>, so a future tenancy module replaces it with one line and
/// every write becomes tenant-stamped, every claim tenant-filtered and every statistic
/// tenant-scoped without another component changing. That is the whole of the platform's
/// multi-tenancy story for B1.1, and stating it as a replaceable default is more honest than
/// shipping a tenancy model nothing uses.
/// </para>
/// </summary>
public sealed class SingleTenantOutboxTenantResolver : IOutboxTenantResolver
{
    private static readonly string[] NoFilter = [];

    public string? ResolveForWrite() => null;

    public IReadOnlyCollection<string> ResolveForDispatch() => NoFilter;
}
