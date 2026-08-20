using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns listing aggregates into wire responses.
/// <para>
/// <b>Static, because it holds no state.</b> Registering a stateless type is a container entry
/// nobody resolves, and CA1822 in Release is what pushed B10's mappers static in the first place.
/// </para>
/// <para>
/// <b>The address is never mapped here.</b> It arrives already redacted from
/// <c>HousingAddressDisclosureService</c>, which is the only thing in the module allowed to decide
/// how much of one a viewer sees. A mapper that reached into <c>listing.Address</c> directly would
/// be the one place the privacy rule could be forgotten, and an architecture test asserts it does
/// not.
/// </para>
/// <para>
/// <b>Documents are excluded from the media list for anybody but the parties.</b> A lease document
/// or an inspection report attached to a property is not gallery content, and listing its
/// identifier publicly would let anybody with the reference try to fetch it from Media.
/// </para>
/// </summary>
public static class HousingListingMapper
{
    /// <summary>The full detail response.</summary>
    public static HousingListingResponse ToResponse(
        HousingListing listing,
        LandlordProfile landlord,
        PropertyAddressResponse address,
        IReadOnlyDictionary<string, HousingAmenity> amenityCatalog,
        IReadOnlyDictionary<string, AccommodationType> typeCatalog,
        bool isOwner,
        bool isStaff,
        bool hasViewing,
        bool hasReservation,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(landlord);
        ArgumentNullException.ThrowIfNull(address);

        var canSeeDocuments = isOwner || isStaff || hasViewing || hasReservation;

        return new HousingListingResponse
        {
            Id = listing.Id.Value,
            Slug = listing.Slug.Value,
            Title = listing.Title.Value,
            Summary = listing.Description.Summary,
            Description = listing.Description.Body,
            AccommodationTypeKey = listing.AccommodationTypeKey,
            AccommodationTypeName = TypeName(listing.AccommodationTypeKey, typeCatalog),

            Status = listing.Status.ToString(),
            Visibility = listing.Visibility.ToString(),

            // The moderation state is a fact about a decision taken against the landlord. A browser
            // has no business seeing that a listing has been flagged.
            ModerationState = isOwner || isStaff ? listing.ModerationState.ToString() : null,

            IsFeatured = listing.IsFeatured,
            PublishedAt = listing.PublishedAt,
            AcceptsViewings = listing.AcceptsViewings,
            AcceptsReservations = listing.AcceptsReservations(now),
            RequiresViewingBeforeReservation = listing.RequiresViewingBeforeReservation,

            Price = ToPrice(listing),
            Capacity = ToCapacity(listing),
            Availability = ToAvailability(listing),
            Location = ToLocation(listing),
            Address = address,
            StayPolicy = ToStayPolicy(listing),

            // Absent entirely on anything that is not shared accommodation, rather than present and
            // empty: a gender preference rendered beside a whole flat is the discriminatory advert
            // this module refuses to publish.
            RoommatePreferences = listing.StayPolicy.IsSharedAccommodation
                ? ToRoommatePreferences(listing)
                : null,

            LeaseType = listing.LeaseType.ToString(),
            FurnishedStatus = listing.FurnishedStatus.ToString(),
            Parking = listing.Parking.ToString(),
            SmokingPolicy = listing.SmokingPolicy.ToString(),
            PetPolicy = listing.PetPolicy.ToString(),

            Amenities = ToAmenities(listing, amenityCatalog),
            HouseRules = [.. listing.HouseRules.Select(ToHouseRule)],
            Media = [.. listing.MediaReferences
                .Where(media => canSeeDocuments || !media.IsDocument)
                .OrderBy(media => media.Role)
                .ThenBy(media => media.Order)
                .Select(ToMedia)],
            CoverImage = listing.CoverImage is { } cover ? ToMedia(cover) : null,
            Providers = [.. listing.ProviderReferences.Select(ToProvider)],
            Guides = [.. listing.GuideReferences
                .OrderBy(guide => guide.Order)
                .Select(ToGuide)],

            Landlord = HousingLandlordMapper.ToSummary(landlord),
            Trust = ToTrust(listing),
            Engagement = ToEngagement(listing),

            IsOwner = isOwner,
            HasViewing = hasViewing,
            HasReservation = hasReservation,
            Revision = listing.Revision,
            CreatedAt = listing.CreatedAt,
            ModifiedAt = listing.ModifiedAt
        };
    }

    /// <summary>
    /// The card a results page renders.
    /// <para>
    /// Carries no address at all, at any disclosure level. A results page is the last place a
    /// street name belongs, and omitting the field entirely means no client can accidentally
    /// render one.
    /// </para>
    /// </summary>
    public static HousingListingCardResponse ToCard(
        HousingListing listing,
        LandlordProfile? landlord,
        IReadOnlyDictionary<string, HousingAmenity> amenityCatalog,
        IReadOnlyDictionary<string, AccommodationType> typeCatalog)
    {
        ArgumentNullException.ThrowIfNull(listing);

        return new HousingListingCardResponse
        {
            Id = listing.Id.Value,
            Slug = listing.Slug.Value,
            Title = listing.Title.Value,
            Summary = listing.Description.Summary,
            AccommodationTypeKey = listing.AccommodationTypeKey,
            AccommodationTypeName = TypeName(listing.AccommodationTypeKey, typeCatalog),
            Status = listing.Status.ToString(),
            Price = ToPrice(listing),
            Capacity = ToCapacity(listing),
            Availability = ToAvailability(listing),
            Location = ToLocation(listing),
            StayPolicy = ToStayPolicy(listing),
            FurnishedStatus = listing.FurnishedStatus.ToString(),
            CoverImage = listing.CoverImage is { } cover ? ToMedia(cover) : null,
            Amenities = ToAmenities(listing, amenityCatalog),
            Landlord = landlord is null
                ? HousingLandlordMapper.Unknown
                : HousingLandlordMapper.ToSummary(landlord),
            IsLandlordVerified = landlord?.IsVerified ?? false,
            IsFeatured = listing.IsFeatured,
            PublishedAt = listing.PublishedAt
        };
    }

    public static HousingPriceResponse ToPrice(HousingListing listing) => new()
    {
        IsDisclosed = listing.Price.IsDisclosed,
        Amount = listing.Price.Amount,
        CurrencyCode = listing.Price.CurrencyCode,
        Period = listing.Price.Period == RentPeriod.None
            ? null
            : listing.Price.Period.ToString(),
        DepositAmount = listing.Price.DepositAmount,
        DepositRequired = listing.Price.DepositRequired,
        MoveInTotal = listing.Price.MoveInTotal,
        IsNegotiable = listing.Price.IsNegotiable,
        UtilitiesIncluded = listing.Price.UtilitiesIncluded,
        InternetIncluded = listing.Price.InternetIncluded
    };

    public static HousingCapacityResponse ToCapacity(HousingListing listing) => new()
    {
        Bedrooms = listing.Capacity.Bedrooms,
        Bathrooms = listing.Capacity.Bathrooms,
        MaximumOccupants = listing.Capacity.MaximumOccupants,
        FloorAreaSquareMetres = listing.Capacity.FloorAreaSquareMetres,
        IsStudio = listing.Capacity.IsStudio
    };

    public static HousingAvailabilityResponse ToAvailability(HousingListing listing) => new()
    {
        AvailableFrom = listing.Availability.AvailableFrom,
        AvailableUntil = listing.Availability.AvailableUntil,
        TotalUnits = listing.Availability.TotalUnits,
        AvailableUnits = listing.Availability.AvailableUnits,
        ReservedUnits = listing.Availability.ReservedUnits,
        OccupiedUnits = listing.Availability.OccupiedUnits,
        MinimumStayNights = listing.Availability.MinimumStayNights,
        MaximumStayNights = listing.Availability.MaximumStayNights
    };

    public static HousingLocationResponse ToLocation(HousingListing listing) => new()
    {
        CountryCode = listing.Location.CountryCode,
        RegionName = listing.Location.RegionName,
        CityName = listing.Location.CityName,
        NeighborhoodName = listing.Location.NeighborhoodName,
        NeighborhoodAreaId = listing.Location.NeighborhoodAreaId,
        DisplayName = listing.Location.DisplayName
    };

    public static StayPolicyResponse ToStayPolicy(HousingListing listing) => new()
    {
        AcceptsShortStay = listing.StayPolicy.AcceptsShortStay,
        AcceptsLongStay = listing.StayPolicy.AcceptsLongStay,
        IsStudentFriendly = listing.StayPolicy.IsStudentFriendly,
        IsFamilyFriendly = listing.StayPolicy.IsFamilyFriendly,
        IsSharedAccommodation = listing.StayPolicy.IsSharedAccommodation,
        IsLookingForRoommate = listing.StayPolicy.IsLookingForRoommate
    };

    public static RoommatePreferencesResponse ToRoommatePreferences(HousingListing listing) =>
        new()
        {
            GenderPreference = listing.RoommatePreferences.GenderPreference.ToString(),
            Occupation = listing.RoommatePreferences.Occupation.ToString(),
            MinimumAge = listing.RoommatePreferences.MinimumAge,
            MaximumAge = listing.RoommatePreferences.MaximumAge,
            SmokingPolicy = listing.RoommatePreferences.SmokingPolicy.ToString(),
            PetPolicy = listing.RoommatePreferences.PetPolicy.ToString(),
            NoiseTolerance = listing.RoommatePreferences.NoiseTolerance.ToString(),
            NationalityPreference = listing.RoommatePreferences.NationalityPreference,
            Languages = listing.RoommatePreferences.Languages
        };

    public static HousingTrustResponse ToTrust(HousingListing listing) => new()
    {
        State = listing.TrustMark.State.ToString(),
        IsVerified = listing.TrustMark.IsVerified,
        Badge = listing.TrustMark.Badge,
        DecidedAt = listing.TrustMark.DecidedAt
    };

    public static HousingEngagementResponse ToEngagement(HousingListing listing) => new()
    {
        ViewCount = listing.Engagement.ViewCount,
        UniqueViewCount = listing.Engagement.UniqueViewCount,
        SaveCount = listing.Engagement.SaveCount,
        ViewingRequestCount = listing.Engagement.ViewingRequestCount,
        ReservationCount = listing.Engagement.ReservationCount,
        ViewingConversionRate = listing.Engagement.ViewingConversionRate
    };

    public static HousingMediaResponse ToMedia(HousingMediaReference media) => new()
    {
        MediaId = media.MediaId,
        Role = media.Role.ToString(),
        Order = media.Order,
        Caption = media.Caption
    };

    public static HouseRuleResponse ToHouseRule(HouseRule rule) => new()
    {
        RuleId = rule.RuleId,
        Text = rule.Text,
        Order = rule.Order
    };

    public static HousingProviderReferenceResponse ToProvider(
        HousingProviderReference reference) => new()
    {
        ProviderId = reference.ProviderId,
        ProviderName = reference.ProviderName,
        Role = reference.Role
    };

    public static HousingGuideReferenceResponse ToGuide(HousingGuideReference reference) => new()
    {
        GuideId = reference.GuideId,
        Title = reference.Title,
        Slug = reference.Slug
    };

    /// <summary>
    /// Resolves amenity keys against the catalogue.
    /// <para>
    /// A retired amenity falls back to its key rather than disappearing: the property still has the
    /// thing, and blanking it would silently misdescribe a home.
    /// </para>
    /// </summary>
    private static IReadOnlyList<HousingAmenityResponse> ToAmenities(
        HousingListing listing, IReadOnlyDictionary<string, HousingAmenity> catalog) =>
        [.. listing.Amenities.Select(assignment =>
        {
            var known = catalog.GetValueOrDefault(assignment.AmenityKey);

            return new HousingAmenityResponse
            {
                Key = assignment.AmenityKey,
                Name = known?.Name ?? assignment.AmenityKey,
                Group = known?.Group,
                IconKey = known?.IconKey,
                IsAccessibilityFeature = known?.IsAccessibilityFeature ?? false,
                Note = assignment.Note
            };
        })];

    private static string? TypeName(
        string key, IReadOnlyDictionary<string, AccommodationType> catalog) =>
        catalog.GetValueOrDefault(key)?.Name;
}
