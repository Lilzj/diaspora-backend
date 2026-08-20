using Diaspora.Modules.Providers.Domain.Identifiers;

namespace Diaspora.Modules.Providers.Integration;

/// <summary>
/// How a search or AI module gets provider documents out of B7.
/// <para>
/// Two access shapes, and both are needed for different reasons.
/// <see cref="StreamAsync"/> is the initial build and the periodic reconciliation: everything,
/// in batches, cursor-paged so a million providers do not become a million-row result set.
/// <see cref="ProjectAsync"/> is the incremental path: B7 publishes an integration event, the
/// consumer projects the one provider that changed, and the index stays current without a sweep.
/// </para>
/// <para>
/// The same shape B6 published for its own content, deliberately — a search module that has
/// already written an ingester against <c>ICommunitySearchProjectionSource</c> should recognise
/// this one immediately rather than learning a second idiom.
/// </para>
/// </summary>
public interface IProviderSearchProjectionSource
{
    /// <summary>One provider's document, or null when it does not exist or is not indexable.</summary>
    Task<ProviderSearchProjection?> ProjectAsync(
        ProviderId providerId, CancellationToken cancellationToken = default);

    /// <summary>Several documents at once.</summary>
    Task<IReadOnlyList<ProviderSearchProjection>> ProjectManyAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every indexable provider, in batches.
    /// <para>
    /// Cursor-paged rather than offset-paged, because an offset sweep over a table that is being
    /// written to skips and repeats rows — which in an index rebuild means providers that
    /// silently never get indexed at all.
    /// </para>
    /// </summary>
    IAsyncEnumerable<ProviderSearchProjection> StreamAsync(
        int batchSize = 200, CancellationToken cancellationToken = default);
}
