using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Media &amp; Digital Asset Management Engine, driven over real HTTP through the same host
/// every other suite uses.
/// <para>
/// Three things are under test at once. The module's behaviour. That a module with nine tables,
/// twelve permissions and twenty routes composed into the platform through nothing but
/// contributor interfaces and one project reference. And the thing no previous module could
/// show: that a module can <b>fulfil a platform contract another frozen module has been
/// waiting on since B3</b> — Trust's <c>DocumentLinkFactory</c> starts minting real document
/// links here without a line of Trust changing.
/// </para>
/// </summary>
public sealed class MediaModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A one-pixel PNG. Real bytes, so the signature check has something to read.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MediaModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ──────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: a
    /// Media-owned gate turns up in Identity's <c>/access/features</c> response, which the
    /// Media module never touched.
    /// </summary>
    [Fact]
    public async Task The_media_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("media.upload");
        names.Should().Contain("media.upload.photo");
        names.Should().Contain("media.galleries");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_media_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Media", "the kernel discovered the module by assembly scan");
    }

    // ── Access control ───────────────────────────────────────────────────

    [Fact]
    public async Task Uploading_refuses_an_anonymous_caller()
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new { kind = "image", contentType = "image/jpeg", fileName = "a.jpg", sizeBytes = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_media_library_is_not_readable_without_a_token()
    {
        var response = await HttpClient.GetAsync("/api/v1/media");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The content route is deliberately anonymous — a public listing photograph must render
    /// for a signed-out visitor, and an <c>&lt;img src&gt;</c> cannot send a bearer token. It
    /// answers "not found" rather than "unauthorised" for an unknown identifier, which is what
    /// stops it being an enumeration oracle.
    /// </summary>
    [Fact]
    public async Task The_content_route_is_anonymous_and_does_not_confirm_what_exists()
    {
        var response = await HttpClient.GetAsync($"/api/v1/media/{Guid.NewGuid()}/content");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Capabilities ─────────────────────────────────────────────────────

    /// <summary>
    /// The endpoint that lets a client stop guessing. It reports what is actually registered,
    /// so on this host — which installs no scanner and no codec — it says so.
    /// </summary>
    [Fact]
    public async Task The_capabilities_endpoint_reports_this_deployments_real_limits()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync("/api/v1/media/capabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var data = document.RootElement.GetProperty("data");

        data.GetProperty("resumableUploads").GetBoolean().Should().BeTrue();

        // No scanner is registered in the test host, and the module says so rather than
        // implying uploads are scanned.
        data.GetProperty("malwareScanningEnabled").GetBoolean().Should().BeFalse();

        data.GetProperty("maxUploadBytes").GetProperty("verificationDocument")
            .GetInt64().Should().Be(5 * 1024 * 1024);
    }

    // ── The upload lifecycle ─────────────────────────────────────────────

    [Fact]
    public async Task A_direct_upload_produces_a_readable_asset()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();

        media.GetProperty("kind").GetString().Should().Be("image");
        media.GetProperty("contentType").GetString().Should().Be("image/png");
        media.GetProperty("sizeBytes").GetInt64().Should().Be(OnePixelPng.Length);

        // Header-based extraction works with no codec installed, which is the whole point of
        // implementing it in managed code.
        media.GetProperty("metadata").GetProperty("width").GetInt32().Should().Be(1);
        media.GetProperty("metadata").GetProperty("height").GetInt32().Should().Be(1);

        // Published so Trust can ask the reuse question of the bytes rather than of a URL.
        media.GetProperty("metadata").GetProperty("contentHash").GetString()
            .Should().NotBeNullOrEmpty();

        var mediaId = media.GetProperty("mediaId").GetGuid();

        var read = await HttpClient.GetAsync($"/api/v1/media/{mediaId}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Private by default, always. The opposite default — public until somebody remembers to
    /// restrict it — is how immigration documents end up indexed.
    /// </summary>
    [Fact]
    public async Task An_uploaded_asset_is_private_and_needs_a_signed_link()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();

        media.GetProperty("visibility").GetString().Should().Be("private");
        media.GetProperty("requiresSignedAccess").GetBoolean().Should().BeTrue();
        media.GetProperty("url").GetString().Should().Contain("sig=");
    }

    /// <summary>
    /// The disguise the signature check exists to catch: a ZIP archive declared as a JPEG.
    /// </summary>
    [Fact]
    public async Task An_archive_disguised_as_an_image_is_refused()
    {
        await AuthenticateAsync();

        var zip = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00 };

        var response = await PostDirectAsync(zip, "image/jpeg", "photo.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("media.upload.signature-mismatch");
    }

    [Fact]
    public async Task A_type_outside_the_allow_list_is_refused()
    {
        await AuthenticateAsync();

        var response = await PostDirectAsync(
            OnePixelPng, "application/x-msdownload", "thing.exe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The full chunked path: open, send parts, complete.</summary>
    [Fact]
    public async Task A_chunked_upload_resumes_and_completes()
    {
        await AuthenticateAsync();

        // Above the 8 MB direct ceiling, so the server chooses the chunked protocol.
        var payload = new byte[9 * 1024 * 1024];
        Random.Shared.NextBytes(payload);

        // Real PNG magic bytes, so the signature check on part one passes.
        OnePixelPng.AsSpan(0, 8).CopyTo(payload);

        var start = await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new
            {
                kind = "image",
                contentType = "image/png",
                fileName = "large.png",
                sizeBytes = payload.Length
            });

        start.StatusCode.Should().Be(HttpStatusCode.Created);

        var session = await ReadDataAsync(start);
        var sessionId = session.GetProperty("sessionId").GetGuid();
        var totalChunks = session.GetProperty("totalChunks").GetInt32();
        var chunkSize = (int)session.GetProperty("chunkSizeBytes").GetInt64();

        session.GetProperty("protocol").GetString().Should().Be("chunked");
        session.GetProperty("status").GetString().Should().Be("queued");
        totalChunks.Should().BeGreaterThan(1);

        for (var number = 1; number <= totalChunks; number++)
        {
            var offset = (number - 1) * chunkSize;
            var length = Math.Min(chunkSize, payload.Length - offset);

            using var content = new ByteArrayContent(payload, offset, length);

            var part = await HttpClient.PutAsync(
                $"/api/v1/media/uploads/{sessionId}/chunks/{number}", content);

            part.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Mid-flight resumption: the client can ask which parts the server holds.
        var resumed = await ReadDataAsync(
            await HttpClient.GetAsync($"/api/v1/media/uploads/{sessionId}"));

        resumed.GetProperty("receivedChunks").GetArrayLength().Should().Be(totalChunks);
        resumed.GetProperty("progressPercent").GetInt32().Should().Be(100);

        var completed = await HttpClient.PostAsync(
            $"/api/v1/media/uploads/{sessionId}/complete", content: null);

        completed.StatusCode.Should().Be(HttpStatusCode.OK);

        var final = await ReadDataAsync(completed);
        final.GetProperty("status").GetString().Should().Be("completed");
        final.GetProperty("media").GetProperty("sizeBytes").GetInt64()
            .Should().Be(payload.Length);
    }

    /// <summary>
    /// Idempotency is what makes the client's retry safe: a part whose response was lost gets
    /// sent again, and the second attempt must not be a conflict.
    /// </summary>
    [Fact]
    public async Task Re_sending_a_part_is_not_an_error()
    {
        await AuthenticateAsync();

        var payload = new byte[9 * 1024 * 1024];
        OnePixelPng.AsSpan(0, 8).CopyTo(payload);

        var session = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new
            {
                kind = "image",
                contentType = "image/png",
                fileName = "large.png",
                sizeBytes = payload.Length
            }));

        var sessionId = session.GetProperty("sessionId").GetGuid();
        var chunkSize = (int)session.GetProperty("chunkSizeBytes").GetInt64();

        using var first = new ByteArrayContent(payload, 0, chunkSize);
        using var again = new ByteArrayContent(payload, 0, chunkSize);

        (await HttpClient.PutAsync($"/api/v1/media/uploads/{sessionId}/chunks/1", first))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.PutAsync($"/api/v1/media/uploads/{sessionId}/chunks/1", again))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var state = await ReadDataAsync(
            await HttpClient.GetAsync($"/api/v1/media/uploads/{sessionId}"));

        state.GetProperty("receivedChunks").GetArrayLength().Should().Be(1);
    }

    /// <summary>An incomplete session cannot produce an asset.</summary>
    [Fact]
    public async Task An_incomplete_upload_cannot_be_completed()
    {
        await AuthenticateAsync();

        var session = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new
            {
                kind = "image",
                contentType = "image/png",
                fileName = "large.png",
                sizeBytes = 9 * 1024 * 1024
            }));

        var sessionId = session.GetProperty("sessionId").GetGuid();

        var completed = await HttpClient.PostAsync(
            $"/api/v1/media/uploads/{sessionId}/complete", content: null);

        completed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>The client's <c>'paused'</c> state, over real HTTP.</summary>
    [Fact]
    public async Task An_upload_can_be_paused_and_resumed()
    {
        await AuthenticateAsync();

        var session = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new
            {
                kind = "image",
                contentType = "image/png",
                fileName = "large.png",
                sizeBytes = 9 * 1024 * 1024
            }));

        var sessionId = session.GetProperty("sessionId").GetGuid();

        var paused = await ReadDataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/media/uploads/{sessionId}/state", new { state = "paused" }));

        paused.GetProperty("status").GetString().Should().Be("paused");

        var resumed = await ReadDataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/media/uploads/{sessionId}/state", new { state = "uploading" }));

        resumed.GetProperty("status").GetString().Should().Be("queued");
    }

    /// <summary>
    /// A member cannot touch another member's upload, and the refusal is a not-found rather
    /// than a forbidden — telling a caller a session exists is a free oracle.
    /// </summary>
    [Fact]
    public async Task One_member_cannot_see_anothers_upload()
    {
        await AuthenticateAsync();

        var session = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/uploads",
            new
            {
                kind = "image",
                contentType = "image/png",
                fileName = "mine.png",
                sizeBytes = 9 * 1024 * 1024
            }));

        var sessionId = session.GetProperty("sessionId").GetGuid();

        await AuthenticateAsync();

        var response = await HttpClient.GetAsync($"/api/v1/media/uploads/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Content delivery ─────────────────────────────────────────────────

    /// <summary>
    /// The bytes come back byte-for-byte, and the response carries the caching posture its
    /// visibility demands: <c>no-store</c> for a private asset, so no shared cache keeps it.
    /// </summary>
    [Fact]
    public async Task Content_is_served_intact_and_is_not_cached_while_private()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();
        var mediaId = media.GetProperty("mediaId").GetGuid();

        var response = await HttpClient.GetAsync($"/api/v1/media/{mediaId}/content");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().Equal(OnePixelPng);

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    /// <summary>
    /// Belt and braces against a browser second-guessing a declared content type — the header
    /// that turns a mislabelled HTML upload from stored XSS into a download.
    /// </summary>
    [Fact]
    public async Task Content_responses_forbid_content_type_sniffing()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();
        var mediaId = media.GetProperty("mediaId").GetGuid();

        var response = await HttpClient.GetAsync($"/api/v1/media/{mediaId}/content");

        response.Headers.TryGetValues("X-Content-Type-Options", out var values)
            .Should().BeTrue();

        values!.Should().Contain("nosniff");
    }

    /// <summary>
    /// The entity tag is derived from the content hash, so it is stable across instances and
    /// survives a storage migration — and a conditional request costs nothing to answer.
    /// </summary>
    [Fact]
    public async Task A_conditional_request_is_answered_without_the_body()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();
        var mediaId = media.GetProperty("mediaId").GetGuid();

        var first = await HttpClient.GetAsync($"/api/v1/media/{mediaId}/content");
        var tag = first.Headers.ETag;

        tag.Should().NotBeNull();

        using var conditional = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/media/{mediaId}/content");

        conditional.Headers.IfNoneMatch.Add(tag!);

        var second = await HttpClient.SendAsync(conditional);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    // ── Signed access ────────────────────────────────────────────────────

    /// <summary>
    /// <b>This test used to stop one assertion short, and that gap was UAT-121.</b>
    /// </summary>
    /// <remarks>
    /// It asserted that the minted URL contained <c>sig=</c> and never fetched it. A link that is
    /// shaped like a signed link is not a link that opens anything — and this one did not: the
    /// handler verified the signature and then ran the full access check anyway, which re-derives
    /// the caller from the request. On a device that request is an <c>&lt;img&gt;</c> load with no
    /// <c>Authorization</c> header, so it answered 403 to every signed URL, including one the
    /// owner had just minted for their own photograph.
    /// </remarks>
    [Fact]
    public async Task A_signed_link_opens_a_private_asset_and_reports_its_expiry()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();
        var mediaId = media.GetProperty("mediaId").GetGuid();

        var signed = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/signed-url", new { variant = "original" }));

        signed.GetProperty("url").GetString().Should().Contain("sig=");
        signed.GetProperty("expiresAt").GetDateTimeOffset()
            .Should().BeAfter(DateTimeOffset.UtcNow);

        // The assertion that was missing. No token on this client, exactly as an image loader
        // has none.
        using var imageLoader = Factory.CreateClient();

        var fetched = await imageLoader.GetAsync(
            PathAndQueryOf(signed.GetProperty("url").GetString()!));

        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await fetched.Content.ReadAsByteArrayAsync()).Should().Equal(OnePixelPng);
    }

    /// <summary>
    /// The URL is absolute, because the client that has to fetch it is not on this origin.
    /// </summary>
    /// <remarks>
    /// The third of UAT-121's faults. With no <c>Media:Storage:PublicBaseUrl</c> configured the
    /// endpoint returned a bare <c>/api/v1/media/…</c> path; the mobile client holds a base of
    /// <c>https://host/v1</c> and joining the two produced <c>/v1/api/v1/media/…</c>, a 404 that
    /// reads as a missing photograph rather than a malformed URL.
    /// </remarks>
    [Fact]
    public async Task A_signed_link_is_absolute_so_a_client_on_another_origin_can_resolve_it()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var signed = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/signed-url", new { variant = "original" }));

        var url = signed.GetProperty("url").GetString();

        Uri.TryCreate(url, UriKind.Absolute, out var absolute).Should().BeTrue(
            "a relative media URL cannot be resolved by a client that is not a browser on this "
            + $"origin, and this one was '{url}'");

        absolute!.Scheme.Should().StartWith("http");
        absolute.Host.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The expiry the response advertises is the one inside the link.
    /// </summary>
    /// <remarks>
    /// The endpoint used to build the route itself and mint a second token for the response's
    /// <c>expiresAt</c>, so the two could disagree — a client caching by the advertised expiry
    /// would keep using a link that had already stopped working.
    /// </remarks>
    [Fact]
    public async Task The_advertised_expiry_is_the_one_the_link_carries()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var signed = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/signed-url", new { variant = "original" }));

        var url = new Uri(signed.GetProperty("url").GetString()!);
        var advertised = signed.GetProperty("expiresAt").GetDateTimeOffset();

        var carried = System.Web.HttpUtility.ParseQueryString(url.Query)["exp"];

        carried.Should().Be(advertised.ToUnixTimeSeconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Serving a signed link is not a bypass: an unreadable asset stays unreadable.
    /// </summary>
    /// <remarks>
    /// The counterweight to the test above. Loosening redemption so an image tag can use a link
    /// must not loosen what a link is worth — a signature still buys only the identity it was
    /// minted for, and never a file the platform would refuse to serve at all.
    /// </remarks>
    [Fact]
    public async Task A_link_for_one_asset_does_not_open_another()
    {
        await AuthenticateAsync();

        var first = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();
        var second = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var signed = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            $"/api/v1/media/{first}/signed-url", new { variant = "original" }));

        // The signature covers the media id, so replaying it against a different asset fails.
        var replayed = PathAndQueryOf(signed.GetProperty("url").GetString()!)
            .Replace(first.ToString(), second.ToString(), StringComparison.OrdinalIgnoreCase);

        using var imageLoader = Factory.CreateClient();

        var response = await imageLoader.GetAsync(replayed);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    /// <summary>The path a client on this origin would request, from an absolute URL.</summary>
    private static string PathAndQueryOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var absolute) ? absolute.PathAndQuery : url;

    /// <summary>
    /// A forged signature is refused. The signature covers every field, so nothing in the
    /// token can be edited without invalidating it.
    /// </summary>
    [Fact]
    public async Task A_forged_signature_is_refused()
    {
        await AuthenticateAsync();

        var media = await UploadOnePixelPngAsync();
        var mediaId = media.GetProperty("mediaId").GetGuid();

        using var anonymous = Factory.CreateClient();

        var expiry = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        var response = await anonymous.GetAsync(
            $"/api/v1/media/{mediaId}/content?v=original&exp={expiry}&sig=not-a-signature");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Collections ──────────────────────────────────────────────────────

    /// <summary>
    /// Ordering is the thing the client cannot do for itself: its galleries hold order as array
    /// position in component state, which is lost on any refetch.
    /// </summary>
    [Fact]
    public async Task A_gallery_holds_an_explicit_order_that_survives_a_refetch()
    {
        await AuthenticateAsync();

        var first = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();
        var second = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var listingId = Guid.NewGuid();

        var collection = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/collections",
            new
            {
                kind = "gallery",
                ownerType = "housingListing",
                ownerId = listingId,
                mediaIds = new[] { first, second }
            }));

        var collectionId = collection.GetProperty("collectionId").GetGuid();

        collection.GetProperty("items").GetArrayLength().Should().Be(2);
        collection.GetProperty("items")[0].GetProperty("mediaId").GetGuid()
            .Should().Be(first);

        // "Make this the cover photo."
        await HttpClient.PutAsync(
            $"/api/v1/media/collections/{collectionId}/primary/{second}", content: null);

        var reread = await ReadDataAsync(
            await HttpClient.GetAsync($"/api/v1/media/collections/{collectionId}"));

        reread.GetProperty("items")[0].GetProperty("mediaId").GetGuid().Should().Be(second);
    }

    /// <summary>
    /// Idempotent by owner and kind, so a retried request does not leave a listing with two
    /// galleries.
    /// </summary>
    [Fact]
    public async Task Creating_the_same_gallery_twice_returns_the_same_one()
    {
        await AuthenticateAsync();

        var listingId = Guid.NewGuid();

        var request = new
        {
            kind = "gallery",
            ownerType = "housingListing",
            ownerId = listingId,
            mediaIds = Array.Empty<Guid>()
        };

        var first = await ReadDataAsync(
            await HttpClient.PostAsJsonAsync("/api/v1/media/collections", request));

        var second = await ReadDataAsync(
            await HttpClient.PostAsJsonAsync("/api/v1/media/collections", request));

        second.GetProperty("collectionId").GetGuid()
            .Should().Be(first.GetProperty("collectionId").GetGuid());
    }

    /// <summary>
    /// The provider wizard renders exactly four slots, and the collection refuses a fifth —
    /// the cardinality is a property of the kind, not of each caller.
    /// </summary>
    [Fact]
    public async Task A_provider_gallery_refuses_a_fifth_photograph()
    {
        await AuthenticateAsync();

        var providerId = Guid.NewGuid();

        var collection = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/collections",
            new
            {
                kind = "providerGallery",
                ownerType = "provider",
                ownerId = providerId,
                mediaIds = Array.Empty<Guid>()
            }));

        var collectionId = collection.GetProperty("collectionId").GetGuid();

        collection.GetProperty("capacity").GetInt32().Should().Be(4);

        HttpResponseMessage? last = null;

        for (var index = 0; index < 5; index++)
        {
            var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

            last = await HttpClient.PostAsJsonAsync(
                $"/api/v1/media/collections/{collectionId}/items", new { mediaId });
        }

        last!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await last.Content.ReadAsStringAsync();
        body.Should().Contain("media.collection.full");
    }

    // ── Attachment and ownership ─────────────────────────────────────────

    [Fact]
    public async Task An_asset_can_be_claimed_by_an_owning_entity()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();
        var listingId = Guid.NewGuid();

        var attached = await ReadDataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/media/attachments",
            new
            {
                mediaId,
                ownerType = "housingListing",
                ownerId = listingId,
                role = "cover"
            }));

        var reference = attached.GetProperty("reference");

        reference.GetProperty("ownerType").GetString().Should().Be("housingListing");
        reference.GetProperty("ownerId").GetGuid().Should().Be(listingId);
        reference.GetProperty("role").GetString().Should().Be("cover");
    }

    /// <summary>
    /// Any member being able to attach someone else's photograph to their own listing is an
    /// image-theft vector that costs nothing to close here.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_attach_someone_elses_asset()
    {
        await AuthenticateAsync();
        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/media/attachments",
            new
            {
                mediaId,
                ownerType = "housingListing",
                ownerId = Guid.NewGuid(),
                role = "cover"
            });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    // ── Processing diagnostics ───────────────────────────────────────────

    /// <summary>
    /// The honest answer on a deployment with no codec: the job ran, some steps are recorded
    /// as unsupported, and the asset is fine. That is a state an operator can read, and it is
    /// distinct from a failure.
    /// </summary>
    [Fact]
    public async Task A_missing_codec_is_reported_as_unsupported_rather_than_failed()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var job = await ReadDataAsync(
            await HttpClient.GetAsync($"/api/v1/media/{mediaId}/processing"));

        var statuses = job.GetProperty("tasks")
            .EnumerateArray()
            .Select(task => new
            {
                Operation = task.GetProperty("operation").GetString(),
                Status = task.GetProperty("status").GetString()
            })
            .ToList();

        statuses.Should().Contain(task =>
            task.Operation == "extractMetadata" && task.Status == "succeeded");

        // No thumbnail generator is registered here, and the job says so by name.
        statuses.Should().Contain(task =>
            task.Operation == "generateThumbnails" && task.Status == "unsupported");

        // No scanner either, so the scan is skipped rather than reported clean.
        statuses.Should().Contain(task => task.Operation == "scanForMalware");
    }

    /// <summary>
    /// Asking for a rendition nothing can produce is refused with a specific code rather than
    /// queueing work nothing will ever perform.
    /// </summary>
    [Fact]
    public async Task Requesting_a_variant_no_processor_can_make_is_refused_clearly()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        var response = await HttpClient.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/variants", new { variant = "thumbnail" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("media.transformation.unavailable");
    }

    // ── Deduplication ────────────────────────────────────────────────────

    /// <summary>
    /// The server-side answer to the client's URL-based reuse check, which any re-upload
    /// defeats.
    /// </summary>
    [Fact]
    public async Task The_same_bytes_are_findable_by_their_digest()
    {
        await AuthenticateAsync();

        var first = await UploadOnePixelPngAsync();
        await UploadOnePixelPngAsync();

        var hash = first.GetProperty("metadata").GetProperty("contentHash").GetString();

        var matches = await ReadDataAsync(
            await HttpClient.GetAsync($"/api/v1/media/by-hash/{hash}"));

        matches.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    // ── Deletion ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_deleted_asset_stops_being_readable()
    {
        await AuthenticateAsync();

        var mediaId = (await UploadOnePixelPngAsync()).GetProperty("mediaId").GetGuid();

        (await HttpClient.DeleteAsync($"/api/v1/media/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.GetAsync($"/api/v1/media/{mediaId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"/api/v1/media/{mediaId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task AuthenticateAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);
    }

    private async Task<JsonElement> UploadOnePixelPngAsync()
    {
        var response = await PostDirectAsync(OnePixelPng, "image/png", "pixel.png");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await ReadDataAsync(response);
    }

    private async Task<HttpResponseMessage> PostDirectAsync(
        byte[] bytes, string contentType, string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);

        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        form.Add(file, "file", fileName);
        form.Add(new StringContent("image"), "kind");

        return await HttpClient.PostAsync("/api/v1/media/uploads/direct", form);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
