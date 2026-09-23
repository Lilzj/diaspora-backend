using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The featured carousel, and the fact that nothing could ever fill it.
/// <para>
/// <b><c>GET /providers/featured</c> has answered with an empty list for its whole life.</b> The
/// aggregate has carried <c>IsFeatured</c> since it was written, <c>SetFeatured</c> was never
/// called from anywhere, and <c>providers:administer</c> was declared as a permission that gated
/// no route. Members saw an empty row on a surface the app draws; staff had no way to put
/// anything in it. Everything about that was correct in isolation.
/// </para>
/// <para>
/// So the first test here is about reachability rather than behaviour: featuring a provider must
/// actually change what the carousel returns. The rest are the rules that make the carousel mean
/// something — only staff decide it, and only a provider members can actually open may be in it.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ProviderFeaturingTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/providers";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProviderFeaturingTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>The whole point: what staff feature is what members see.</summary>
    [Fact]
    public async Task Featuring_a_provider_puts_it_in_the_carousel()
    {
        var providerId = await ActiveProviderAsync("Naija Express Money Transfer");

        await SignInAsRoleAsync("Admin");

        var featured = await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/featured",
            new { isFeatured = true, reason = "Consistently well reviewed in London." });

        featured.StatusCode.Should().Be(HttpStatusCode.OK);

        var carousel = await ReadIdsAsync(await HttpClient.GetAsync($"{Root}/featured"));

        carousel.Should().Contain(providerId);
    }

    /// <summary>And taking it out takes it out, so the decision is reversible.</summary>
    [Fact]
    public async Task Unfeaturing_a_provider_removes_it_from_the_carousel()
    {
        var providerId = await ActiveProviderAsync("Ebony Braids and Beauty");

        await SignInAsRoleAsync("Admin");

        await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/featured", new { isFeatured = true, reason = "Editorial pick." });

        await HttpClient.PutAsJsonAsync($"{Root}/{providerId}/featured", new { isFeatured = false });

        var carousel = await ReadIdsAsync(await HttpClient.GetAsync($"{Root}/featured"));

        carousel.Should().NotContain(providerId);
    }

    /// <summary>
    /// An unpublished provider cannot be featured: the carousel is a promise that these are worth
    /// opening, and a draft is a dead link.
    /// </summary>
    [Fact]
    public async Task A_provider_members_cannot_open_is_not_featurable()
    {
        var providerId = await RegisterProviderAsync("Lagos Legal Services");

        await SignInAsRoleAsync("Admin");

        var featured = await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/featured", new { isFeatured = true, reason = "Looks good." });

        featured.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var carousel = await ReadIdsAsync(await HttpClient.GetAsync($"{Root}/featured"));

        carousel.Should().NotContain(providerId);
    }

    /// <summary>
    /// The owner of a business cannot feature it. Otherwise the carousel means nothing at all.
    /// </summary>
    [Fact]
    public async Task An_owner_cannot_feature_their_own_business()
    {
        var providerId = await ActiveProviderAsync("Accra Auto Repairs");

        var featured = await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/featured", new { isFeatured = true, reason = "We are excellent." });

        featured.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A registered provider, left as its registration made it.</summary>
    private async Task<Guid> RegisterProviderAsync(string name)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync(
            Root,
            new Dictionary<string, object?>
            {
                ["name"] = name,
                ["providerType"] = "Business",
                ["category"] = "Restaurants",
                ["cityName"] = "London",
                ["countryCode"] = "GB",
                ["phone"] = "+447700900456"
            },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").GetProperty("providerId").GetGuid();
    }

    /// <summary>The same, published by its owner so members can reach it.</summary>
    private async Task<Guid> ActiveProviderAsync(string name)
    {
        var providerId = await RegisterProviderAsync(name);

        var published = await HttpClient.PutAsJsonAsync(
            $"{Root}/{providerId}/status", new { status = "Active" });

        published.IsSuccessStatusCode.Should().BeTrue(
            await published.Content.ReadAsStringAsync());

        return providerId;
    }

    private static async Task<List<Guid>> ReadIdsAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return [.. document.RootElement
            .GetProperty("data")
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("providerId").GetGuid())];
    }
}
