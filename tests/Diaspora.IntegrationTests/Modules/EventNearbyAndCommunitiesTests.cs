using System.Net;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CL = Diaspora.Modules.CommunityLife.Domain;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>Events near me, nearest first, and events from my communities</b> (UAT, 3 Nov 2026). The
/// events screen said "Diaspora cannot filter events by" Nearby, a radius, distance and
/// Recommended. The route had taken a point and a radius since 14 Oct, as a rectangle; it now keeps
/// to the circle, says how far each event is, sorts by that distance on request, and narrows to the
/// communities the member belongs to.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class EventNearbyAndCommunitiesTests : PooledIdentityTestBase
{
    private const string Events = "/api/v1/community-life/events";

    // Nyali, Mombasa. Each event is placed due north of it, at roughly the distance in its title, and
    // "Corner" sits inside the 10 km search's bounding box but about 12.6 km away, outside the circle.
    private const double Latitude = -4.0220;
    private const double Longitude = 39.7100;
    private const double DegreesPerKm = 1d / 111.32d;

    public EventNearbyAndCommunitiesTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Nearby_events_keep_to_the_circle_say_how_far_they_are_and_sort_nearest_first()
    {
        var tag = $"near{Guid.NewGuid():N}"[..14];

        await SeedEventsAsync(
            (title: $"{tag} eight", latitude: Latitude + (8 * DegreesPerKm), longitude: Longitude, communityId: null, days: 3),
            (title: $"{tag} one", latitude: Latitude + (1 * DegreesPerKm), longitude: Longitude, communityId: null, days: 9),
            (title: $"{tag} four", latitude: Latitude + (4 * DegreesPerKm), longitude: Longitude, communityId: null, days: 6),
            (title: $"{tag} corner", latitude: Latitude + (8.9 * DegreesPerKm), longitude: Longitude + (8.9 * DegreesPerKm), communityId: null, days: 1));

        var nearest = await ItemsAsync(
            $"{Events}?q={tag}&latitude={Latitude}&longitude={Longitude}&radiusKm=10&sort=Nearest&limit=50");

        nearest.Select(item => item.GetProperty("title").GetString())
            .Should().Equal([$"{tag} one", $"{tag} four", $"{tag} eight"],
                "nearest first, and the corner of the square is not inside the circle");

        var distances = nearest.Select(item => item.GetProperty("distanceKm").GetDouble()).ToList();
        distances.Should().BeInAscendingOrder();
        distances[0].Should().BeApproximately(1, 0.2);
        distances[2].Should().BeApproximately(8, 0.3);

        var soonest = await ItemsAsync(
            $"{Events}?q={tag}&latitude={Latitude}&longitude={Longitude}&radiusKm=10&limit=50");

        soonest.Select(item => item.GetProperty("title").GetString())
            .Should().Equal([$"{tag} eight", $"{tag} four", $"{tag} one"],
                "the default order is still soonest first, inside the same circle");
        soonest.Should().OnlyContain(item => item.GetProperty("distanceKm").ValueKind == JsonValueKind.Number,
            "a nearby search says how far every event is, whatever the order");
    }

    [Fact]
    public async Task Events_from_my_communities_are_the_communities_i_belong_to()
    {
        var tag = $"mine{Guid.NewGuid():N}"[..14];

        var (_, member) = await RegisterVerifiedMemberAsync();
        var joined = await SeedCommunityAsync($"Lagos Hub {tag}");
        var other = await SeedCommunityAsync($"Accra Hub {tag}");
        await FoundAsync(joined, member.User.Id);

        await SeedEventsAsync(
            (title: $"{tag} joined", latitude: 6.5244, longitude: 3.3792, communityId: joined, days: 4),
            (title: $"{tag} other", latitude: 5.6037, longitude: -0.1870, communityId: other, days: 5),
            (title: $"{tag} none", latitude: 6.5244, longitude: 3.3792, communityId: null, days: 6));

        Api.Authenticate(member.Tokens.AccessToken);

        (await ItemsAsync($"{Events}?q={tag}&joinedCommunities=true&limit=50"))
            .Select(item => item.GetProperty("title").GetString())
            .Should().Equal([$"{tag} joined"], "only events posted into a community the member belongs to");

        (await ItemsAsync($"{Events}?q={tag}&limit=50"))
            .Should().HaveCount(3, "without the filter every event is still listed");

        Api.ClearAuthentication();

        (await ItemsAsync($"{Events}?q={tag}&joinedCommunities=true&limit=50"))
            .Should().BeEmpty("a signed-out visitor belongs to no community");
    }

    private async Task<List<JsonElement>> ItemsAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return [.. document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private async Task SeedEventsAsync(
        params (string title, double latitude, double longitude, Guid? communityId, int days)[] rows)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var now = DateTimeOffset.UtcNow;
        var organizerId = UserId.New();

        foreach (var (row, index) in rows.Select((row, index) => (row, index)))
        {
            var communityEvent = CL.CommunityEvents.CommunityEvent.Draft(
                CL.Identifiers.CommunityEventId.New(),
                organizationId: null,
                organizerId,
                CL.ValueObjects.CommunityLifeTitle.Create(row.title).Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"{row.title}-{index}-{Guid.NewGuid():N}"),
                CL.ValueObjects.CommunityLifeDescription.Create("A seeded event.", null).Value,
                "community-meeting",
                CL.ValueObjects.EventSchedule
                    .Create(now.AddDays(row.days), now.AddDays(row.days).AddHours(2), "Africa/Nairobi").Value,
                CL.ValueObjects.CommunityLifeLocationContext.Create("KE", null, "Mombasa").Value,
                CL.ValueObjects.EventVenue.Create(name: "Hall", latitude: row.latitude, longitude: row.longitude).Value,
                CL.Enums.EventAttendanceMode.InPerson,
                now).Value;

            if (row.communityId is { } communityId)
            {
                communityEvent.PostInCommunity(communityId).IsSuccess.Should().BeTrue();
            }

            communityEvent.Publish(now).IsSuccess.Should().BeTrue();
            dbContext.Set<CL.CommunityEvents.CommunityEvent>().Add(communityEvent);
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedCommunityAsync(string name)
    {
        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate.Propose(
            CommunityId.New(),
            CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value,
            name,
            CommunityType.NationalDiaspora,
            CommunityVisibility.Public,
            "NG").Value;

        community.Open();
        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return community.Id.Value;
    }

    private async Task FoundAsync(Guid communityId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var aggregate = await communities.GetAsync(CommunityId.From(communityId));

        memberships.Add(CommunityMembership.Found(
            CommunityMembershipId.New(), aggregate!, UserId.From(userId), DateTimeOffset.UtcNow).Value);

        await unitOfWork.SaveChangesAsync();
    }
}
