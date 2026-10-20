using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
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
    private readonly IHttpContextAccessor _httpContext;
    private readonly MediaStorageOptions _options;

    public MediaUrlFactory(
        SignedUrlFactory signedUrls,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContext,
        IOptions<MediaStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _signedUrls = signedUrls;
        _currentUser = currentUser;
        _httpContext = httpContext;
        _options = options.Value;
    }

    /// <summary>The URL for an asset's original bytes.</summary>
    public string ForOriginal(MediaAsset asset) =>
        ForVariant(asset, MediaVariantPurpose.Original);

    /// <summary>
    /// The URL for one rendition, signed when the asset requires it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Who the token is bound to depends on who can present themselves again.</b> Binding a
    /// link to one member is what stops it working for whoever reads the support ticket it gets
    /// pasted into — a real property, and the right one for a verification document, which is
    /// fetched by code that holds a bearer token anyway.
    /// </para>
    /// <para>
    /// It is the wrong property for a photograph. The consumer of this URL is
    /// <c>&lt;img src&gt;</c>, which sends no <c>Authorization</c> header, so the request that
    /// redeems it is anonymous by construction and can never match an audience. Every embedded
    /// media URL was minted bound to the member who asked for the response and then fetched by an
    /// image loader that could not prove it was them — so the token made the URL <em>less</em>
    /// usable than no token at all, because a rejected signature stops the request rather than
    /// falling through to the ordinary access check.
    /// </para>
    /// <para>
    /// So an ordinary asset gets an unbound link: short-lived, unguessable, and redeemable by
    /// whoever holds it — which, for an image on a listing anyone may look at, is the whole
    /// point.
    /// </para>
    /// </remarks>
    public string ForVariant(MediaAsset asset, MediaVariantPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (!asset.RequiresSignedAccess)
        {
            return Absolute(PathFor(asset, purpose));
        }

        var audience = asset.Kind is MediaKind.VerificationDocument
            ? _currentUser.UserId
            : null;

        return ForSignedVariant(asset, purpose, audience, asset.SignedUrlLifetime).Url;
    }

    /// <summary>
    /// The bare, unsigned URL of one rendition, for a caller that holds only the identifier.
    /// <para>
    /// For an asset already made public — a member's avatar, a creator's cover — whose owner keeps
    /// the identifier rather than the asset. The URL is only useful if the asset really is public;
    /// for anything else the content route refuses an anonymous fetch, which is the right failure.
    /// </para>
    /// </summary>
    public string ForPublicVariant(Guid mediaId, MediaVariantPurpose purpose) =>
        Absolute(PathFor(mediaId, purpose));

    private static string PathFor(MediaAsset asset, MediaVariantPurpose purpose) =>
        PathFor(asset.Id.Value, purpose);

    private static string PathFor(Guid mediaId, MediaVariantPurpose purpose) =>
        purpose is MediaVariantPurpose.Original
            ? $"/api/v1/media/{mediaId}/content"
            : $"/api/v1/media/{mediaId}/variants/{purpose.ToString().ToLowerInvariant()}";

    /// <summary>
    /// A signed link to one rendition, minted for a named audience.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns the expiry as well as the URL because they are one fact. The signed-url endpoint
    /// used to build the route itself and then mint a token beside it; splitting the two is how a
    /// response ends up advertising an expiry that is not the one inside the link.
    /// </para>
    /// <para>
    /// <paramref name="lifetime"/> is passed through untouched, including null — which means "use
    /// the factory's default for this kind". The two callers deliberately differ, and did before
    /// this method existed: an embedded URL takes the asset's own lifetime, and an explicitly
    /// requested link takes the deployment-configured one.
    /// </para>
    /// </remarks>
    public SignedMediaLink ForSignedVariant(
        MediaAsset asset,
        MediaVariantPurpose purpose,
        UserId? audience,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var token = _signedUrls.Create(asset.Id, purpose, audience, asset.Kind, lifetime);

        return new SignedMediaLink(
            Absolute($"{PathFor(asset, purpose)}?{token.ToQueryString()}"),
            token.ExpiresAt);
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

    /// <summary>
    /// Turns a route into something a client can actually fetch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A relative URL is wrong for every real consumer of this platform.</b> The option's
    /// note — "empty means links are relative, which is correct behind a single origin" —
    /// assumed a browser on that origin. The consumer is a mobile app: it holds a base of
    /// <c>https://host/v1</c> while these routes begin <c>/api/v1</c>, so joining the two yields
    /// <c>/v1/api/v1/media/…</c>. The deployment had no <c>PublicBaseUrl</c> set, so that is
    /// precisely what every media URL did.
    /// </para>
    /// <para>
    /// The order is: the configured base first, because a deployment behind a CDN or a custom
    /// domain knows its public name better than any single request does; then the current
    /// request's own origin, which is right far more often than a relative path and cannot be
    /// left out of a configuration file; and a relative path only when there is no request at all
    /// — a background worker composing a notification, where an invented origin would be worse
    /// than an obviously incomplete one.
    /// </para>
    /// <para>
    /// The scheme is the forwarded one: the host calls <c>UseForwardedHeaders</c> with
    /// <c>XForwardedProto</c>, so behind a TLS-terminating proxy this reads <c>https</c> rather
    /// than the <c>http</c> the proxy speaks to it. Without that every image URL would be mixed
    /// content, which a mobile client refuses outright.
    /// </para>
    /// </remarks>
    private string Absolute(string path)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}{path}";
        }

        var request = _httpContext.HttpContext?.Request;

        return request is not null && request.Host.HasValue
            ? $"{request.Scheme}://{request.Host.Value}{path}"
            : path;
    }
}

/// <summary>A signed link and the moment it stops working. One fact, returned together.</summary>
public sealed record SignedMediaLink(string Url, DateTimeOffset ExpiresAt);
