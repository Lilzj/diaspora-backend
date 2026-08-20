using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Address disclosure — the module's most consequential privacy control.
/// <para>
/// A precise address on a public page tells everyone which door is empty on the first of the month,
/// tells a scammer exactly which property to advertise as their own, and tells anybody who dislikes
/// the current occupant where to find them. Every assertion here is about one of those.
/// </para>
/// </summary>
public class PropertyAddressPrivacyTests
{
    private static PropertyAddress Address(
        AddressVisibility visibility = AddressVisibility.NeighborhoodOnly) =>
        PropertyAddress.Create(
            "14 Bellenden Road",
            "Flat 3",
            "SE15 4QY",
            51.4712d,
            -0.0685d,
            visibility).Value;

    [Fact]
    public void The_default_is_the_safest_value()
    {
        PropertyAddress.Undisclosed.Visibility
            .Should().Be(AddressVisibility.NeighborhoodOnly);
    }

    [Fact]
    public void A_stranger_sees_no_street_and_a_fuzzed_pin()
    {
        var disclosed = Address().Disclose(
            isOwner: false, isStaff: false, isAuthenticated: false, hasConfirmedViewing: false);

        disclosed.Line1.Should().BeNull();
        disclosed.Line2.Should().BeNull();
        disclosed.PostalCode.Should().BeNull();

        disclosed.Latitude.Should().NotBeNull(
            "a map with no pin is useless for judging a commute");

        disclosed.Latitude.Should().NotBe(
            51.4712d, "an exact pin on a public page is a door number in another form");
    }

    [Fact]
    public void The_landlord_always_sees_their_own_property_in_full()
    {
        var disclosed = Address().Disclose(
            isOwner: true, isStaff: false, isAuthenticated: true, hasConfirmedViewing: false);

        disclosed.Line1.Should().Be("14 Bellenden Road");
        disclosed.Latitude.Should().Be(51.4712d);
    }

    /// <summary>
    /// A moderator investigating a duplicate listing needs to see that two adverts name one flat.
    /// </summary>
    [Fact]
    public void Staff_see_the_full_address()
    {
        var disclosed = Address().Disclose(
            isOwner: false, isStaff: true, isAuthenticated: true, hasConfirmedViewing: false);

        disclosed.Line1.Should().Be("14 Bellenden Road");
    }

    /// <summary>
    /// The intended path to an address, and the reason booking a viewing is gated on a verified
    /// identity rather than merely being signed in.
    /// </summary>
    [Fact]
    public void A_confirmed_viewing_unlocks_the_full_address()
    {
        var address = Address(AddressVisibility.OnViewingConfirmed);

        address.Disclose(false, false, true, hasConfirmedViewing: false)
            .Line1.Should().BeNull();

        address.Disclose(false, false, true, hasConfirmedViewing: true)
            .Line1.Should().Be("14 Bellenden Road");
    }

    /// <summary>
    /// Street level gives enough to judge a commute and not enough to knock. The flat number goes
    /// with the house number, because "Flat 3" is a door.
    /// </summary>
    [Fact]
    public void Street_level_strips_the_building_number_and_the_second_line()
    {
        var disclosed = Address(AddressVisibility.StreetOnly).Disclose(
            isOwner: false, isStaff: false, isAuthenticated: true, hasConfirmedViewing: false);

        disclosed.Line1.Should().Be("Bellenden Road");
        disclosed.Line2.Should().BeNull();
    }

    /// <summary>
    /// The redaction is deliberately conservative. "12th Avenue" is a street and "12 Avenue Road"
    /// is a door, and on this field an over-eager strip is unhelpful while an under-eager one
    /// publishes an address.
    /// </summary>
    [Fact]
    public void A_street_whose_name_starts_with_a_word_is_left_alone()
    {
        var address = PropertyAddress.Create(
            "Bellenden Road", visibility: AddressVisibility.StreetOnly).Value;

        address.Disclose(false, false, true, false).Line1.Should().Be("Bellenden Road");
    }

    [Fact]
    public void A_line_that_is_only_a_number_discloses_nothing_rather_than_a_bare_door_number()
    {
        var address = PropertyAddress.Create(
            "14", visibility: AddressVisibility.StreetOnly).Value;

        address.Disclose(false, false, true, false).Line1.Should().BeNull();
    }

    /// <summary>
    /// A hostel's address is public anyway, so a signed-in member sees it without a viewing.
    /// </summary>
    [Fact]
    public void Authenticated_members_see_a_business_premises()
    {
        var address = Address(AddressVisibility.AuthenticatedMembers);

        address.Disclose(false, false, isAuthenticated: false, false)
            .Line1.Should().BeNull();

        address.Disclose(false, false, isAuthenticated: true, false)
            .Line1.Should().Be("14 Bellenden Road");
    }

    /// <summary>
    /// Half a coordinate is a pin in the Atlantic rather than a missing value a client can detect.
    /// </summary>
    [Fact]
    public void A_coordinate_must_be_both_halves_or_neither()
    {
        PropertyAddress.Create(latitude: 51.47d).IsFailure.Should().BeTrue();
        PropertyAddress.Create(longitude: -0.06d).IsFailure.Should().BeTrue();
        PropertyAddress.Create(latitude: 51.47d, longitude: -0.06d).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_coordinate_off_the_earth_is_refused()
    {
        PropertyAddress.Create(latitude: 91d, longitude: 0d).IsFailure.Should().BeTrue();
        PropertyAddress.Create(latitude: 0d, longitude: 181d).IsFailure.Should().BeTrue();
    }
}
