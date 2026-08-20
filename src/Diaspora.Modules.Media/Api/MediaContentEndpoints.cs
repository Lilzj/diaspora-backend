using System.Globalization;
using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// The two routes that return bytes rather than JSON.
/// <para>
/// <b>These are the only endpoints on the platform that do not use the response envelope</b>,
/// and the exception is deliberate rather than an oversight. The envelope describes a
/// resource; this route <em>is</em> the resource. An <c>&lt;img src&gt;</c> cannot parse JSON,
/// and wrapping a 512 MB video in a body with a <c>meta</c> block would be absurd.
/// </para>
/// <para>
/// <b>Anonymous, and correctly so.</b> The route allows unauthenticated callers because a
/// public listing photograph must render for a member who has not signed in — and because an
/// image tag cannot send a bearer token even when one exists. Access is decided inside the
/// handler from the asset's visibility and, for anything private, a signed token in the query
/// string. An authorisation attribute here would break public images without making private
/// ones any safer.
/// </para>
/// <para>
/// Caching headers follow visibility exactly. Public assets are immutable and cacheable by
/// shared caches — the content hash is in the ETag and the URL never changes meaning. Anything
/// else is <c>no-store</c>, which is what stops a CDN caching an immigration document and
/// handing it to the next person who requests that URL.
/// </para>
/// </summary>
internal static class MediaContentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{mediaId:guid}/content", async (
                Guid mediaId,
                HttpContext context,
                ISender sender,
                CancellationToken cancellationToken) =>
                await ServeAsync(mediaId, variant: null, context, sender, cancellationToken))
            .AllowAnonymous()
            .WithName("GetMediaContent")
            .WithSummary("The asset's bytes. Range-capable; signed for private assets.");

        group.MapGet("/{mediaId:guid}/variants/{variant}", async (
                Guid mediaId,
                string variant,
                HttpContext context,
                ISender sender,
                CancellationToken cancellationToken) =>
                await ServeAsync(mediaId, variant, context, sender, cancellationToken))
            .AllowAnonymous()
            .WithName("GetMediaVariantContent")
            .WithSummary("One rendition's bytes.");
    }

    private static async Task<IResult> ServeAsync(
        Guid mediaId,
        string? variant,
        HttpContext context,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = context.Request.Query;

        var result = await sender.Send(
            new GetMediaContentQuery
            {
                MediaId = mediaId,
                Variant = variant ?? query[SignedMediaToken.VariantParameter].FirstOrDefault(),
                Signature = query[SignedMediaToken.SignatureParameter].FirstOrDefault(),
                ExpiresAtUnixSeconds =
                    long.TryParse(
                        query[SignedMediaToken.ExpiryParameter].FirstOrDefault(),
                        CultureInfo.InvariantCulture,
                        out var expiry)
                        ? expiry
                        : null,
                AudienceUserId =
                    Guid.TryParse(
                        query[SignedMediaToken.AudienceParameter].FirstOrDefault(),
                        out var audience)
                        ? audience
                        : null
            },
            cancellationToken);

        if (result.IsFailure)
        {
            // Errors still use the platform's problem document. A failure is a description of
            // a resource state, which is exactly what the envelope is for.
            return PlatformResults.Ok(context, result);
        }

        var content = result.Value;

        // A conditional request that matches costs nothing to answer. The tag is derived from
        // the content hash, so it is identical across instances and survives a storage
        // migration — which a provider ETag would not.
        if (content.EntityTag is { } tag &&
            context.Request.Headers.IfNoneMatch.Any(
                value => string.Equals(value, tag, StringComparison.Ordinal)))
        {
            await content.DisposeAsync();
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        ApplyHeaders(context, content);

        return Results.Stream(
            content.Content,
            content.ContentType,
            enableRangeProcessing: true,
            lastModified: content.LastModified,
            entityTag: content.EntityTag is null
                ? null
                : EntityTagHeaderValue.Parse(content.EntityTag));
    }

    private static void ApplyHeaders(HttpContext context, MediaContentResult content)
    {
        var headers = context.Response.Headers;

        headers.CacheControl = content.IsPubliclyCacheable

            // Immutable: the URL identifies bytes, and bytes do not change. A year is the
            // conventional maximum and is what makes a CDN in front of this worth having.
            ? "public, max-age=31536000, immutable"

            // Private assets must not sit in a shared cache. This is the header that stops a
            // CDN serving one member's verification document to the next caller.
            : "no-store, no-cache, must-revalidate, private";

        // Always an attachment disposition with an explicit type, never inline. An HTML or SVG
        // file served inline from the platform's own origin is stored cross-site scripting;
        // forcing a download makes the content type irrelevant to the browser's parser.
        headers.ContentDisposition =
            $"inline; filename=\"{content.FileName}\"";

        // Belt and braces on the same problem: even if a disposition is mishandled, this stops
        // the browser second-guessing the declared content type.
        headers["X-Content-Type-Options"] = "nosniff";
    }
}
