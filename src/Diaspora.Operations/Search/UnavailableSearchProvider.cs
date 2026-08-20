using Diaspora.Platform.Operations.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Search;

/// <summary>
/// The provider that ships: one that honestly reports it cannot serve.
/// <para>
/// <b>Not a null object that pretends to work.</b> <see cref="IsAvailableAsync"/> returns false, so
/// every caller takes its own fallback path — the database-backed engine each module already ships —
/// and search behaves exactly as it did before B17. A provider that returned an empty result set
/// instead would look like "nothing matched", and the platform would report zero results for every
/// search on the day someone forgot to register a real one.
/// </para>
/// <para>
/// Its other job is to make the gap visible. The operational dashboard reads the provider name, so
/// <c>none</c> appears beside every index rather than the absence of a search section.
/// </para>
/// </summary>
public sealed class UnavailableSearchProvider : ISearchProvider, ISearchIndexer
{
    private readonly ILogger<UnavailableSearchProvider> _logger;

    public UnavailableSearchProvider(ILogger<UnavailableSearchProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public string Name => "none";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Debug rather than warning. A caller that checked IsAvailableAsync first and fell back
        // correctly should not fill the log; a caller that did not is a bug this line helps find.
        _logger.LogDebug(
            "A search reached the platform provider for index {Index} with no engine registered. " +
            "The caller should have checked IsAvailableAsync and used its own engine.",
            query.Index);

        return Task.FromResult(SearchResult.Empty);
    }

    public Task<SearchIndexState> DescribeAsync(string index, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(index);

        return Task.FromResult(new SearchIndexState(index, 0, null, 0, IsAvailable: false));
    }

    public Task EnsureIndexAsync(SearchIndexDefinition definition, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task IndexAsync(IReadOnlyList<SearchDocument> documents, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteAsync(string index, IReadOnlyList<string> ids, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
