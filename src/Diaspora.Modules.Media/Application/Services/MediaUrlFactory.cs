using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Builds the URLs that appear in every media response.
/// <para>
/// <b>Every URL this produces points at Media, never at a storage provider.</b> That is the
/// property that makes provider migration invisible: a client that cached
/// <c>/api/v1/media/{id}/content</c> in 2026 still resolves it in 2028 after the platform has
/// moved from MinIO to S3, because the route is stable and the provider lookup happens behind
/// it. A response carrying a bucket URL would put the provider choice into every client cache,
/// every database row and every screenshot in a support ticket.
/// </para>
/// <para>
/// A public asset gets a bare route, which a CDN in front of the API can cache. A private one
/// gets the same route plus a signed, short-lived, audience-bound token — because the caller
/// asking for the response is not necessarily the caller who will fetch the image, and an
/// <c>&lt;img src&gt;</c> cannot send a bearer token.
/// </para>
/// </summary>
public sealed class MediaUrlFactory
{
    private readonly SignedUrlFactory _signedUrls;
    private readonly ICurrentUser _currentUser;
    private readonly MediaStorageOptions _options;

    public MediaUrlFactory(
        SignedUrlFactory signedUrls,
        ICurrentUser currentUser,
        IOptions<MediaStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _signedUrls = signedUrls;
        _currentUser = currentUser;
        _options = options.Value;
    }

    /// <summary>The URL for an asset's original bytes.</summary>
    public string ForOriginal(MediaAsset asset) =>
        ForVariant(asset, MediaVariantPurpose.Original);

    /// <summary>
    /// The URL for one rendition.
    /// <para>
    /// Signed when the asset requires it, and bound to the current caller when there is one —
    /// so a URL lifted out of one member's response does not work in another's browser.
    /// </para>
    /// </summary>
    public string ForVariant(MediaAsset asset, MediaVariantPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var path = purpose is MediaVariantPurpose.Original
            ? $"/api/v1/media/{asset.Id.Value}/content"
            : $"/api/v1/media/{asset.Id.Value}/variants/{purpose.ToString().ToLowerInvariant()}";

        if (!asset.RequiresSignedAccess)
        {
            return Absolute(path);
        }

        var token = _signedUrls.Create(
            asset.Id, purpose, _currentUser.UserId, asset.Kind, asset.SignedUrlLifetime);

        return Absolute($"{path}?{token.ToQueryString()}");
    }

    /// <summary>
    /// The thumbnail URL, or null when no thumbnail exists.
    /// <para>
    /// Null rather than a fallback to the original, deliberately. The client's
    /// <c>thumbnailUrl</c> is already optional and its renderers already handle the absence;
    /// silently substituting a full-size image would make a list of forty photographs download
    /// forty full-size images and nobody would notice until the bandwidth bill.
    /// </para>
    /// </summary>
    public string? ForThumbnail(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        return asset.HasVariant(MediaVariantPurpose.Thumbnail)
            ? ForVariant(asset, MediaVariantPurpose.Thumbnail)
            : null;
    }

    /// <summary>Where a client sends the next part of an upload.</summary>
    public string ForUploadSession(Guid sessionId) =>
        Absolute($"/api/v1/media/uploads/{sessionId}/chunks");

    private string Absolute(string path) =>
        string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? path
            : $"{_options.PublicBaseUrl.TrimEnd('/')}{path}";
}
