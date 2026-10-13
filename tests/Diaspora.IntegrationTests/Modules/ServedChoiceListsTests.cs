using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Choice lists the app now reads instead of keeping its own copy (13 Oct 2026). Each copy had
/// drifted: five of six update categories and every lease length the app offered were values
/// the server did not have, and were dropped or filed as "Other" without a word.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ServedChoiceListsTests : PooledIdentityTestBase
{
    public ServedChoiceListsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Update_categories_are_the_servers_own()
    {
        var (_, member) = await RegisterAndVerifyAsync();
        Api.Authenticate(member.Tokens.AccessToken);

        var keys = await KeysAsync("/api/v1/community/posts/categories", element => element);

        keys.Should().Contain(["police", "marketPrices", "employment", "other"]);
        keys.Should().NotContain(["safetyAlert", "jobLead", "housingTip"]);
    }

    [Fact]
    public async Task Housing_options_are_the_values_a_listing_accepts()
    {
        var furnishing = await KeysAsync("/api/v1/housing/listing-options", data => data.GetProperty("furnishing"));
        var leases = await KeysAsync("/api/v1/housing/listing-options", data => data.GetProperty("leaseTypes"));

        furnishing.Should().Contain(["Unfurnished", "PartlyFurnished", "Furnished", "FullyEquipped"]);
        leases.Should().Contain(["MonthToMonth", "FixedTerm", "ShortTerm", "LongTerm"]);
        leases.Should().NotContain("Unspecified");
    }

    private async Task<List<string?>> KeysAsync(string route, Func<JsonElement, JsonElement> pick)
    {
        using var document = JsonDocument.Parse(await HttpClient.GetStringAsync(route));

        return pick(document.RootElement.GetProperty("data"))
            .EnumerateArray()
            .Select(option => option.GetProperty("key").GetString())
            .ToList();
    }
}
