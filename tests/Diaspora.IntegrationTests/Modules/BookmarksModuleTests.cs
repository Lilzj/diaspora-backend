using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The acceptance test for B2.6's whole thesis.
/// <para>
/// The Bookmarks module lives in its own assembly and was added to the platform without a
/// single edit to the Platform Kernel, the Identity module, or <c>Program.cs</c> — only a
/// project reference so its assembly is in the build output. This suite drives it over real
/// HTTP through the same host every other test uses, proving the module's endpoints,
/// permissions, feature gate, EF configuration and CQRS handlers all composed. If a toy
/// module plugs in this cleanly, Trust, Marketplace, Messaging and the rest will too.
/// </para>
/// </summary>
public sealed class BookmarksModuleTests : IdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BookmarksModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_member_can_save_list_and_remove_a_bookmark()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // Create
        var created = await HttpClient.PostAsJsonAsync(
            "/api/v1/bookmarks",
            new { targetType = "guide", targetId = "guide-123", note = "read later" },
            JsonOptions);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookmark = await ReadDataAsync<BookmarkDto>(created);
        bookmark.TargetType.Should().Be("guide");
        bookmark.TargetId.Should().Be("guide-123");

        // List
        var listed = await HttpClient.GetAsync("/api/v1/bookmarks");
        var bookmarks = await ReadDataAsync<List<BookmarkDto>>(listed);
        bookmarks.Should().ContainSingle().Which.Id.Should().Be(bookmark.Id);

        // Remove
        var removed = await HttpClient.DeleteAsync($"/api/v1/bookmarks/{bookmark.Id}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRemoval = await HttpClient.GetAsync("/api/v1/bookmarks");
        (await ReadDataAsync<List<BookmarkDto>>(afterRemoval)).Should().BeEmpty();
    }

    [Fact]
    public async Task Saving_the_same_target_twice_is_idempotent()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var first = await Create("listing", "listing-9");
        var second = await Create("listing", "listing-9");

        first.Id.Should().Be(second.Id, "a second save of the same target updates rather than duplicates");

        var listed = await HttpClient.GetAsync("/api/v1/bookmarks");
        (await ReadDataAsync<List<BookmarkDto>>(listed)).Should().ContainSingle();
    }

    [Fact]
    public async Task Bookmarks_require_authentication()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync("/api/v1/bookmarks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_module_endpoints_return_the_platform_error_envelope()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // A bookmark id that does not exist — the module returns its own NotFound, which the
        // shared PlatformResults helper renders as the same problem+json every module emits.
        var response = await HttpClient.DeleteAsync($"/api/v1/bookmarks/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"code\"");
        body.Should().Contain("Bookmarks.NotFound");
    }

    [Fact]
    public async Task The_module_feature_gate_composed_into_the_platform_catalogue()
    {
        // Proof the module's IFeatureContributor reached the composed catalogue: bookmarks.use
        // is a Bookmarks-owned gate, and it turns up in Identity's /access/features response —
        // which the module never touched — for a member who has reached its tier.
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features.Select(f => f.Name).Should().Contain("bookmarks.use");
        features.Features.Single(f => f.Name == "bookmarks.use").IsEnabled.Should().BeTrue();
    }

    private async Task<BookmarkDto> Create(string targetType, string targetId)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/bookmarks",
            new { targetType, targetId },
            JsonOptions);

        return await ReadDataAsync<BookmarkDto>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Expected success but got {(int)response.StatusCode}. Body: {body}");
        }

        var envelope = JsonSerializer.Deserialize<Envelope<T>>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize: {body}");

        return envelope.Data;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record BookmarkDto(Guid Id, string TargetType, string TargetId, string? Note, DateTimeOffset CreatedAt);
}
