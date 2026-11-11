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

        // BSEC-005. This comment used to read "always an attachment disposition, never inline"
        // and the next statement emitted `inline` unconditionally. The comment named the right
        // threat -- an HTML or SVG file served inline from the platform's own origin is stored
        // cross-site scripting -- and was the only thing implementing it.
        //
        // `inline` is now reached only by the types the client renders in place, by allow-list.
        // Everything else downloads, which makes the content type irrelevant to the browser's
        // parser whatever it claims to be.
        headers.ContentDisposition = Disposition(content);

        // Belt and braces on the same problem: even if a disposition is mishandled, this stops
        // the browser second-guessing the declared content type.
        headers["X-Content-Type-Options"] = "nosniff";

        // And a third layer, because the first two are both header-shaped and a document served
        // inline despite them should still be inert. `default-src 'none'` denies every fetch a
        // rendered document could make and `sandbox` drops it into an opaque origin, so script
        // in a file that reached a parser has nothing to reach back to.
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
    }

    /// <summary>
    /// The content types the client renders in place, and the only ones served <c>inline</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An allow-list rather than a deny-list of the dangerous types, because the dangerous set is
    /// whatever a browser will parse next year. <c>FileSignatureReader</c> does not recognise SVG
    /// or HTML today, so neither can currently be stored — which is why BSEC-005 is MEDIUM rather
    /// than HIGH — and that is a property of the signature table rather than of this decision.
    /// Adding a signature should not quietly make a new type renderable from this origin.
    /// </para>
    /// <para>
    /// Decided from the content type and not from <c>MediaKind</c>, deliberately: the question is
    /// what a browser would do with the type the response declares, and the type is what it reads.
    /// A kind would have to be plumbed through <c>MediaContentResult</c> to answer the same
    /// question one step further from the evidence.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> InlineRenderableTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
            "video/mp4",
            "video/webm"
        };

    /// <summary>
    /// The <c>Content-Disposition</c> header for one asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name needs no escaping here, and that is worth stating rather than re-implementing.</b>
    /// BSEC-005's secondary point is that <c>FileName</c> is interpolated without sanitisation, and
    /// a quote or a CR/LF would break the header. It cannot: <c>SafeFileName.Sanitize</c> keeps only
    /// <c>char.IsLetterOrDigit</c>, excluding the Format, Control, Surrogate and PrivateUse
    /// categories, and maps space, hyphen, underscore and dot to a single hyphen. A double quote is
    /// not a letter or a digit; CR and LF are Control. Neither can reach this string, and
    /// <c>MediaContentResult.FileName</c> is documented as always the sanitised form.
    /// </para>
    /// <para>
    /// <b>What does survive is non-ASCII</b>, because <c>é</c> and <c>Ω</c> are letters. RFC 6266
    /// allows only ASCII in the plain <c>filename</c> parameter, so a name holding either is
    /// emitted in the RFC 5987 <c>filename*</c> form as well — the extended parameter for clients
    /// that read it, and an ASCII-folded <c>filename</c> for those that do not.
    /// </para>
    /// </remarks>
    private static string Disposition(MediaContentResult content)
    {
        var disposition = InlineRenderableTypes.Contains(content.ContentType)
            ? "inline"
            : "attachment";

        var name = content.FileName;

        if (name.All(char.IsAscii))
        {
            return $"{disposition}; filename=\"{name}\"";
        }

        // Both forms, in the order RFC 6266 expects: a client that understands filename* uses it
        // and ignores the other, and one that does not still gets a usable name.
        var ascii = new string([.. name.Where(char.IsAscii)]);

        if (ascii.Length == 0)
        {
            ascii = "file";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{disposition}; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(name)}");
    }
}
