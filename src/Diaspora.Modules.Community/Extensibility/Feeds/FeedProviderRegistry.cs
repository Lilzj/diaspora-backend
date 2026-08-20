namespace Diaspora.Modules.Community.Extensibility.Feeds;

/// <summary>
/// Resolves a feed provider by key.
/// <para>
/// Two providers claiming one key fails startup. The alternative is that registration order
/// decides which feed a member sees, and registration order is assembly load order, which
/// differs between a test host and a published one.
/// </para>
/// </summary>
public sealed class FeedProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IFeedProvider> _providers;

    public FeedProviderRegistry(IEnumerable<IFeedProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var byKey = new Dictionary<string, IFeedProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var key = provider.FeedKey?.Trim();

            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    $"{provider.GetType().FullName} declares no feed key.");
            }

            if (byKey.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two feed providers claim '{key}': {existing.GetType().FullName} and " +
                    $"{provider.GetType().FullName}. A feed key is owned by exactly one provider.");
            }

            byKey[key] = provider;
        }

        _providers = byKey;
    }

    public IReadOnlyList<string> Keys =>
        _providers.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList();

    public int Count => _providers.Count;

    public IFeedProvider? Find(string? feedKey) =>
        string.IsNullOrWhiteSpace(feedKey) ? null : _providers.GetValueOrDefault(feedKey.Trim());
}
