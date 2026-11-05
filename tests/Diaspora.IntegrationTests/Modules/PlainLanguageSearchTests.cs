using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// <b>Plain-language search for homes and jobs</b> (owner, 5 Nov 2026). The text was matched
/// as one phrase, so a sentence ("a flat by the sea", "cleaner for one day") found nothing. Any
/// meaningful word of it now finds a result, in any case, and words that only hold the sentence
/// together match nothing on their own.
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class PlainLanguageSearchTests : PooledIdentityTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlainLanguageSearchTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_sentence_finds_a_home_by_any_of_its_words()
    {
        var run = await SeedHomesAsync("Bright {0} Flat", "Quiet {0} Office");
        var flat = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Bright {0} Flat", run);
        var office = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Quiet {0} Office", run);

        var titles = await SearchHomesAsync($"query=looking%20for%20a%20FLAT%20{run}%20please");

        titles.Should().Contain(flat);
        titles.Should().Contain(office, "both share the run's word");

        var flatOnly = await SearchHomesAsync($"query=FLAT%20bright{run}%20{run}x");
        flatOnly.Should().NotContain(office, "no word of that search is in the office's title");
    }

    [Fact]
    public async Task A_sentence_finds_a_job_by_any_of_its_words_in_any_case()
    {
        var run = Guid.NewGuid().ToString("N")[..8];
        var cleaner = await PublishAJobAsync($"Cleaner {run}k wanted");
        var driver = await PublishAJobAsync($"Driver {run}p wanted");

        Api.ClearAuthentication();

        var titles = await SearchJobsAsync($"q=I%20need%20CLEANER%20{run}k%20work");

        titles.Should().Contain($"Cleaner {run}k wanted");
        titles.Should().NotContain($"Driver {run}p wanted", "no word of the search is in the driver's job");
        cleaner.Should().NotBe(driver);
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task<string> SeedHomesAsync(params string[] titleFormats)
    {
        Factory.EnsureDatabaseCreated();

        var run = "pl" + Guid.NewGuid().ToString("N")[..8];
        var landlordId = LandlordProfileId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var format in titleFormats)
        {
            var title = string.Format(System.Globalization.CultureInfo.InvariantCulture, format, run);

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                UserId.New(),
                HousingTitle.Create(title).Value,
                HousingSlug.Create($"plain-{run}-{Guid.NewGuid().ToString("N")[..6]}"),
                HousingDescription.Create("A home seeded for the plain-language search tests.").Value,
                "apartment",
                HousingPrice.Create(20_000m, "MUR", RentPeriod.PerMonth, null).Value,
                HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
                HousingAvailability.Create(totalUnits: 1).Value,
                HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
                PropertyAddress.Undisclosed,
                StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<List<string>> SearchHomesAsync(string query)
    {
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"/api/v1/housing/search?limit=100&{query}"));

        return page.Items.Select(item => item.Title).ToList();
    }

    private async Task<List<string>> SearchJobsAsync(string query)
    {
        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"/api/v1/jobs/?limit=100&{query}"));

        return page.Items.Select(item => item.Title).ToList();
    }

    private async Task<Guid> PublishAJobAsync(string title)
    {
        Factory.EnsureDatabaseCreated();

        var (_, owner) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(owner.Tokens.AccessToken);

        var employer = await ReadJsonAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/employers",
            new { displayName = $"{title} Employer", organizationType = "Company", countryCode = "MU" }));

        var job = await ReadJsonAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                employerId = employer.GetProperty("employerId").GetGuid(),
                title,
                description = $"{title}, described at enough length to be valid.",
                categoryKey = "hospitality",
                jobType = "PartTime",
                experienceLevel = "Entry",
                countryCode = "MU"
            }));

        var jobId = job.GetProperty("jobId").GetGuid();

        var published = await HttpClient.PostAsJsonAsync(
            $"/api/v1/jobs/{jobId}/publish", new { openImmediately = true });
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        return jobId;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("data").Clone();
    }

    private sealed record PageShape
    {
        public List<CardShape> Items { get; init; } = [];
    }

    private sealed record CardShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
