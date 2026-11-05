using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Location.Domain.Towns;
using Diaspora.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>Reading a place out of a search</b> (owner, 5 Nov 2026): "3 bedroom flat in Grand Baie".
/// The platform is used everywhere, so the towns come from the world list (GeoNames), and a
/// member's slip ("grand baye") still finds the town, without any AI.
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class WorldTownMatchTests : PooledIdentityTestBase
{
    public WorldTownMatchTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("Grand Baie", "Grand Baie", true)]
    [InlineData("grand-baie", "Grand Baie", true)]
    [InlineData("Grand Bay", "Grand Baie", true)]
    [InlineData("grand baye", "Grand Baie", false)]
    [InlineData("mahebourg", "Mahébourg", true)]
    [InlineData("PORT LOUIS", "Port Louis", true)]
    public async Task A_town_is_found_by_its_name_its_other_spellings_and_a_close_slip(
        string typed, string expected, bool exact)
    {
        await SignInAndSeedAsync();

        var match = await MatchAsync(typed, "MU");

        match.GetProperty("found").GetBoolean().Should().BeTrue();
        match.GetProperty("name").GetString().Should().Be(expected);
        match.GetProperty("countryCode").GetString().Should().Be("MU");
        match.GetProperty("exact").GetBoolean().Should().Be(exact);
        match.GetProperty("latitude").GetDouble().Should().BeLessThan(0, "Mauritius is south of the equator");
    }

    [Fact]
    public async Task The_members_own_country_is_asked_first()
    {
        await SignInAndSeedAsync();

        (await MatchAsync("grand bay", "MU")).GetProperty("countryCode").GetString().Should().Be("MU");
        (await MatchAsync("grand bay", "US")).GetProperty("countryCode").GetString().Should().Be(
            "US", "Grand Bay, Alabama is the one a member in the United States means");
    }

    [Fact]
    public async Task A_place_in_another_country_is_found_when_home_has_nothing_like_it()
    {
        await SignInAndSeedAsync();

        var match = await MatchAsync("grand bay", "NG");

        match.GetProperty("found").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Found on production data, 5 Nov 2026: GeoNames files Rose Hill as "Beau Bassin-Rose Hill",
    /// so "salon in Rose Hill" from Mauritius found no town at home, then Rose Hill in the United
    /// States, and the app told the member it was outside Mauritius.
    /// </summary>
    [Theory]
    [InlineData("rose hill", "Beau Bassin-Rose Hill")]
    [InlineData("Rose-Hill", "Beau Bassin-Rose Hill")]
    [InlineData("vacoas", "Vacoas-Phoenix")]
    [InlineData("phoenix", "Vacoas-Phoenix")]
    public async Task A_town_at_home_is_found_by_the_words_inside_its_name(string typed, string expected)
    {
        await SignInAndSeedAsync();

        var match = await MatchAsync(typed, "MU");

        match.GetProperty("found").GetBoolean().Should().BeTrue();
        match.GetProperty("name").GetString().Should().Be(expected);
        match.GetProperty("countryCode").GetString().Should().Be("MU");
        match.GetProperty("exact").GetBoolean().Should().BeFalse("part of a name is not the name as the list spells it");
    }

    [Fact]
    public async Task Words_inside_a_name_count_only_at_home()
    {
        await SignInAndSeedAsync();

        var inTheStates = await MatchAsync("rose hill", "US");

        inTheStates.GetProperty("name").GetString().Should().Be("Rose Hill");
        inTheStates.GetProperty("countryCode").GetString().Should().Be("US");
        inTheStates.GetProperty("exact").GetBoolean().Should().BeTrue();

        // From a country with neither, the world is asked by whole names only.
        (await MatchAsync("bassin rose", "NG")).GetProperty("found").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("zzqx town")]
    [InlineData("granby")]
    [InlineData("a")]
    public async Task A_name_close_to_no_town_finds_nothing(string typed)
    {
        await SignInAndSeedAsync();

        (await MatchAsync(typed, "MU")).GetProperty("found").GetBoolean().Should().BeFalse();
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task SignInAndSeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        dbContext.Set<WorldTown>().AddRange(
            WorldTown.Create(NewId(), "Grand Baie", ["Grand Bay", "Grand-Baie"], "MU", -20.01, 57.58, 11_000),
            WorldTown.Create(NewId(), "Port Louis", ["Port-Louis"], "MU", -20.16, 57.50, 150_000),
            WorldTown.Create(NewId(), "Mahébourg", ["Mahebourg"], "MU", -20.41, 57.70, 17_000),
            WorldTown.Create(NewId(), "Grand Bay", [], "US", 30.48, -88.34, 3_600),
            WorldTown.Create(NewId(), "Beau Bassin-Rose Hill", ["Beau Bassin"], "MU", -20.23, 57.47, 111_355),
            WorldTown.Create(NewId(), "Vacoas-Phoenix", [], "MU", -20.30, 57.48, 105_000),
            WorldTown.Create(NewId(), "Rose Hill", ["Rosehill"], "US", 38.79, -77.11, 20_226));

        await dbContext.SaveChangesAsync();
    }

    private static int NewId() => Random.Shared.Next(100_000_000, int.MaxValue);

    private async Task<JsonElement> MatchAsync(string typed, string countryCode)
    {
        var response = await HttpClient.GetAsync(
            $"/api/v1/location/towns/match?text={Uri.EscapeDataString(typed)}&countryCode={countryCode}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").Clone();
    }
}
