using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Mauritius's towns are in the catalogue (14 Oct 2026). It held two districts and five towns, so
/// Grand Baie could not be chosen and a business there was filed under Rose Hill, 28 km away.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MauritiusGazetteerTests : PooledIdentityTestBase
{
    public MauritiusGazetteerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("grand baie", "Grand Baie")]
    [InlineData("mahebourg", "Mahebourg")]
    [InlineData("mahébourg", "Mahebourg")]
    [InlineData("flic en flac", "Flic en Flac")]
    public async Task A_member_finds_a_mauritian_town_by_name(string typed, string expected)
    {
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var body = await HttpClient.GetStringAsync($"/api/v1/location/search?q={Uri.EscapeDataString(typed)}");

        body.Should().Contain(expected);
    }

    [Fact]
    public async Task The_launch_places_are_still_there_once_each()
    {
        var (_, member) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var body = await HttpClient.GetStringAsync("/api/v1/location/search?q=rose hill");
        using var document = JsonDocument.Parse(body);

        // Re-running a seeder must reuse a node, never add a second Rose Hill beside the first.
        CountCities(document.RootElement, "Rose Hill").Should().Be(1, body);
    }

    private static int CountCities(JsonElement element, string name) => element.ValueKind switch
    {
        JsonValueKind.Object =>
            (element.TryGetProperty("cityName", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == name
             && element.TryGetProperty("level", out var l) && string.Equals(l.GetString(), "City", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            + element.EnumerateObject().Sum(property => CountCities(property.Value, name)),
        JsonValueKind.Array => element.EnumerateArray().Sum(item => CountCities(item, name)),
        _ => 0
    };
}
