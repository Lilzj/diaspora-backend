using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Extensibility.Knowledge;

/// <summary>
/// Resolves the section providers registered on this deployment.
/// <para>
/// <b>Last registration wins per key.</b> A deployment replacing the shipped "most helpful" shelf
/// with one that ranks differently registers after B8.1 and gets the key; it does not get a second
/// shelf with the same heading. That is the same last-wins discipline B8 applies to its search
/// engine, applied per key rather than wholesale, because sections are a set and an engine is a
/// singleton.
/// </para>
/// <para>
/// <b>A provider that throws is dropped, not propagated.</b> A category page composed of seven
/// independent shelves must not become a 500 because a contributed eighth one has a bug — the
/// reader wants the six that worked. The failure is logged at error, which is where an operator
/// will find it, and the page renders.
/// </para>
/// <para>
/// Scoped, like every provider it holds. See the seam interface for why.
/// </para>
/// </summary>
public sealed class KnowledgeSectionProviderRegistry
{
    private readonly IReadOnlyList<IKnowledgeSectionProvider> _providers;
    private readonly ILogger<KnowledgeSectionProviderRegistry> _logger;

    public KnowledgeSectionProviderRegistry(
        IEnumerable<IKnowledgeSectionProvider> providers,
        ILogger<KnowledgeSectionProviderRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _logger = logger;

        // Grouped by key with the last registration kept, then ordered. Materialised once per
        // scope rather than per call, because a country dashboard asks for sections repeatedly.
        _providers =
        [
            .. providers
                .GroupBy(provider => provider.Key, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(provider => provider.Order)
                .ThenBy(provider => provider.Key, StringComparer.Ordinal)
        ];
    }

    /// <summary>Every registered section key, in render order. What the capabilities endpoint reports.</summary>
    public IReadOnlyList<string> Keys => [.. _providers.Select(provider => provider.Key)];

    /// <summary>
    /// Builds every applicable section for one request, in render order.
    /// <para>
    /// Sequential rather than concurrent, deliberately. Every provider shares the request's
    /// <c>DbContext</c>, and EF Core does not permit two operations on one context at once — a
    /// <c>Task.WhenAll</c> here would be an intermittent "a second operation was started" that only
    /// appears when two shelves happen to overlap. The shelves are small and indexed; the win would
    /// have been latency measured in single-digit milliseconds and the cost is a class of bug that
    /// reproduces once a week.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeSectionContent>> BuildAsync(
        KnowledgeSectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = new List<KnowledgeSectionContent>(_providers.Count);

        foreach (var provider in _providers)
        {
            if (!provider.AppliesTo(request))
            {
                continue;
            }

            var content = await BuildOneAsync(provider, request, cancellationToken);

            if (content is { GuideIds.Count: > 0 })
            {
                sections.Add(content with { Order = provider.Order });
            }
        }

        return sections;
    }

    private async Task<KnowledgeSectionContent?> BuildOneAsync(
        IKnowledgeSectionProvider provider,
        KnowledgeSectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.BuildAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // One broken shelf must not fail the page. See the class summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Knowledge section provider {Section} failed for {CountryCode}/{Category}.",
                provider.Key,
                request.CountryCode,
                request.KnowledgeCategoryKey);

            return null;
        }
#pragma warning restore CA1031
    }
}
