using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Mints short-lived links to verification documents, when the platform has somewhere to
/// mint them from.
/// <para>
/// The brief is explicit that Trust must not implement storage — it reuses the platform's
/// <see cref="IStorageService"/>. That abstraction was declared in B1 and, as of B3, still
/// has no registered implementation: Trust is the first module that actually needs to read a
/// file back. Rather than register a fake one and pretend the capability exists, this takes
/// the dependency <em>optionally</em>.
/// </para>
/// <para>
/// The <c>IEnumerable&lt;IStorageService&gt;</c> is the mechanism. The container resolves it
/// to an empty sequence when nothing is registered, which is the idiomatic way to express an
/// optional dependency without a service locator and without a null-object implementation
/// that would hand callers a broken URL. When a storage provider is registered — by
/// Infrastructure, or by whichever phase supplies one — this starts working with no change
/// here.
/// </para>
/// <para>
/// Until then a document link is <c>null</c>, which <c>VerificationDocumentResponse.DocumentUrl</c>
/// already models as "the caller may not read this file". A reviewer sees the document's
/// metadata and no link, which is a visibly incomplete feature rather than a silently broken
/// one.
/// </para>
/// </summary>
public sealed class DocumentLinkFactory
{
    /// <summary>
    /// How long a link stays valid. Short on purpose: these are immigration documents, and a
    /// link that outlives the page it was rendered on is a link that ends up pasted into a
    /// support ticket.
    /// </summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);

    private readonly IStorageService? _storage;
    private readonly ILogger<DocumentLinkFactory> _logger;

    public DocumentLinkFactory(
        IEnumerable<IStorageService> storage,
        ILogger<DocumentLinkFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage.FirstOrDefault();
        _logger = logger;
    }

    /// <summary>Whether the platform can currently produce document links at all.</summary>
    public bool IsAvailable => _storage is not null;

    /// <summary>
    /// A presigned link for one stored object, or null when storage is not configured or the
    /// object is missing.
    /// </summary>
    public async Task<string?> TryCreateLinkAsync(
        string storageKey, CancellationToken cancellationToken = default)
    {
        if (_storage is null || string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        try
        {
            return await _storage.GetPresignedUrlAsync(storageKey, LinkLifetime, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A storage failure must not fail the read. A reviewer opening a verification
            // should see the metadata and a missing link, not a 500.
            _logger.LogError(
                exception, "Could not mint a document link for key {StorageKey}.", storageKey);

            return null;
        }
    }

    /// <summary>Links for several objects, keyed by whatever identifier the caller uses.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> CreateLinksAsync(
        IEnumerable<(Guid Id, string StorageKey)> objects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objects);

        var links = new Dictionary<Guid, string>();

        if (_storage is null)
        {
            return links;
        }

        foreach (var (id, key) in objects)
        {
            if (await TryCreateLinkAsync(key, cancellationToken) is { } url)
            {
                links[id] = url;
            }
        }

        return links;
    }
}
