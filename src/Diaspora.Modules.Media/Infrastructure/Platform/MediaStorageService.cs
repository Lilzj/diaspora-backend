using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Infrastructure.Platform;

/// <summary>
/// Fulfils the platform's <see cref="IStorageService"/>, which has been declared and
/// unimplemented since B1.
/// <para>
/// <b>This class is the entire Trust integration, and it required no change to Trust.</b>
/// Trust's <c>DocumentLinkFactory</c> takes an <c>IEnumerable&lt;IStorageService&gt;</c>
/// specifically so it can work before an implementation exists — its own documentation says
/// "when a storage provider is registered, by Infrastructure or by whichever phase supplies
/// one, this starts working with no change here." B5 is that phase. Registering this type in
/// the Media module's <c>ConfigureServices</c> turns Trust's null document links into real,
/// short-lived, audited ones, and not a line of the Trust module was touched to do it.
/// </para>
/// <para>
/// <b>Two kinds of key reach this service.</b> A <em>media reference</em> — the identifier of
/// an asset Media owns — is resolved to that asset and served through Media's own signed
/// endpoint, so the access check, the audit line and the quarantine state all still apply. A
/// <em>raw storage key</em> is passed to the provider directly, which is what a caller that
/// wrote bytes through this same interface expects to get back. The first form is what every
/// new caller should use; the second exists because the interface's shape predates B5 and
/// promises it.
/// </para>
/// <para>
/// The URLs it returns are relative when no public base URL is configured, which is correct
/// behind a single origin and is what the frontend's <c>apiClient</c> baseURL already assumes.
/// </para>
/// </summary>
public sealed class MediaStorageService : IStorageService
{
    /// <summary>
    /// The prefix marking a key as a media reference rather than a raw object key. Chosen so
    /// it cannot collide with a <see cref="StorageKey"/>, whose first segment is always a
    /// media kind or <c>uploads</c>.
    /// </summary>
    public const string MediaReferencePrefix = "media:";

    private readonly StorageProviderRegistry _storage;
    private readonly IMediaAssetRepository _assets;
    private readonly SignedUrlFactory _signedUrls;
    private readonly MediaStorageOptions _options;
    private readonly ILogger<MediaStorageService> _logger;

    public MediaStorageService(
        StorageProviderRegistry storage,
        IMediaAssetRepository assets,
        SignedUrlFactory signedUrls,
        IOptions<MediaStorageOptions> options,
        ILogger<MediaStorageService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _storage = storage;
        _assets = assets;
        _signedUrls = signedUrls;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Writes bytes at a caller-supplied key and returns a link to them.
    /// <para>
    /// The key still goes through <see cref="StorageKey"/> validation. A caller outside this
    /// module supplying <c>../../etc/passwd</c> is refused by the same value object that
    /// guards Media's own writes — the platform interface does not get a weaker path.
    /// </para>
    /// </summary>
    public async Task<string> UploadAsync(
        Stream content,
        string key,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageKey = StorageKey.TryCreate(key);

        if (storageKey.IsFailure)
        {
            throw new ArgumentException("Not a usable storage key.", nameof(key));
        }

        var type = MediaContentType.TryCreate(contentType);

        await _storage.Default.WriteAsync(
            new StorageWriteRequest
            {
                Container = _options.Container,
                Key = storageKey.Value,
                Content = content,
                ContentType = type.IsSuccess ? type.Value : MediaContentType.OctetStream,
                ContentLength = content.CanSeek ? content.Length : null
            },
            cancellationToken);

        return await GetUrlAsync(key, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (TryParseMediaId(key) is { } mediaId)
        {
            var asset = await _assets.GetAsync(mediaId, cancellationToken);

            if (asset is not null)
            {
                var provider = _storage.Resolve(asset.Location.Provider);

                if (provider.IsSuccess)
                {
                    await provider.Value.DeletePrefixAsync(
                        asset.Location.Container, asset.Location.Key.Prefix, cancellationToken);
                }
            }

            return;
        }

        var storageKey = StorageKey.TryCreate(key);

        if (storageKey.IsSuccess)
        {
            await _storage.Default.DeleteAsync(
                _options.Container, storageKey.Value, cancellationToken);
        }
    }

    /// <summary>
    /// A durable link. For a media reference this is Media's content route, which applies the
    /// access check on every request — a permanently valid <em>route</em> rather than a
    /// permanently valid <em>grant</em>, which is the distinction that keeps this safe to
    /// store.
    /// </summary>
    public Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default)
    {
        if (TryParseMediaId(key) is { } mediaId)
        {
            return Task.FromResult(Absolute($"/api/v1/media/{mediaId.Value}/content"));
        }

        return Task.FromResult(Absolute($"/api/v1/media/objects/{Uri.EscapeDataString(key)}"));
    }

    /// <summary>
    /// A short-lived link. This is the method Trust calls, and the one that makes verification
    /// documents visible to reviewers.
    /// <para>
    /// Prefers a platform-signed link over a provider-presigned one, deliberately. A
    /// provider-presigned URL bypasses the platform entirely: no access check, no audit line,
    /// and no way to stop it working when the asset is quarantined. For an immigration document
    /// that is the wrong trade, and the round trip through the API is not the bottleneck.
    /// </para>
    /// </summary>
    public async Task<string> GetPresignedUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (TryParseMediaId(key) is { } mediaId)
        {
            var asset = await _assets.GetAsync(mediaId, cancellationToken);

            if (asset is null)
            {
                throw new FileNotFoundException("No media asset exists for that reference.", key);
            }

            var token = _signedUrls.Create(
                asset.Id, MediaVariantPurpose.Original, audience: null, asset.Kind, expiry);

            return Absolute($"/api/v1/media/{asset.Id.Value}/content?{token.ToQueryString()}");
        }

        var storageKey = StorageKey.TryCreate(key);

        if (storageKey.IsFailure)
        {
            throw new ArgumentException("Not a usable storage key.", nameof(key));
        }

        var presigned = await _storage.Default.CreatePresignedReadUrlAsync(
            _options.Container, storageKey.Value, expiry, cancellationToken);

        if (presigned is not null)
        {
            return presigned.ToString();
        }

        // The filesystem provider cannot presign. Serving through the API is the fallback
        // every provider supports, and it is the safer of the two anyway.
        _logger.LogDebug(
            "Provider {Provider} cannot presign; serving {Key} through the API instead.",
            _storage.Default.Name, key);

        return Absolute($"/api/v1/media/objects/{Uri.EscapeDataString(key)}");
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (TryParseMediaId(key) is { } mediaId)
        {
            return await _assets.GetSummaryAsync(mediaId, cancellationToken) is not null;
        }

        var storageKey = StorageKey.TryCreate(key);

        return storageKey.IsSuccess &&
               await _storage.Default.ExistsAsync(
                   _options.Container, storageKey.Value, cancellationToken);
    }

    /// <summary>
    /// Recognises a media reference in either of the two forms callers actually hold: a bare
    /// identifier, or one carrying the explicit prefix.
    /// </summary>
    private static MediaId? TryParseMediaId(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var candidate = key.StartsWith(MediaReferencePrefix, StringComparison.OrdinalIgnoreCase)
            ? key[MediaReferencePrefix.Length..]
            : key;

        return Guid.TryParse(candidate, out var value) ? MediaId.From(value) : null;
    }

    private string Absolute(string path) =>
        string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? path
            : $"{_options.PublicBaseUrl.TrimEnd('/')}{path}";
}
