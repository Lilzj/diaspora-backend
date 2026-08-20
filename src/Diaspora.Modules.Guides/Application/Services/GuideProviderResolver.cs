using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Hydrates the provider cards a guide detail renders — B8 as the first consumer of B7's
/// published Integration contracts.
/// <para>
/// The guide stores only identifiers and editorial text; the provider's name, slug, badge and
/// logo are B7's answers at read time, so a provider that renames itself or loses its badge
/// is never misrepresented by an old guide. Batched through <c>FindManyAsync</c>, because a
/// guide may reference twenty providers and a page must not make twenty round trips.
/// </para>
/// <para>
/// Reads fail soft to an empty answer: a guide whose provider cards cannot be hydrated
/// renders with bare references, and a knowledge page taken down by a directory outage would
/// be a dependency inverted.
/// </para>
/// </summary>
public sealed class GuideProviderResolver
{
    private readonly IProviderDirectory _providers;
    private readonly ILogger<GuideProviderResolver> _logger;

    public GuideProviderResolver(
        IProviderDirectory providers, ILogger<GuideProviderResolver> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>Summaries for the referenced providers, keyed by identifier. Missing ones are absent.</summary>
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
                providerIds.Select(ProviderId.From).ToList(), cancellationToken);
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
    /// declining to validate is how a guide ends up recommending a ghost, so an outage refuses
    /// the attach and the author retries.
    /// </summary>
    public async Task<bool> ExistsAsync(
        Guid providerId, CancellationToken cancellationToken = default)
    {
        var found = await _providers.FindAsync(ProviderId.From(providerId), cancellationToken);

        return found is not null;
    }
}
