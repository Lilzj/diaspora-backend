using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Media.Domain.Policies;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The byte ceilings on the two routes that carry bytes (BSEC-006).
/// <para>
/// <b>The chunk route's own comment claimed this existed.</b> It read "the size cap is the
/// session's chunk size plus headroom, enforced by the host's request-body limit — a route that
/// accepted an unbounded body would be a memory exhaustion vector regardless of what the session
/// declared". A repository-wide search for <c>MaxRequestBodySize</c>, <c>RequestSizeLimit</c>,
/// <c>MultipartBodyLengthLimit</c> and <c>Kestrel</c> returned nothing, so the real ceiling was
/// Kestrel's default 30 MB: six times the chunk size, and not a number anybody chose.
/// </para>
/// <para>
/// <b>These assert the declaration rather than a 413, and that is deliberate.</b> The obvious test
/// is to send an oversized body and expect 413. Probed in this host, an oversized chunk PUT comes
/// back <c>401</c> — authorization runs before anything reads the body — so the status says nothing
/// about the limit. Getting past that needs an authenticated caller and a live session, and even
/// then whether <c>TestServer</c> enforces <c>IHttpMaxRequestBodySizeFeature</c> at all is
/// unproven here, because the short-circuit meant no body was ever read.
/// </para>
/// <para>
/// Asserting a 413 this suite has never observed would be the same mistake as the comment: a
/// statement of a control standing in for the control. What is provable is that the limit is
/// declared on the endpoint and equals the payload the route is allowed to receive, which is
/// exactly what was absent.
/// </para>
/// </summary>
[Collection(PlatformCollection.Name)]
public sealed class RequestBodyLimitTests
{
    private const string ChunkRoute =
        "/api/v1/media/uploads/{sessionId:guid}/chunks/{chunkNumber:int}";

    private const string DirectRoute = "/api/v1/media/uploads/direct";

    /// <summary>Framing allowance over the payload, as <c>UploadEndpoints</c> declares it.</summary>
    private const long Headroom = 256 * 1024;

    private readonly List<RouteEndpoint> _endpoints;

    public RequestBodyLimitTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.EnsureDatabaseCreated();

        using var scope = factory.Services.CreateScope();

        _endpoints = [.. scope.ServiceProvider
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// <b>The chunk PUT is bounded at one chunk plus framing.</b>
    /// <para>
    /// Compared against <see cref="MediaLimits.ChunkSize"/> rather than against a number, because
    /// the comment's promise is a relationship: raising the chunk size must raise the limit with
    /// it, and a literal here would silently stop being the chunk size the day somebody changed
    /// one.
    /// </para>
    /// </summary>
    [Fact]
    public void The_chunk_route_is_bounded_by_the_declared_chunk_size()
    {
        Limit(ChunkRoute, "PUT").Should().Be(
            MediaLimits.ChunkSize.Bytes + Headroom,
            "the cap the route's comment described, which did not exist: the effective ceiling "
            + "was Kestrel's 30 MB default, six times the chunk size");
    }

    /// <summary>
    /// <b>The direct POST is bounded at one whole small file plus framing</b>, and the form reader
    /// is bounded too.
    /// </summary>
    /// <remarks>
    /// Two limits because this route reads a form: <c>RequestSizeLimit</c> is what the server
    /// enforces as bytes arrive, and <c>MultipartBodyLengthLimit</c> is what <c>ReadFormAsync</c>
    /// enforces per section. The form reader's own default is 128 MB — far above any body this
    /// route is allowed to receive — so setting only the first would leave a limit nobody chose
    /// sitting behind one somebody did.
    /// </remarks>
    [Fact]
    public void The_direct_upload_route_bounds_both_the_body_and_the_form_reader()
    {
        Limit(DirectRoute, "POST").Should().Be(
            MediaLimits.DirectUploadCeiling.Bytes + Headroom);

        Endpoint(DirectRoute, "POST").Metadata
            .GetMetadata<IFormOptionsMetadata>()!
            .MultipartBodyLengthLimit
            .Should().Be(
                MediaLimits.DirectUploadCeiling.Bytes,
                "the form reader's default is 128 MB, which would outrank the body limit beside it");
    }

    /// <summary>
    /// <b>No byte-carrying route is left on the host default.</b>
    /// </summary>
    /// <remarks>
    /// The two routes above are the only ones that read a body as bytes rather than as JSON. Named
    /// individually so that a third — a future avatar or attachment route — fails here rather than
    /// inheriting a 30 MB ceiling nobody noticed, which is how these two got theirs.
    /// </remarks>
    [Fact]
    public void Exactly_these_two_routes_declare_a_byte_ceiling()
    {
        var declared = _endpoints
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
                is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Distinct()
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

        declared.Should().BeEquivalentTo(
            [ChunkRoute, DirectRoute],
            "a new route that carries bytes needs a ceiling chosen for its payload, and the way "
            + "to notice is for this to fail");
    }

    private long? Limit(string route, string method) =>
        Endpoint(route, method).Metadata
            .GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

    private RouteEndpoint Endpoint(string route, string method)
    {
        var endpoint = _endpoints.SingleOrDefault(candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal)
            && (candidate.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()
                    ?.HttpMethods ?? [])
                .Contains(method, StringComparer.OrdinalIgnoreCase));

        endpoint.Should().NotBeNull(
            "{0} {1} must exist for this rule to be asserting anything", method, route);

        return endpoint!;
    }
}
