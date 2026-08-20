using Diaspora.Platform.Operations.Scalability;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Scalability;

/// <summary>
/// Builds asset URLs from the configured base, whether that base is the origin or a CDN.
/// <para>
/// <b>Switching to a CDN is a configuration change and nothing else</b>, and that is a dividend of a
/// decision B5 made for a different reason: media travels through this platform as a reference and
/// never as a URL, so every URL the platform emits is built at the edge of a response from a key. There
/// is no persisted URL anywhere to migrate.
/// </para>
/// <para>
/// <b>It does not sign.</b> A URL from this builder is guessable if the key is, so a caller must not
/// treat one as an access control — object storage permissions are what protect a private object. A
/// signing CDN adapter replaces this class and honours the lifetime argument that this one ignores.
/// </para>
/// </summary>
public sealed class OriginContentDeliveryUrlBuilder : IContentDeliveryUrlBuilder
{
    private readonly Uri? _base;

    public OriginContentDeliveryUrlBuilder(IOptions<TopologyOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.ContentDeliveryBaseUrl;

        if (!string.IsNullOrWhiteSpace(configured) &&
            Uri.TryCreate(configured, UriKind.Absolute, out var parsed))
        {
            _base = parsed;
        }
    }

    public string Provider => _base is null ? "origin" : "cdn";

    public Uri Build(string storageKey, TimeSpan? lifetime = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var relative = storageKey.TrimStart('/');

        // A relative URI when no base is configured. The host resolves it against the request, which is
        // what makes the same response correct behind a proxy, in a container and on a developer's
        // machine — none of which agree about what the origin's absolute address is.
        return _base is null
            ? new Uri(relative, UriKind.Relative)
            : new Uri(_base, relative);
    }

    /// <summary>
    /// Nothing to purge: an origin holds no copy.
    /// <para>
    /// Implemented as a no-op rather than as a throw, because every caller that replaces an object
    /// should call it unconditionally. A purge that had to be guarded by "is a CDN configured" is a
    /// purge somebody will forget on the day one is.
    /// </para>
    /// </summary>
    public Task PurgeAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
