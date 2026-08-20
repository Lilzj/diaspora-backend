using System.Globalization;
using System.Text;

namespace Diaspora.Platform.Operations.Caching;

/// <summary>
/// A structured cache key.
/// <para>
/// Structured rather than a bare string because the two operations a cache is actually asked for
/// in an incident — "drop everything about this member" and "drop everything in this family" —
/// are impossible over opaque strings. A key that knows its own namespace can be dropped by
/// namespace; a key that is <c>"feed_" + userId</c> can only be dropped by guessing.
/// </para>
/// <para>
/// <b>The version segment is what makes prefix invalidation cheap.</b> Dropping a namespace does
/// not enumerate and delete its keys — a shared store with a million keys cannot be scanned during
/// an incident — it increments the namespace's version, which changes every storage key in that
/// family at once and leaves the old entries to expire on their own. That is the standard trick
/// and it is the only one that works at size.
/// </para>
/// </summary>
public readonly record struct CacheKey
{
    /// <summary>Separates the segments of a storage key. Chosen to match the convention shared stores use.</summary>
    public const char Separator = ':';

    private CacheKey(string ns, string identifier)
    {
        Namespace = ns;
        Identifier = identifier;
    }

    /// <summary>
    /// The family this key belongs to — <c>recommendations.feed</c>, <c>guides.catalog</c>. Dotted,
    /// lower case, and namespaced to the module that owns the data, exactly like every other
    /// catalogue name on the platform.
    /// </summary>
    public string Namespace { get; }

    /// <summary>What distinguishes this entry inside its namespace. Never blank.</summary>
    public string Identifier { get; }

    /// <summary>True for the default instance, which no cache operation accepts.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Namespace);

    /// <summary>
    /// Builds a key from a namespace and any number of parts.
    /// <para>
    /// A null part becomes <c>~</c> rather than being skipped. Skipping would make
    /// <c>For("x", a, null, b)</c> and <c>For("x", a, b)</c> the same key, which is how a cache
    /// starts answering one question with another question's answer.
    /// </para>
    /// </summary>
    public static CacheKey For(string cacheNamespace, params ReadOnlySpan<string?> parts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheNamespace);

        if (parts.Length == 0)
        {
            return new CacheKey(cacheNamespace, "_");
        }

        var builder = new StringBuilder();

        for (var index = 0; index < parts.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(Separator);
            }

            var part = parts[index];
            builder.Append(string.IsNullOrEmpty(part) ? "~" : Sanitize(part));
        }

        return new CacheKey(cacheNamespace, builder.ToString());
    }

    /// <summary>Builds a key from a namespace and a single already-formed identifier.</summary>
    public static CacheKey Raw(string cacheNamespace, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        return new CacheKey(cacheNamespace, Sanitize(identifier));
    }

    /// <summary>
    /// The string handed to the store: <c>prefix:namespace:version:identifier</c>.
    /// </summary>
    /// <param name="prefix">
    /// Deployment prefix. Two environments sharing one Redis without it read each other's entries —
    /// which is discovered when staging's feature-flag snapshot appears in production.
    /// </param>
    /// <param name="version">The namespace's current version. Incrementing it invalidates the family.</param>
    public string ToStorageKey(string prefix, long version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}{Separator}{Namespace}{Separator}v{version}{Separator}{Identifier}");
    }

    public override string ToString() => $"{Namespace}{Separator}{Identifier}";

    /// <summary>
    /// Replaces the separator inside a caller-supplied part.
    /// <para>
    /// Without this, a part containing a colon shifts every segment after it and two different keys
    /// can collide. Cheap insurance against a defect that would present as one member seeing
    /// another member's cached page.
    /// </para>
    /// </summary>
    private static string Sanitize(string part) =>
        part.IndexOf(Separator, StringComparison.Ordinal) < 0
            ? part
            : part.Replace(Separator, '_');
}
