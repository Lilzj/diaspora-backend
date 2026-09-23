using System.Text.Json.Serialization;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// What a community update looks like to a map — 16 Sep 2026.
/// <para>
/// <b>The client drew none of these for the whole life of the feature.</b> It read
/// <c>data.updates</c>; this route answers a <c>groups</c> dictionary keyed by resource type, so
/// the layer resolved to an empty list on every pan. The client is fixed, and the shape it now
/// reads is pinned here — the existing nearby tests assert a well-formed <em>empty</em> payload,
/// which is exactly the assertion that cannot tell a working layer from a broken one.
/// </para>
/// <para>
/// The payload beside the pin is the second half. An alert card renders the update's own words and
/// a "reported 20 minutes ago" line, and a map must drop an alert whose time has passed before the
/// sweep gets to it — none of which a title and a coordinate can carry.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class NearbyUpdatePayloadTests : PooledIdentityTestBase
{
    // Peckham, which the seeded catalogue knows.
    private const double Latitude = 51.4742d;
    private const double Longitude = -0.0691d;

    public NearbyUpdatePayloadTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_published_update_nearby_is_in_the_updates_group_with_what_a_card_renders()
    {
        var run = await SeedUpdateAsync();

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var nearby = await IdentityApiClient.ReadDataAsync<NearbyShape>(
            await HttpClient.GetAsync(
                $"/api/v1/location/nearby?latitude={Latitude}&longitude={Longitude}&radiusKm=5"));

        nearby.UnavailableResourceTypes.Should().BeEmpty(
            "nothing failed, and an empty group means nobody has anything here");

        var updates = nearby.Groups.Should().ContainKey("updates").WhoseValue;
        var item = updates.Should().ContainSingle(row => row.Title == $"Road closed {run}").Subject;

        item.Latitude.Should().BeApproximately(Latitude, 0.01d);
        item.Longitude.Should().BeApproximately(Longitude, 0.01d);
        item.DistanceKm.Should().BeLessThan(1d);

        // Camel-cased, as every enum this module publishes is. A client that switches on
        // PascalCase gets nothing, which is how the alert kind came to be undefined.
        item.Category.Should().Be("traffic");

        var payload = item.Payload.Should().NotBeNull().And.Subject;

        payload["urgency"].Should().Be("urgent");
        payload["status"].Should().Be("published");
        payload["body"].Should().Be($"The high street is closed between the station and the market. {run}");
        payload["countryCode"].Should().Be("GB");
        payload.Should().ContainKey("publishedAt");
        payload.Should().ContainKey("expiresAt", "a map has to drop an alert whose time has passed");
        payload.Should().ContainKey("confirmationCount");
        payload.Should().ContainKey("verificationStatus");

        DateTimeOffset.TryParse(payload["publishedAt"], out var publishedAt).Should().BeTrue();
        DateTimeOffset.TryParse(payload["expiresAt"], out var expiresAt).Should().BeTrue();
        expiresAt.Should().BeAfter(publishedAt);
    }

    /// <summary>
    /// A draft is not near anybody. The provider asks for published, public posts only, and this
    /// holds it to that — a map is a public surface and has no notion of a viewer's audience.
    /// </summary>
    [Fact]
    public async Task A_draft_is_not_offered_to_the_map()
    {
        var run = await SeedUpdateAsync(publish: false);

        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var nearby = await IdentityApiClient.ReadDataAsync<NearbyShape>(
            await HttpClient.GetAsync(
                $"/api/v1/location/nearby?latitude={Latitude}&longitude={Longitude}&radiusKm=5"));

        nearby.Groups["updates"].Should().NotContain(row => row.Title == $"Road closed {run}");
    }

    private async Task<string> SeedUpdateAsync(bool publish = true)
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var post = CommunityPost.Draft(
            UserId.New(),
            PublishingIdentity.Personal(),
            PostBody.Create(
                $"The high street is closed between the station and the market. {run}").Value,
            PostTitle.CreateOptional($"Road closed {run}").Value,
            PostCategory.Traffic,
            UrgencyLevel.Urgent,
            ContentAudience.Public,
            PostLocationContext.Create(
                countryCode: "GB",
                areaName: "Peckham",
                cityName: "London",
                latitude: Latitude,
                longitude: Longitude),
            now).Value;

        if (publish)
        {
            post.Publish(now);
        }

        dbContext.Set<CommunityPost>().Add(post);
        await dbContext.SaveChangesAsync();

        return run;
    }

    private sealed record NearbyShape
    {
        public Dictionary<string, List<NearbyItemShape>> Groups { get; init; } = [];

        public List<string> UnavailableResourceTypes { get; init; } = [];
    }

    private sealed record NearbyItemShape
    {
        public string Title { get; init; } = string.Empty;

        public string? Category { get; init; }

        public double DistanceKm { get; init; }

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        [JsonPropertyName("payload")]
        public Dictionary<string, string>? Payload { get; init; }
    }
}
