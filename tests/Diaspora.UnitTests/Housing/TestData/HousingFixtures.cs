using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.UnitTests.Housing.TestData;

/// <summary>
/// Aggregates in known-good states, so a test that is about one rule does not spend twenty lines
/// constructing the eight value objects it does not care about.
/// <para>
/// Every fixture is deliberately the ordinary case — a two-bedroom flat, one unit, available now,
/// long let — so a test that varies one thing makes that variation obvious.
/// </para>
/// </summary>
public static class HousingFixtures
{
    /// <summary>
    /// A fixed clock. Tests that assert on durations need one, and a test that reads
    /// <c>DateTimeOffset.UtcNow</c> is a test that fails at midnight on the last day of a month.
    /// </summary>
    public static DateTimeOffset Now { get; } =
        new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    public static UserId NewUserId() => UserId.From(Guid.NewGuid());

    public static LandlordProfileId NewLandlordId() =>
        LandlordProfileId.From(Guid.NewGuid());

    public static HousingListingId NewListingId() => HousingListingId.From(Guid.NewGuid());

    /// <summary>A two-bedroom flat, available now, one unit, long let. The ordinary case.</summary>
    public static HousingListing Listing(
        int totalUnits = 1,
        DateTimeOffset? availableFrom = null,
        bool shared = false,
        LandlordProfileId? landlordId = null) =>
        HousingListing.Draft(
            NewListingId(),
            landlordId ?? NewLandlordId(),
            NewUserId(),
            HousingTitle.Create("Bright two-bed flat near the station").Value,
            HousingSlug.Create("bright-two-bed-flat-near-the-station"),
            HousingDescription.Create("A bright two-bedroom flat.").Value,
            "apartment",
            Price(),
            HousingCapacity.Create(2, 1m, maximumOccupants: 4).Value,
            HousingAvailability.Create(
                availableFrom, totalUnits: totalUnits).Value,
            Location(),
            PropertyAddress.Undisclosed,
            StayPolicy.Create(
                acceptsShortStay: false,
                acceptsLongStay: true,
                isSharedAccommodation: shared).Value,
            Now).Value;

    /// <summary>A published, available listing — what most workflow tests need to start from.</summary>
    public static HousingListing PublishedListing(
        int totalUnits = 1, LandlordProfileId? landlordId = null)
    {
        var listing = Listing(totalUnits, landlordId: landlordId);

        // Publication needs at least one photograph, which is the module's one readiness rule.
        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, Now);
        listing.Publish(Now);

        return listing;
    }

    public static HousingPrice Price(
        decimal amount = 1_200m,
        string currency = "GBP",
        RentPeriod period = RentPeriod.PerMonth,
        decimal? deposit = 1_800m) =>
        HousingPrice.Create(amount, currency, period, deposit).Value;

    public static HousingLocationContext Location(Guid? neighborhoodAreaId = null) =>
        HousingLocationContext.Create(
            "GB",
            "Greater London",
            "London",
            "Peckham",
            neighborhoodAreaId ?? Guid.NewGuid(),
            "/GB/greater-london/london/peckham").Value;

    public static LandlordProfile Landlord(
        LandlordKind kind = LandlordKind.Individual, UserId? ownerId = null) =>
        LandlordProfile.Register(
            NewLandlordId(),
            ownerId ?? NewUserId(),
            LandlordIdentity.Create("Rose Hill Lettings", kind, "Rose Hill Ltd").Value,
            HousingSlug.Create("rose-hill-lettings"),
            "GB",
            LandlordContact.None,
            Now).Value;

    public static HousingViewing Viewing(
        HousingListingId? listingId = null,
        LandlordProfileId? landlordId = null,
        UserId? seekerId = null,
        DateTimeOffset? preferredAt = null) =>
        HousingViewing.Request(
            HousingViewingId.From(Guid.NewGuid()),
            listingId ?? NewListingId(),
            landlordId ?? NewLandlordId(),
            seekerId ?? NewUserId(),
            ViewingWindow.Create(preferredAt ?? Now.AddDays(2)).Value,
            "Could I come round on Saturday?",
            Now,
            Guid.NewGuid()).Value;

    public static HousingReservation Reservation(
        int units = 1,
        HousingListingId? listingId = null,
        LandlordProfileId? landlordId = null,
        UserId? seekerId = null,
        DateTimeOffset? holdExpiresAt = null) =>
        HousingReservation.Open(
            HousingReservationId.From(Guid.NewGuid()),
            listingId ?? NewListingId(),
            landlordId ?? NewLandlordId(),
            seekerId ?? NewUserId(),
            units,
            Terms(holdExpiresAt),
            Now,
            Guid.NewGuid()).Value;

    public static ReservationTerms Terms(DateTimeOffset? holdExpiresAt = null) =>
        ReservationTerms.Create(
            Now.AddDays(14),
            null,
            holdExpiresAt ?? Now.AddDays(3),
            1_200m,
            1_800m,
            "GBP",
            RentPeriod.PerMonth,
            LeaseType.LongTerm,
            occupants: 2).Value;

    public static NeighborhoodProfile NeighborhoodProfile(Guid? areaId = null) =>
        Modules.Housing.Domain.Neighborhoods.NeighborhoodProfile.Create(
            NeighborhoodProfileId.From(Guid.NewGuid()),
            areaId ?? Guid.NewGuid(),
            "GB",
            "Peckham",
            "peckham",
            Now,
            cityName: "London",
            areaPath: "/GB/greater-london/london/peckham").Value;

    public static NeighborhoodInsight Insight(
        NeighborhoodProfile profile,
        UserId? authorId = null,
        string body = "Quiet after ten, and the buses run all night.") =>
        NeighborhoodInsight.Submit(
            NeighborhoodInsightId.From(Guid.NewGuid()),
            profile.Id,
            profile.AreaId,
            authorId ?? NewUserId(),
            NeighborhoodInsightCategory.Noise,
            body,
            "quiet after ten",
            Now).Value;
}
