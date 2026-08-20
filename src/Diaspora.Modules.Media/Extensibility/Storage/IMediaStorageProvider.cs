using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Extensibility.Storage;

/// <summary>
/// One place bytes can live: a filesystem, S3, Azure Blob, Cloudflare R2, Google Cloud
/// Storage, MinIO.
/// <para>
/// <b>The contract that makes storage replaceable.</b> Nothing above this interface names a
/// provider, a bucket, an SDK or a URL scheme; the domain records <em>which</em> provider an
/// object lives in (<see cref="StorageLocation.Provider"/>) and asks the registry for it by
/// name at read time. That is what makes a provider migration a gradual, reversible operation
/// rather than a big-bang copy: objects written before the switch keep naming their old
/// provider and keep working.
/// </para>
/// <para>
/// <b>Capabilities are declared, not assumed.</b> A filesystem cannot mint a presigned URL and
/// has no server-side copy; S3 can do both. Rather than force every provider to fake what it
/// cannot do — the design that produces a presigned URL which is really an API route and then
/// surprises somebody at 3 a.m. — a provider states what it supports and the pipeline plans
/// around it. <see cref="Capabilities"/> is why <see cref="UploadProtocol.Multipart"/> and
/// <see cref="UploadProtocol.PresignedDirect"/> can exist without every deployment supporting
/// them.
/// </para>
/// <para>
/// <b>Implementing one.</b> Put it in any <c>Diaspora.*</c> assembly with a public
/// parameterless-resolvable constructor and register it — the module scans for implementations
/// exactly as Location scans for <c>INearbyResourceProvider</c>. Media never learns the type
/// exists; it resolves it through <c>StorageProviderRegistry</c> by
/// <see cref="Name"/>.
/// </para>
/// </summary>
public interface IMediaStorageProvider
{
    /// <summary>
    /// The provider's stable name — <c>filesystem</c>, <c>s3</c>, <c>azure-blob</c>.
    /// <para>
    /// Written into every <see cref="StorageLocation"/> this provider creates and used to
    /// resolve it again years later. Changing it orphans every object already stored under the
    /// old name, so treat it as a contract rather than a label.
    /// </para>
    /// </summary>
    string Name { get; }

    /// <summary>The bucket, container or root directory this provider writes to by default.</summary>
    string DefaultContainer { get; }

    /// <summary>What this provider can actually do.</summary>
    StorageCapability Capabilities { get; }

    /// <summary>Writes an object, overwriting any existing one at the same key.</summary>
    Task<StorageWriteResult> WriteAsync(
        StorageWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an object for reading.
    /// <para>
    /// Returns a stream rather than a byte array on purpose: a 512 MB video read into memory
    /// to be written straight back out to a response is 512 MB of managed heap per concurrent
    /// download, and the download endpoint is the one place that scales with member count
    /// rather than with member activity.
    /// </para>
    /// </summary>
    Task<Stream?> OpenReadAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens part of an object, for HTTP range requests.
    /// <para>
    /// The difference between a video that seeks and a video that must be downloaded in full
    /// before it plays. Providers that cannot do this return the whole object and the caller
    /// slices — correct, merely slower.
    /// </para>
    /// </summary>
    Task<Stream?> OpenRangeAsync(
        string container,
        StorageKey key,
        long offset,
        long? length,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default);

    Task<StorageObjectInfo?> DescribeAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string container, StorageKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes everything under a prefix. How an asset and all its renditions go at once.
    /// </summary>
    Task DeletePrefixAsync(
        string container, string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints a provider-signed URL, when
    /// <see cref="StorageCapability.PresignedRead"/> is supported.
    /// <para>
    /// Returns null when it is not, rather than throwing or fabricating one. The caller falls
    /// back to serving through the API, which every provider supports.
    /// </para>
    /// </summary>
    Task<Uri?> CreatePresignedReadUrlAsync(
        string container,
        StorageKey key,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints a URL the client may PUT to directly, when
    /// <see cref="StorageCapability.PresignedWrite"/> is supported.
    /// </summary>
    Task<Uri?> CreatePresignedWriteUrlAsync(
        string container,
        StorageKey key,
        MediaContentType contentType,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assembles staged parts into one object, server-side where the provider can
    /// (<see cref="StorageCapability.ServerSideCompose"/>) and by streaming where it cannot.
    /// <para>
    /// A 512 MB video assembled through the application is 512 MB across the network twice;
    /// assembled by S3's multipart-complete it never leaves the provider. Both are correct,
    /// and the difference is entirely a provider capability — which is exactly the sort of
    /// thing the domain must not know about.
    /// </para>
    /// </summary>
    Task<StorageWriteResult> ComposeAsync(
        string container,
        IReadOnlyList<StorageKey> parts,
        StorageKey destination,
        MediaContentType contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the provider is reachable. Feeds the module's readiness check.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
