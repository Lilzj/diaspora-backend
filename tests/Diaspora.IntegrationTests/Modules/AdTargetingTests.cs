using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Targeting options and audience estimates — MON-GAP-007.
/// <para>
/// <b>The register's complaint had two halves and this is the second.</b> The first was that the
/// builder collected seven dimensions and the wire carried one, which the audience type fixed. The
/// second was that the selectable values were "hard-coded in the app" and that there was no way to
/// show a size — so what these pin is that the options come from the module that owns them, and
/// that the estimate says what it is based on rather than quoting a number.
/// </para>
/// <para>
/// <b>The interesting assertions are the refusals.</b> A dimension the platform cannot enumerate
/// says so rather than returning an empty list; a selection it cannot count says so rather than
/// returning zero. Both would render as "nothing matches", which is a different and false answer.
/// </para>
/// </summary>
[Collection(WorkCollection.Name)]
public sealed class AdTargetingTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/financial";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AdTargetingTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Options ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Financial references no other module, so a real community reaching this list proves the
    /// contribution works.</b> B18 supplies the vocabulary; B16 never learns what a community is.
    /// </summary>
    [Fact]
    public async Task The_community_dimension_is_filled_by_the_module_that_owns_it()
    {
        using var client = await AdvertiserAsync();
        var slug = await SeedCommunityAsync("Targeting probe community");

        var options = await ReadDataAsync<OptionsDto>(
            await client.GetAsync($"{Root}/targeting/options?dimension=Community&query=Targeting%20probe"));

        options.HasCatalogue.Should().BeTrue();
        options.Options.Should().NotBeEmpty();
        options.Options.Should().Contain(option => option.Label == "Targeting probe community");

        slug.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// <b>A dimension with no catalogue says so, rather than returning an empty list.</b> "Pick
    /// from this empty list" and "type what you want, we cannot enumerate these" are different
    /// sentences, and only one of them leaves the advertiser able to proceed.
    /// </summary>
    [Fact]
    public async Task A_dimension_the_platform_cannot_enumerate_says_so()
    {
        using var client = await AdvertiserAsync();

        var options = await ReadDataAsync<OptionsDto>(
            await client.GetAsync($"{Root}/targeting/options?dimension=Interest"));

        options.HasCatalogue.Should().BeFalse();
        options.Options.Should().BeEmpty();
        options.Note.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>Country is deliberately not served.</b> It is an ISO list the app already holds in full,
    /// and a second copy from here would be a second list to keep in step for nothing.
    /// </summary>
    [Fact]
    public async Task Country_is_left_to_the_app_and_the_note_says_why()
    {
        using var client = await AdvertiserAsync();

        var options = await ReadDataAsync<OptionsDto>(
            await client.GetAsync($"{Root}/targeting/options?dimension=Country"));

        options.HasCatalogue.Should().BeFalse();
        options.Note.Should().Contain("ISO");
    }

    [Fact]
    public async Task A_dimension_the_platform_does_not_honour_is_refused()
    {
        using var client = await AdvertiserAsync();

        var response = await client.GetAsync($"{Root}/targeting/options?dimension=Astrology");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("dimension.unknown");
    }

    // ── Estimates ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A range, not a number, and the low end is the largest single community.</b> Somebody in
    /// two of the selected communities is one person, and the platform cannot tell how many are in
    /// both — so the fewest the union can contain is the biggest group, and the most is the sum.
    /// </summary>
    [Fact]
    public async Task A_community_selection_is_estimated_as_a_range_with_its_reasoning()
    {
        using var client = await AdvertiserAsync();

        var first = await SeedCommunityWithMembersAsync("Estimate probe A", members: 5);
        var second = await SeedCommunityWithMembersAsync("Estimate probe B", members: 3);

        var estimate = await ReadDataAsync<EstimateDto>(
            await client.PostAsJsonAsync($"{Root}/targeting/estimate", new
            {
                communityIds = new[] { first, second }
            }));

        estimate.IsNarrowed.Should().BeTrue();
        estimate.LowerBound.Should().Be(5, "the fewest the union can hold is the biggest group");
        estimate.UpperBound.Should().Be(8, "the most it can hold is the sum");

        estimate.ConstrainedDimensions.Should().Contain("Community");
        estimate.Basis.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>A campaign narrowed on something nobody can count says so.</b> Reporting zero would read
    /// as "nothing matches"; reporting no constraint would read as "everybody". Both are false.
    /// </summary>
    [Fact]
    public async Task A_selection_the_platform_cannot_count_is_narrowed_but_unsized()
    {
        using var client = await AdvertiserAsync();

        var estimate = await ReadDataAsync<EstimateDto>(
            await client.PostAsJsonAsync($"{Root}/targeting/estimate", new
            {
                interests = new[] { "technology" }
            }));

        estimate.IsNarrowed.Should().BeTrue("the campaign genuinely is narrowed");
        estimate.UpperBound.Should().Be(0);
        estimate.ConstrainedDimensions.Should().Contain("Interest");
        estimate.Basis.Should().Contain("cannot count");
    }

    /// <summary>An unnarrowed campaign has no size to estimate, and the platform does not invent one.</summary>
    [Fact]
    public async Task An_unnarrowed_campaign_is_not_given_a_size()
    {
        using var client = await AdvertiserAsync();

        var estimate = await ReadDataAsync<EstimateDto>(
            await client.PostAsJsonAsync($"{Root}/targeting/estimate", new { }));

        estimate.IsNarrowed.Should().BeFalse();
        estimate.ConstrainedDimensions.Should().BeEmpty();
        estimate.Basis.Should().Contain("does not publish");
    }

    /// <summary>
    /// <b>Constraints intersect, so the tightest bound wins.</b> Summing across dimensions would
    /// quote a reach larger than any one of the campaign's own constraints allows.
    /// </summary>
    [Fact]
    public async Task Adding_a_dimension_never_widens_the_estimate()
    {
        using var client = await AdvertiserAsync();
        var community = await SeedCommunityWithMembersAsync("Intersection probe", members: 4);

        var communityOnly = await ReadDataAsync<EstimateDto>(
            await client.PostAsJsonAsync($"{Root}/targeting/estimate", new
            {
                communityIds = new[] { community }
            }));

        var withInterest = await ReadDataAsync<EstimateDto>(
            await client.PostAsJsonAsync($"{Root}/targeting/estimate", new
            {
                communityIds = new[] { community },
                interests = new[] { "technology" }
            }));

        withInterest.UpperBound.Should().BeLessThanOrEqualTo(communityOnly.UpperBound);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<HttpClient> AdvertiserAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return client;
    }

    private async Task<string> SeedCommunityAsync(string name)
    {
        var (slug, _) = await SeedAsync(name, members: 0);
        return slug;
    }

    private async Task<Guid> SeedCommunityWithMembersAsync(string name, int members)
    {
        var (_, id) = await SeedAsync(name, members);
        return id;
    }

    private async Task<(string Slug, Guid Id)> SeedAsync(string name, int members)
    {
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate.Propose(
            CommunityId.New(), slug, name,
            CommunityType.InterestCommunity, CommunityVisibility.Public).Value;

        community.Open();

        // Through the aggregate, because the count is maintained by it and writing the column
        // directly would be testing a number the domain never produced.
        for (var index = 0; index < members; index++)
        {
            community.RecordMemberJoined();
        }

        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return (slug.Value, community.Id.Value);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        var envelope = JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        return envelope.Data!;
    }

    // ── Wire shapes, re-declared. ───────────────────────────────────────────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record OptionDto(string Value, string Label);

    private sealed record OptionsDto(
        string Dimension, List<OptionDto> Options, bool HasCatalogue, string? Note);

    private sealed record EstimateDto(
        long LowerBound,
        long UpperBound,
        List<string> ConstrainedDimensions,
        string Basis,
        bool IsNarrowed);
}
