using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// The member's geographic context, and the travel-mode rule that every discovery query on the
/// platform depends on.
/// </summary>
public sealed class LocationProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static LocationSnapshot Place(
        string countryCode, string countryName, string? city, string? neighborhood) =>
        LocationSnapshot.TryCreate(
            CountryCode.Create(countryCode), countryName,
            stateName: null, cityName: city, neighborhood: neighborhood).Value;

    private static LocationSnapshot Peckham() =>
        Place("GB", "United Kingdom", "London", "Peckham");

    private static LocationSnapshot RoseHill() =>
        Place("MU", "Mauritius", "Rose Hill", "Rose Hill Central");

    // ── Travel mode ──────────────────────────────────────────────────────

    /// <summary>
    /// The rule the client implements as <c>getActiveLocation</c>. It lives in one place here
    /// so no consumer has to reimplement it, and so travel mode is honoured by every vertical
    /// without any of them knowing it exists.
    /// </summary>
    [Fact]
    public void The_active_location_is_the_home_location_when_travel_mode_is_off()
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetCurrentLocation(Peckham(), Now);

        profile.ActiveLocation!.Neighborhood.Should().Be("Peckham");
    }

    [Fact]
    public void The_active_location_is_the_destination_when_travel_mode_is_on()
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetCurrentLocation(Peckham(), Now);
        profile.SetTravelMode(true, RoseHill(), Now);

        profile.ActiveLocation!.CountryCode.Value.Should().Be("MU");
        profile.CurrentLocation!.CountryCode.Value.Should().Be("GB");
    }

    /// <summary>
    /// A member who toggles travel mode on without choosing a destination should keep seeing
    /// home results rather than nothing at all.
    /// </summary>
    [Fact]
    public void Travel_mode_without_a_destination_falls_back_to_home()
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetCurrentLocation(Peckham(), Now);
        profile.SetTravelMode(true, destination: null, Now);

        profile.ActiveLocation!.Neighborhood.Should().Be("Peckham");
    }

    /// <summary>
    /// Picking a place from the selector while abroad must not silently overwrite where the
    /// member lives.
    /// </summary>
    [Fact]
    public void Moving_while_travelling_changes_the_destination_not_the_home()
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetCurrentLocation(Peckham(), Now);
        profile.SetTravelMode(true, RoseHill(), Now);

        profile.SetCurrentLocation(
            Place("MU", "Mauritius", "Curepipe", "Curepipe Town"), Now);

        profile.TravelLocation!.CityName.Should().Be("Curepipe");
        profile.CurrentLocation!.Neighborhood.Should().Be("Peckham");
    }

    /// <summary>Toggling off and on again while abroad returns to where the member was.</summary>
    [Fact]
    public void Turning_travel_mode_off_preserves_the_destination()
    {
        var profile = LocationProfile.CreateFor(UserId.New());
        profile.SetCurrentLocation(Peckham(), Now);
        profile.SetTravelMode(true, RoseHill(), Now);
        profile.SetTravelMode(false, null, Now);
        profile.SetTravelMode(true, null, Now);

        profile.ActiveLocation!.CountryCode.Value.Should().Be("MU");
    }

    // ── Recents ──────────────────────────────────────────────────────────

    /// <summary>Mirrors the client's own reducer: filter the same place, unshift, take five.</summary>
    [Fact]
    public void Recents_are_capped_at_five_most_recent_first()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        for (var index = 1; index <= 8; index++)
        {
            profile.SetCurrentLocation(
                Place("GB", "United Kingdom", $"City {index}", $"Area {index}"),
                Now.AddMinutes(index));
        }

        profile.RecentLocations.Should().HaveCount(LocationProfile.MaxRecentLocations);
        profile.RecentLocationsByRecency[0].Location.CityName.Should().Be("City 8");
    }

    /// <summary>
    /// The same neighbourhood reached by GPS and by search produces two snapshots. One entry is
    /// what the member expects to see.
    /// </summary>
    [Fact]
    public void Revisiting_a_place_does_not_add_a_second_recent_entry()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.SetCurrentLocation(Peckham(), Now);
        profile.SetCurrentLocation(RoseHill(), Now.AddMinutes(1));
        profile.SetCurrentLocation(Peckham(), Now.AddMinutes(2));

        profile.RecentLocations.Should().HaveCount(2);
        profile.RecentLocationsByRecency[0].Location.Neighborhood.Should().Be("Peckham");
    }

    // ── Saved locations ──────────────────────────────────────────────────

    /// <summary>Idempotent on the label, matching the client's Record-keyed store.</summary>
    [Fact]
    public void Saving_the_same_label_twice_moves_it_rather_than_duplicating()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.SaveLocation("Home", Peckham(), Now);
        profile.SaveLocation("Home", RoseHill(), Now.AddMinutes(1));

        profile.SavedLocations.Should().HaveCount(1);
        profile.SavedLocations[0].Location.CountryCode.Value.Should().Be("MU");
    }

    /// <summary>
    /// Comparison is case-insensitive but storage preserves what the member typed.
    /// </summary>
    [Fact]
    public void Labels_compare_case_insensitively_and_store_verbatim()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.SaveLocation("home", Peckham(), Now);
        profile.SaveLocation("HOME", RoseHill(), Now);

        profile.SavedLocations.Should().HaveCount(1);
        profile.SavedLocations[0].Label.Should().Be("home");
    }

    [Fact]
    public void Removing_a_label_that_was_never_saved_fails()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.RemoveSavedLocation("Work").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Saved_locations_are_capped()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        for (var index = 0; index < LocationProfile.MaxSavedLocations; index++)
        {
            profile.SaveLocation($"Place {index}", Peckham(), Now).IsSuccess.Should().BeTrue();
        }

        profile.SaveLocation("One too many", Peckham(), Now).IsFailure.Should().BeTrue();
    }

    /// <summary>The three the frontend seeds are conventions, not a closed set.</summary>
    [Fact]
    public void A_custom_label_is_allowed_alongside_the_conventional_three()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.SaveLocation("Mum's", Peckham(), Now).IsSuccess.Should().BeTrue();

        SavedLocation.ConventionalLabels.Should().BeEquivalentTo("Home", "Work", "School");
    }

    // ── Privacy ──────────────────────────────────────────────────────────

    /// <summary>
    /// Identity owns the setting; Location enforces it, because Location is the only module
    /// that ever emits a coordinate.
    /// </summary>
    [Fact]
    public void Approximate_precision_coarsens_the_coordinate_and_drops_the_street()
    {
        var precise = LocationSnapshot.TryCreate(
            CountryCode.Create("GB"),
            "United Kingdom",
            stateName: "Greater London",
            cityName: "London",
            neighborhood: "Peckham",
            street: "Rye Lane",
            coordinate: GeoCoordinate.Create(51.474215d, -0.069133d)).Value;

        var disclosed = precise.AtPrecision(CoordinatePrecision.Approximate);

        disclosed.Coordinate!.Latitude.Should().Be(51.47d);
        disclosed.Street.Should().BeNull(
            "a precise street beside a rounded coordinate defeats the rounding");
    }

    // ── Defaults ─────────────────────────────────────────────────────────

    /// <summary>The client store's initial radius. Asserted so the two cannot drift.</summary>
    [Fact]
    public void The_default_preferences_match_the_client_store()
    {
        var preferences = GeographicPreference.Default;

        preferences.SearchRadius.Distance.Kilometers.Should().Be(5d);
        preferences.SearchRadius.Scope.Should().Be(RadiusScope.Radius);
        preferences.DistanceUnit.Should().Be(DistanceUnit.Kilometers);
        preferences.CoordinatePrecision.Should().Be(CoordinatePrecision.Exact);
    }

    [Fact]
    public void A_new_profile_starts_with_no_location_and_an_undetermined_permission()
    {
        var profile = LocationProfile.CreateFor(UserId.New());

        profile.ActiveLocation.Should().BeNull();
        profile.PermissionStatus.Should().Be(LocationPermissionStatus.Undetermined);
        profile.TravelMode.Should().BeFalse();
    }

    /// <summary>
    /// The display name is what the chip renders, and it is two components — the client's own
    /// <c>displayName</c> is exactly that.
    /// </summary>
    [Fact]
    public void A_display_name_is_composed_from_the_neighbourhood_and_the_city()
    {
        Peckham().DisplayName.Should().Be("Peckham, London");
        Place("GB", "United Kingdom", "London", null).DisplayName.Should().Be("London, United Kingdom");
        Place("GB", "United Kingdom", null, null).DisplayName.Should().Be("United Kingdom");
    }
}
