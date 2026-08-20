using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Hydrates the shop behind a storefront and the shops referenced from a listing.
/// <para>
/// <b>This is the brief's "provider-owned store", and it costs B7 nothing.</b> A seller profile
/// linked to a Providers shop stores that shop's identifier and no more; the name, badge, rating
/// and logo are B7's answers at read time, so a shop that renames itself or loses its verification
/// is never misrepresented by a listing written six months ago. Batched, because a results page may
/// carry twenty storefront listings and must not make twenty round trips.
/// </para>
/// <para>
/// Reads fail soft to an empty answer: a listing whose provider card cannot be hydrated renders
/// with bare references, and a marketplace taken down by a directory outage would be a dependency
/// inverted.
/// </para>
/// </summary>
public sealed class MarketplaceProviderResolver
{
    private readonly IProviderDirectory _providers;
    private readonly ILogger<MarketplaceProviderResolver> _logger;

    public MarketplaceProviderResolver(
        IProviderDirectory providers, ILogger<MarketplaceProviderResolver> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>Summaries for the referenced shops, keyed by identifier. Missing ones absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, ProviderSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        var ids = providerIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ProviderSummary>();
        }

        try
        {
            return await _providers.FindManyAsync(
                [.. ids.Select(ProviderId.From)], cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A directory outage renders bare references, not a failed page.
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Hydrating provider references from B7 failed.");

            return new Dictionary<Guid, ProviderSummary>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Whether the shop exists, for validating a storefront link or a reference attach.
    /// <para>
    /// This one does not fail soft: declining to validate is how a listing ends up recommending a
    /// ghost, so an outage refuses the attach and the seller retries.
    /// </para>
    /// </summary>
    public async Task<bool> ExistsAsync(
        Guid providerId, CancellationToken cancellationToken = default) =>
        providerId != Guid.Empty
        && await _providers.FindAsync(ProviderId.From(providerId), cancellationToken) is not null;
}
