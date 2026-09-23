using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Closing an account forgets where the member lives — 16 Sep 2026.
/// <para>
/// <b>Written before the client was allowed to save a place.</b> The routes for saved places have
/// been served since the module shipped, and the maps register refused to use them on exactly this
/// ground: a home address must not be persisted without a deletion path. It was right. The platform
/// held one erasure provider, Identity's, which reaches sessions, devices and one-time codes — so a
/// closed account kept its location profile, and the home address in it, for ever.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class LocationPrivacyTests : PooledIdentityTestBase
{
    public LocationPrivacyTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Erasing_a_member_deletes_the_places_they_saved()
    {
        var userId = await SeedProfileWithSavedPlaceAsync();

        using var scope = Factory.Services.CreateScope();

        var location = scope.ServiceProvider
            .GetServices<IDataErasureProvider>()
            .Should().ContainSingle(provider => provider.Module == "Location")
            .Subject;

        var preview = await location.PreviewAsync(userId.Value);

        preview.Should().Contain(result =>
            result.DataSet == "saved places"
            && result.Outcome == ErasureOutcome.Deleted
            && result.RecordsAffected == 1,
            "a member is told what will go before it goes");

        var erased = await location.EraseAsync(userId.Value);

        erased.Should().Contain(result => result.DataSet == "location profile");

        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await dbContext.Set<LocationProfile>()
            .IgnoreQueryFilters()
            .AnyAsync(profile => profile.UserId == userId))
            .Should().BeFalse("the profile holds the address, so the profile goes");
    }

    /// <summary>
    /// A member who never opened a map is not an error, and the receipt says so in its own words
    /// rather than reporting a deletion that did not happen.
    /// </summary>
    [Fact]
    public async Task A_member_with_no_location_profile_is_reported_as_holding_nothing()
    {
        using var scope = Factory.Services.CreateScope();

        var location = scope.ServiceProvider
            .GetServices<IDataErasureProvider>()
            .Single(provider => provider.Module == "Location");

        var preview = await location.PreviewAsync(Guid.NewGuid());

        preview.Should().ContainSingle()
            .Which.Outcome.Should().Be(ErasureOutcome.NothingHeld);
    }

    [Fact]
    public async Task A_member_can_take_their_saved_places_with_them()
    {
        var userId = await SeedProfileWithSavedPlaceAsync();

        using var scope = Factory.Services.CreateScope();

        var location = scope.ServiceProvider
            .GetServices<IDataExportProvider>()
            .Single(provider => provider.Module == "Location");

        var sections = await location.ExportAsync(userId.Value);

        var places = sections.Should().ContainSingle(section => section.DataSet == "saved places")
            .Subject.Records;

        var place = places.Should().ContainSingle().Subject;

        place["label"].Should().Be("Home");
        place["cityName"].Should().Be("London");
        // What the member chose, not what the catalogue resolved around them.
        place.Should().NotContainKey("areaId");
    }

    private async Task<UserId> SeedProfileWithSavedPlaceAsync()
    {
        Factory.EnsureDatabaseCreated();

        var userId = UserId.New();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var profile = LocationProfile.CreateFor(userId);

        var snapshot = LocationSnapshot.TryCreate(
            CountryCode.Create("GB"),
            "United Kingdom",
            stateName: "Greater London",
            cityName: "London",
            neighborhood: "Peckham",
            coordinate: GeoCoordinate.TryCreate(51.4742d, -0.0691d).Value,
            displayName: "Peckham, London").Value;

        profile.SaveLocation("Home", snapshot, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        dbContext.Set<LocationProfile>().Add(profile);
        await dbContext.SaveChangesAsync();

        return userId;
    }
}
