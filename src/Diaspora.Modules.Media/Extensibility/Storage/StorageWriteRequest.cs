using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>
/// Everything a provider needs in order to write one object.
/// <para>
/// A request object rather than eight parameters, because the set will grow — server-side
/// encryption keys, object tags, a legal hold — and a parameter list that grows is a breaking
/// change to every provider anyone has written. A record with new optional properties is not.
/// </para>
/// </summary>
public sealed record StorageWriteRequest
{
    /// <summary>Bucket, container or root directory.</summary>
    public required string Container { get; init; }

    public required StorageKey Key { get; init; }

    /// <summary>
    /// The bytes. The provider reads it to the end and does not dispose it — ownership stays
    /// with the caller, which is what lets a caller hash and write the same stream.
    /// </summary>
    public required Stream Content { get; init; }

    public required MediaContentType ContentType { get; init; }

    /// <summary>
    /// Length in bytes when known. Providers that must set <c>Content-Length</c> ahead of the
    /// body need it; a streaming source legitimately does not have it.
    /// </summary>
    public long? ContentLength { get; init; }

    public StorageTier Tier { get; init; } = StorageTier.Hot;

    /// <summary>
    /// The name to offer on download, when the object is served as an attachment. Always the
    /// sanitised form — a raw client-supplied name in a <c>Content-Disposition</c> header is a
    /// response-splitting vector.
    /// </summary>
    public string? DownloadFileName { get; init; }

    /// <summary>
    /// Cache lifetime hint for providers that set headers on the object. Null for anything
    /// private: an immigration document must never acquire a public cache directive.
    /// </summary>
    public TimeSpan? CacheLifetime { get; init; }
}
