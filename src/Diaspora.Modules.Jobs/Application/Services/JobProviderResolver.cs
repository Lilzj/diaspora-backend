using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Hydrates the provider cards a job detail renders — the recruitment agency behind a posting,
/// the immigration consultant it recommends, the CV service or career coach it offers.
/// <para>
/// The posting stores only identifiers and editorial text; the provider's name, badge, rating and
/// logo are B7's answers at read time, so an agency that renames itself or loses its verification
/// is never misrepresented by a posting written six months ago. Batched through
/// <c>FindManyAsync</c>, because a posting may reference ten providers and a page must not make
/// ten round trips.
/// </para>
/// <para>
/// Reads fail soft to an empty answer: a posting whose provider cards cannot be hydrated renders
/// with bare references, and a job advert taken down by a directory outage would be a dependency
/// inverted.
/// </para>
/// </summary>
public sealed class JobProviderResolver
{
    private readonly IProviderDirectory _providers;
    private readonly ILogger<JobProviderResolver> _logger;

    public JobProviderResolver(
        IProviderDirectory providers, ILogger<JobProviderResolver> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>Summaries for the referenced providers, keyed by identifier. Missing ones absent.</summary>
    public async Task<IReadOnlyDictionary<Guid, ProviderSummary>> ResolveAsync(
        IReadOnlyCollection<Guid> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, ProviderSummary>();
        }

        try
        {
            return await _providers.FindManyAsync(
                [.. providerIds.Select(ProviderId.From)], cancellationToken);
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
    /// Whether the provider exists, for validating an attach. This one does not fail soft —
    /// declining to validate is how a posting ends up recommending a ghost, so an outage refuses
    /// the attach and the employer retries.
    /// </summary>
    public async Task<bool> ExistsAsync(
        Guid providerId, CancellationToken cancellationToken = default) =>
        await _providers.FindAsync(ProviderId.From(providerId), cancellationToken) is not null;
}
