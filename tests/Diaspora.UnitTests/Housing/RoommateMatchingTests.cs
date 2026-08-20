using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Roommate matching, and the one content rule in this module written for a legal reason rather
/// than a product one.
/// <para>
/// A household choosing who they will share a kitchen with is lawful in most of the markets this
/// platform serves. The same restriction on a whole property is housing discrimination in nearly
/// all of them. The aggregate enforces the distinction rather than documenting it.
/// </para>
/// </summary>
public class RoommateMatchingTests
{
    private static readonly DateTimeOffset Now = HousingFixtures.Now;

    [Fact]
    public void Preferences_are_refused_on_a_whole_property()
    {
        var listing = HousingFixtures.Listing(shared: false);

        var preferences = RoommatePreferences.Create(
            occupation: RoommateOccupation.Student).Value;

        listing.SetRoommatePreferences(preferences, baseRevision: null)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Preferences_are_permitted_on_shared_accommodation()
    {
        var listing = HousingFixtures.Listing(shared: true);

        var preferences = RoommatePreferences.Create(
            occupation: RoommateOccupation.Student,
            languages: ["Yoruba", "English"]).Value;

        listing.SetRoommatePreferences(preferences, baseRevision: null)
            .IsSuccess.Should().BeTrue();

        listing.RoommatePreferences.Languages.Should().Contain("Yoruba");
    }

    /// <summary>
    /// The rule that matters. A gender preference on a whole flat is a discriminatory advert, and
    /// it is refused loudly rather than silently dropped — a landlord who is not told would believe
    /// it had been applied.
    /// </summary>
    [Fact]
    public void A_gender_preference_is_refused_on_a_whole_property()
    {
        var listing = HousingFixtures.Listing(shared: false);

        var preferences = RoommatePreferences.Create(
            genderPreference: GenderPreference.FemaleOnly).Value;

        listing.SetRoommatePreferences(preferences, baseRevision: null)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_gender_preference_is_permitted_on_a_spare_room()
    {
        var listing = HousingFixtures.Listing(shared: true);

        var preferences = RoommatePreferences.Create(
            genderPreference: GenderPreference.FemaleOnly).Value;

        listing.SetRoommatePreferences(preferences, baseRevision: null)
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A family household is not a gender restriction, so it is permitted anywhere.
    /// </summary>
    [Fact]
    public void A_family_preference_is_permitted_on_a_whole_property()
    {
        var listing = HousingFixtures.Listing(shared: false);

        var preferences = RoommatePreferences.Create(
            genderPreference: GenderPreference.FamilyOnly).Value;

        listing.SetRoommatePreferences(preferences, baseRevision: null)
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// A gender preference surviving on a listing that has become a whole flat is precisely the
    /// discriminatory advert this module refuses to publish, so it is cleared rather than left.
    /// </summary>
    [Fact]
    public void Preferences_are_cleared_when_the_property_stops_being_shared()
    {
        var listing = HousingFixtures.Listing(shared: true);

        listing.SetRoommatePreferences(
            RoommatePreferences.Create(
                genderPreference: GenderPreference.MaleOnly).Value,
            baseRevision: null);

        listing.SetStayPolicy(
            StayPolicy.Create(
                acceptsShortStay: false,
                acceptsLongStay: true,
                isSharedAccommodation: false).Value,
            baseRevision: null);

        listing.RoommatePreferences.IsEmpty.Should().BeTrue();
        listing.RoommatePreferences.GenderPreference.Should().Be(GenderPreference.Any);
    }

    /// <summary>
    /// Nationality preference is dropped rather than refused where the deployment has not enabled
    /// it: a landlord should not get a validation error for a field their client showed them, and
    /// the operator's decision is not theirs to argue with.
    /// </summary>
    [Fact]
    public void A_nationality_preference_is_dropped_unless_the_deployment_allows_it()
    {
        var withoutPermission = RoommatePreferences.Create(
            nationalityPreference: "Nigerian",
            allowNationalityPreference: false).Value;

        withoutPermission.NationalityPreference.Should().BeNull();

        var withPermission = RoommatePreferences.Create(
            nationalityPreference: "Nigerian",
            allowNationalityPreference: true).Value;

        withPermission.NationalityPreference.Should().Be("Nigerian");
    }

    [Fact]
    public void An_inverted_age_range_is_refused()
    {
        RoommatePreferences.Create(minimumAge: 40, maximumAge: 25)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// A comma would split one language into two on the way back out of the delimited column.
    /// </summary>
    [Fact]
    public void A_language_containing_a_comma_cannot_corrupt_the_stored_list()
    {
        var preferences = RoommatePreferences.Create(
            languages: ["Yoruba, Igbo"]).Value;

        preferences.Languages.Should().ContainSingle();
        preferences.LanguagesCsv.Should().NotContain(",");
    }

    /// <summary>
    /// A property that accepts neither a short stay nor a long one is one nobody can ever book.
    /// </summary>
    [Fact]
    public void A_stay_policy_must_accept_some_length_of_stay()
    {
        StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: false)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Looking for a roommate plainly means the accommodation is shared, so it is inferred rather
    /// than refused.
    /// </summary>
    [Fact]
    public void Looking_for_a_roommate_implies_shared_accommodation()
    {
        var policy = StayPolicy.Create(
            acceptsShortStay: false,
            acceptsLongStay: true,
            isSharedAccommodation: false,
            isLookingForRoommate: true).Value;

        policy.IsSharedAccommodation.Should().BeTrue();
    }
}
