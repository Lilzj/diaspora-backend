using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Content;

/// <summary>
/// Resolves the module that owns a content type.
/// <para>
/// Scoped, not singleton: a source will hold a repository, and a singleton registry over scoped
/// sources is the captive dependency the host's provider validation refuses to start on — the
/// same trap B4 recorded with its country metadata catalogue.
/// </para>
/// </summary>
public sealed class CommunityContentSourceRegistry
{
    private readonly IReadOnlyDictionary<CommunityContentType, ICommunityContentSource> _sources;

    public CommunityContentSourceRegistry(IEnumerable<ICommunityContentSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var byType = new Dictionary<CommunityContentType, ICommunityContentSource>();

        foreach (var source in sources)
        {
            if (byType.TryGetValue(source.ContentType, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two content sources claim '{source.ContentType}': " +
                    $"{existing.GetType().FullName} and {source.GetType().FullName}. " +
                    "A content type is owned by exactly one module.");
            }

            byType[source.ContentType] = source;
        }

        _sources = byType;
    }

    public IReadOnlyList<CommunityContentType> ClaimedTypes =>
        _sources.Keys.OrderBy(type => (int)type).ToList();

    public ICommunityContentSource? Find(CommunityContentType contentType) =>
        _sources.GetValueOrDefault(contentType);

    /// <summary>Describes one item through whichever module owns its type.</summary>
    public async Task<ContentDescriptor?> DescribeAsync(
        ContentReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var source = Find(reference.Type);

        return source is null
            ? null
            : await source.DescribeAsync(reference.Id, cancellationToken);
    }

    /// <summary>
    /// Describes a mixed set of references, one round trip per owning module.
    /// <para>
    /// The grouping is the point: a saved screen holding guides, jobs, events and posts resolves
    /// in four calls rather than in one per card.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<ContentReference> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(references);

        var results = new Dictionary<string, ContentDescriptor>(StringComparer.Ordinal);

        foreach (var group in references.GroupBy(reference => reference.Type))
        {
            var source = Find(group.Key);

            if (source is null)
            {
                continue;
            }

            var ids = group.Select(reference => reference.Id).Distinct().ToList();
            var described = await source.DescribeManyAsync(ids, cancellationToken);

            foreach (var (id, descriptor) in described)
            {
                results[ContentReference.Create(group.Key, id).Key] = descriptor;
            }
        }

        return results;
    }
}
