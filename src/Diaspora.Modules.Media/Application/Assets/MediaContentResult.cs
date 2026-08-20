namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// An open read over an asset's bytes, plus everything the response needs to describe them.
/// <para>
/// A stream and not a byte array, deliberately and non-negotiably. A 512 MB video read into
/// memory to be copied to a response body is half a gigabyte of managed heap for every
/// concurrent download, and downloads are the one endpoint whose concurrency scales with how
/// many members are <em>looking</em> rather than how many are acting. Streaming keeps the
/// per-request cost at the buffer size.
/// </para>
/// <para>
/// The caller disposes <see cref="Content"/>. In practice that is the endpoint, which hands it
/// to <c>Results.Stream</c> and lets ASP.NET Core dispose it after the body is written.
/// </para>
/// </summary>
public sealed class MediaContentResult : IAsyncDisposable
{
    public MediaContentResult(
        Stream content,
        string contentType,
        string fileName,
        long? length,
        string? entityTag,
        bool isPubliclyCacheable,
        DateTimeOffset lastModified)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
        ContentType = contentType;
        FileName = fileName;
        Length = length;
        EntityTag = entityTag;
        IsPubliclyCacheable = isPubliclyCacheable;
        LastModified = lastModified;
    }

    public Stream Content { get; }

    public string ContentType { get; }

    /// <summary>
    /// The name offered on download. Always the sanitised form — a raw client-supplied name in
    /// a <c>Content-Disposition</c> header is a response-splitting vector.
    /// </summary>
    public string FileName { get; }

    public long? Length { get; }

    /// <summary>
    /// Derived from the content hash, so it is stable across restarts, across instances, and
    /// across a storage migration. A provider ETag would change when the object moved.
    /// </summary>
    public string? EntityTag { get; }

    /// <summary>
    /// Whether a shared cache may keep this.
    /// <para>
    /// False for anything not public — which is what stops a CDN in front of the API caching
    /// an immigration document and serving it to the next person who asks for that URL.
    /// </para>
    /// </summary>
    public bool IsPubliclyCacheable { get; }

    public DateTimeOffset LastModified { get; }

    public async ValueTask DisposeAsync() => await Content.DisposeAsync();
}
