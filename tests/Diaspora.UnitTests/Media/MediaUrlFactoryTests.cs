using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// Whether the URL that comes back is one a client can actually fetch.
/// </summary>
/// <remarks>
/// <para>
/// The third fault in UAT-121, and the quietest. Media emitted <c>/api/v1/media/{id}/content</c>
/// with no origin, because <c>PublicBaseUrl</c> was never set on the deployment. The mobile client
/// holds a base of <c>https://host/v1</c>, so joining the two produced
/// <c>/v1/api/v1/media/…</c> — a 404 that looks like a missing photograph rather than a
/// malformed URL.
/// </para>
/// <para>
/// Configuration alone would have fixed the deployment and left the trap set for the next one, so
/// the behaviour under test is the fallback: absent an explicit base, the URL is built from the
/// request's own origin.
/// </para>
/// </remarks>
public sealed class MediaUrlFactoryTests
{
    [Fact]
    public void The_configured_base_wins()
    {
        // A deployment behind a CDN or a custom domain knows its public name better than any
        // single request does.
        var url = Factory(publicBaseUrl: "https://cdn.diaspora.app/")
            .ForOriginal(PublicAsset());

        url.Should().StartWith("https://cdn.diaspora.app/api/v1/media/");
    }

    [Fact]
    public void Without_a_configured_base_the_requests_own_origin_is_used()
    {
        var url = Factory(request: Request("diaspora-api.fly.dev", "https"))
            .ForOriginal(PublicAsset());

        url.Should().StartWith("https://diaspora-api.fly.dev/api/v1/media/");
    }

    [Fact]
    public void The_forwarded_scheme_is_honoured()
    {
        // Fly terminates TLS and speaks http to the container. Reading the raw scheme would make
        // every image URL mixed content, which a mobile client refuses outright — so the host
        // calls UseForwardedHeaders and this reads what that produced.
        var url = Factory(request: Request("diaspora-api.fly.dev", "https"))
            .ForOriginal(PublicAsset());

        url.Should().NotStartWith("http://");
    }

    [Fact]
    public void With_no_request_at_all_the_path_stays_relative()
    {
        // A background worker composing a notification has no request to read an origin from.
        // An invented origin would be worse than an obviously incomplete one.
        var url = Factory().ForOriginal(PublicAsset());

        url.Should().StartWith("/api/v1/media/");
    }

    [Fact]
    public void A_public_asset_gets_a_bare_cacheable_route()
    {
        var url = Factory(publicBaseUrl: "https://host").ForOriginal(PublicAsset());

        url.Should().NotContain("sig=");
    }

    [Fact]
    public void A_private_asset_gets_a_signed_one()
    {
        var url = Factory(publicBaseUrl: "https://host").ForOriginal(PrivateAsset());

        url.Should().Contain("sig=").And.Contain("exp=");
    }

    [Fact]
    public void A_signed_link_reports_the_expiry_that_is_inside_it()
    {
        // The reason this returns a pair rather than a string. The signed-url endpoint used to
        // build the route itself and mint a second token for the response's `expiresAt`, so the
        // advertised expiry was not the one in the link.
        var link = Factory(publicBaseUrl: "https://host")
            .ForSignedVariant(PrivateAsset(), MediaVariantPurpose.Original, audience: null);

        link.Url.Should().Contain($"exp={link.ExpiresAt.ToUnixTimeSeconds()}");
    }

    [Fact]
    public void A_photographs_link_is_not_bound_to_the_member_who_asked_for_it()
    {
        // An <img> tag sends no Authorization header, so an audience-bound link is one the image
        // loader can never redeem. Worse than useless: a rejected signature stops the request
        // rather than falling through to the ordinary access check.
        var url = Factory(publicBaseUrl: "https://host", caller: UserId.New())
            .ForOriginal(PrivateAsset());

        url.Should().NotContain("aud=");
    }

    [Fact]
    public void A_verification_documents_link_still_is()
    {
        // Fetched by code that holds a bearer token, where binding is what stops a link pasted
        // into a support ticket working for whoever reads it.
        var url = Factory(publicBaseUrl: "https://host", caller: UserId.New())
            .ForOriginal(NewAsset(MediaKind.VerificationDocument));

        url.Should().Contain("aud=");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static MediaUrlFactory Factory(
        string publicBaseUrl = "", HttpContext? request = null, UserId? caller = null)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch.AddYears(56));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(caller);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(request);

        return new MediaUrlFactory(
            new SignedUrlFactory(
                Options.Create(new MediaSecurityOptions { SigningKey = "a-test-signing-key" }),
                clock),
            currentUser,
            accessor,
            Options.Create(new MediaStorageOptions { PublicBaseUrl = publicBaseUrl }));
    }

    private static HttpContext Request(string host, string scheme)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Scheme = scheme;
        return context;
    }

    private static MediaAsset PrivateAsset() => NewAsset();

    private static MediaAsset PublicAsset()
    {
        var asset = NewAsset();
        asset.ChangeVisibility(MediaVisibility.Public);
        return asset;
    }

    private static MediaAsset NewAsset(MediaKind kind = MediaKind.Image) =>
        MediaAsset.Record(
            MediaId.New(),
            UserId.New(),
            kind,
            StorageLocation.Create(
                "filesystem",
                "diaspora-media",
                StorageKey.ForOriginal(
                    kind,
                    Guid.NewGuid(),
                    FileExtension.TryCreate("jpg").Value,
                    DateTimeOffset.UnixEpoch)),
            MediaContentType.Create("image/jpeg"),
            SafeFileName.Create("photo.jpg"),
            ByteSize.FromBytes(1024),
            ContentHash.Compute("photo"u8)).Value;
}
