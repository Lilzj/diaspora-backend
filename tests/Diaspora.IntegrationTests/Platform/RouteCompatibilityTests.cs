using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The <c>/v1</c> versus <c>/api/v1</c> question, answered against a real host.
/// <para>
/// <b>What was actually wrong.</b> The React Native client's <c>apiClient</c> is created with
/// <c>baseURL: 'https://api.diaspora.app/v1'</c> and every call it makes is relative —
/// <c>/conversations</c>, <c>/notifications</c>, <c>/marketplace/listings</c>. Sixteen modules serve
/// <c>/api/v1/{module}</c> and an architecture test asserts each prefix. Every request the client
/// made would therefore have 404'd, on every surface, and the ticket would have read "the whole app
/// is broken" rather than "one module is on the wrong prefix". B13 filed it as a gateway concern;
/// B13.2 restated it as V-1 and declined to move one module onto a different prefix, correctly. It
/// had no owner, and <c>docker/docker-compose.yml</c> shows why nobody could point at one: there is
/// no reverse proxy in this repository at all.
/// </para>
/// <para>
/// <b>What must stay true after closing it.</b> The canonical path is unchanged, the alias reaches
/// the same endpoint rather than a copy of it, a path that exists under neither still 404s, and the
/// non-versioned surfaces — health and the manifest — are untouched.
/// </para>
/// </summary>
public sealed class RouteCompatibilityTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RouteCompatibilityTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// The canonical prefix is the platform's and did not move. Asserted first, because a
    /// compatibility layer that worked by relocating the real routes would pass every other test
    /// here and break every deployment already calling the documented ones.
    /// </summary>
    [Fact]
    public async Task The_canonical_prefix_still_serves_the_platform()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        (await HttpClient.GetAsync("/api/v1/conversations")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>The claim that matters: the alias reaches the same endpoint, not a copy of it.</b> A
    /// conversation opened through <c>/v1</c> and read back through <c>/api/v1</c> is one row in one
    /// table written by one handler. Two sets of routes could pass a status-code comparison and
    /// still be two different things.
    /// </summary>
    [Fact]
    public async Task A_conversation_opened_through_the_alias_is_read_back_on_the_canonical_path()
    {
        var (_, buyer) = await RegisterAndVerifyAsync();
        var (_, seller) = await RegisterAndVerifyAsync();

        Api.Authenticate(buyer.Tokens.AccessToken);

        var created = await HttpClient.PostAsJsonAsync(
            "/v1/conversations",
            new
            {
                recipientId = seller.User.Id,
                context = new
                {
                    type = "MarketplaceListing",
                    entityId = "alias-listing",
                    entityTitle = "A bicycle"
                }
            },
            JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var opened = await IdentityApiClient.ReadDataAsync<OpenedConversation>(created);

        var readBack = await HttpClient.GetAsync($"/api/v1/conversations/{opened.ConversationId}");

        readBack.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The property that makes this a rewrite rather than a second API: authorisation is endpoint
    /// metadata, so an alias that reached a copy of the route could reach a copy without the
    /// authorisation on it. A protected route must refuse identically on both spellings.
    /// </summary>
    [Fact]
    public async Task An_authorised_route_refuses_an_anonymous_caller_on_both_spellings()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/conversations")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.GetAsync("/v1/conversations")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The rewrite prepends a segment; it cannot invent a route. A path that exists under neither
    /// spelling still 404s, from the same router, so a typo does not become a 500 or a hang.
    /// </summary>
    [Fact]
    public async Task A_path_that_exists_under_neither_prefix_is_still_not_found()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/v1/nothing-here")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        (await anonymous.GetAsync("/api/v1/nothing-here")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Segment-aware, not a string prefix. <c>/v1x</c> is a different first segment and must not be
    /// claimed — a raw <c>StartsWith</c> would have rewritten it to <c>/api/v1x</c>, which is a
    /// different 404 arrived at for a reason nobody could reconstruct from the logs.
    /// </summary>
    [Fact]
    public async Task A_path_that_merely_begins_with_the_same_letters_is_not_rewritten()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/v1x/conversations")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The surfaces that are deliberately not versioned. A rewrite that caught these would take the
    /// liveness probe out on the first deployment.
    /// </summary>
    [Fact]
    public async Task The_unversioned_surfaces_are_untouched()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync("/")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record OpenedConversation
    {
        public Guid ConversationId { get; init; }
    }
}
