using Diaspora.Api.Middleware;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Media.Domain.Policies;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The request-body ceilings: one global default, and the routes allowed past it (BSEC-006).
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

    private const string GuideRoute = GuidesModuleMetadata.RoutePrefix + "/{guideId:guid}";

    private const string GuideDraftRoute = GuideRoute + "/draft";

    private const string NoticeRoute =
        GuidesModuleMetadata.IntelligenceRoutePrefix + "/notices";

    private const string NoticeByIdRoute = NoticeRoute + "/{noticeId:guid}";

    private readonly List<RouteEndpoint> _endpoints;

    private readonly RequestBodyLimitOptions _bodyLimit;

    private readonly KestrelServerOptions _kestrel;

    public RequestBodyLimitTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.EnsureDatabaseCreated();

        using var scope = factory.Services.CreateScope();

        _endpoints = [.. scope.ServiceProvider
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()];

        _bodyLimit = scope.ServiceProvider
            .GetRequiredService<IOptions<RequestBodyLimitOptions>>().Value;

        _kestrel = scope.ServiceProvider
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
    }

    /// <summary>
    /// <b>There is a global ceiling at all</b>, which is the whole of BSEC-006.
    /// </summary>
    /// <remarks>
    /// Before this, no code or configuration set <c>MaxRequestBodySize</c>, so every route on
    /// the platform inherited Kestrel's 30,000,000-byte default. The finding is not that the
    /// default is wrong; it is that nobody chose it.
    /// </remarks>
    [Fact]
    public void The_host_declares_a_global_body_ceiling()
    {
        _bodyLimit.MaxBodyBytes.Should().Be(
            2 * 1024 * 1024,
            "the configured default, which appsettings.json states explicitly rather than "
            + "leaving to the framework");

        _bodyLimit.MaxBodyBytes.Should().BeLessThan(
            30_000_000,
            "a value at or above Kestrel's own default would read as a deliberate choice "
            + "while leaving the bound no tighter than it was");
    }

    /// <summary>
    /// <b>And Kestrel actually receives it.</b>
    /// </summary>
    /// <remarks>
    /// The option and the server limit are two different objects, and binding one says nothing
    /// about the other. This resolves <see cref="KestrelServerOptions"/> from the composed
    /// container and reads the limit back, which runs the <c>Configure</c> delegate that does
    /// the mapping — provable without Kestrel ever serving a request, which it does not do
    /// under <c>TestServer</c>. A configured option nothing consumes is the shape of defect
    /// this programme has already found twice.
    /// </remarks>
    [Fact]
    public void Kestrel_is_configured_from_that_option()
    {
        _kestrel.Limits.MaxRequestBodySize.Should().Be(
            _bodyLimit.MaxBodyBytes,
            "binding the option is not the same as applying it");
    }

    /// <summary>
    /// <b>The notice ceiling tracks the content caps it exists for.</b>
    /// </summary>
    /// <remarks>
    /// An official notice carries up to <c>MaxNoticeLocalizations</c> translations of content
    /// bounded by <c>MaxNoticeContent</c>, plus the notice's own. Asserted as the relationship
    /// rather than as 4,262,144, so that raising either cap without raising the ceiling fails
    /// here instead of becoming a 413 for an editor.
    /// </remarks>
    [Fact]
    public void The_notice_routes_are_bounded_by_the_declared_content_caps()
    {
        var expected =
            ((IntelligenceLimits.MaxNoticeLocalizations + 1L)
                * IntelligenceLimits.MaxNoticeContent
                * 4)
            + Headroom;

        Limit(NoticeRoute, "POST").Should().Be(expected);
        Limit(NoticeByIdRoute, "PUT").Should().Be(expected);
    }

    /// <summary>
    /// <b>The guide ceiling is deliberately below what GuideLimits would allow</b>, and this
    /// records that the gap is intended rather than an oversight.
    /// </summary>
    /// <remarks>
    /// <c>MaxSections</c> × <c>MaxBlocksPerSection</c> × <c>MaxBlockLength</c> is four hundred
    /// million characters, and <c>SaveGuideDraftRequest</c> carries the whole section list, so
    /// that really is reachable in one body. Those caps bound an aggregate, not a request.
    /// Deriving a transport ceiling from them would reproduce the unbounded body the finding is
    /// about, so this one is a policy number — and the assertion that matters is that it stays
    /// far below the aggregate rather than that it equals any particular figure.
    /// </remarks>
    [Fact]
    public void The_guide_authoring_ceiling_is_far_below_the_aggregate_maximum()
    {
        var aggregateCharacters =
            (long)GuideLimits.MaxSections
            * GuideLimits.MaxBlocksPerSection
            * GuideLimits.MaxBlockLength;

        var ceiling = Limit(GuideRoute, "PUT");

        ceiling.Should().Be(Limit(GuideDraftRoute, "PUT"), "both are the same editor saving");

        ceiling.Should().BeLessThan(
            aggregateCharacters / 10,
            "a transport ceiling derived from the aggregate caps would be four hundred "
            + "megabytes, which is the unbounded body this finding is about");

        ceiling.Should().BeGreaterThan(
            _bodyLimit.MaxBodyBytes,
            "the override exists to raise the ceiling for authoring, not to lower it");
    }

    /// <summary>
    /// <b>No route declares a ceiling tighter than the global default.</b>
    /// </summary>
    /// <remarks>
    /// An override that lowers the limit is legal and would be invisible: the route would
    /// simply start refusing bodies the rest of the platform accepts. Every override here is
    /// meant to raise it, so this states that and fails if one ever does the opposite.
    /// </remarks>
    [Fact]
    public void Every_declared_ceiling_raises_rather_than_lowers_the_default()
    {
        var lowered = _endpoints
            .Select(endpoint => new
            {
                Route = endpoint.RoutePattern.RawText!,
                Limit = endpoint.Metadata
                    .GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize,
            })
            .Where(row => row.Limit is not null && row.Limit < _bodyLimit.MaxBodyBytes)
            .Select(row => row.Route + " = " + row.Limit)
            .Distinct()
            .ToList();

        lowered.Should().BeEmpty();
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
    /// <b>The set of routes allowed past the global ceiling is exactly this one.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two read a body as bytes (media upload); four carry JSON whose own content caps exceed
    /// the global default (guide authoring, official notices). Named individually so that a
    /// seventh fails here rather than quietly inheriting a larger ceiling, which is how the
    /// first two came to have one nobody had chosen.
    /// </para>
    /// <para>
    /// This rule already earned itself: adding the four below turned it red, which is the only
    /// reason the list in it is accurate rather than aspirational.
    /// </para>
    /// </remarks>
    [Fact]
    public void Exactly_these_six_routes_declare_a_byte_ceiling()
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
            [ChunkRoute, DirectRoute, GuideRoute, GuideDraftRoute, NoticeRoute, NoticeByIdRoute],
            "a new route that needs more than the global default needs a ceiling chosen for its "
            + "payload, and the way to notice is for this to fail");
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
