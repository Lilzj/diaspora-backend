namespace Diaspora.Modules.Housing;

/// <summary>
/// The stable wire-level error codes the Housing module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them, and
/// translations key off them. Change the description on an <c>Error</c> freely; never change a
/// code. New failures get new codes.
/// </para>
/// </summary>
public static class HousingErrorCodes
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public const string ListingNotFound = "housing.listing.not-found";
    public const string LandlordNotFound = "housing.landlord.not-found";
    public const string ViewingNotFound = "housing.viewing.not-found";
    public const string ReservationNotFound = "housing.reservation.not-found";
    public const string NeighborhoodNotFound = "housing.neighborhood.not-found";
    public const string NeighborhoodProfileNotFound = "housing.neighborhood.profile.not-found";
    public const string InsightNotFound = "housing.insight.not-found";
    public const string AccommodationTypeNotFound = "housing.accommodation-type.not-found";
    public const string AmenityNotFound = "housing.amenity.not-found";
    public const string ReportNotFound = "housing.report.not-found";
    public const string ListingHidden = "housing.listing.hidden";
    public const string ListingDeleted = "housing.listing.deleted";
    public const string DraftNotVisible = "housing.draft.not-visible";

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public const string NotAuthenticated = "housing.not-authenticated";
    public const string NotLandlord = "housing.not-landlord";
    public const string NotListingOwner = "housing.listing.not-owned";
    public const string NotSeeker = "housing.not-seeker";
    public const string NotViewingParty = "housing.viewing.not-party";
    public const string NotReservationParty = "housing.reservation.not-party";
    public const string NotInsightAuthor = "housing.insight.not-author";
    public const string NotModerator = "housing.not-moderator";
    public const string LandlordSuspended = "housing.landlord.suspended";
    public const string LandlordNotVerified = "housing.landlord.not-verified";
    public const string AddressNotDisclosed = "housing.address.not-disclosed";

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public const string RevisionConflict = "housing.revision-conflict";

    // ── Listing lifecycle ───────────────────────────────────────────────────────────────────

    public const string ListingNotPublishable = "housing.listing.not-publishable";
    public const string ListingAlreadyPublished = "housing.listing.already-published";
    public const string ListingNotPublished = "housing.listing.not-published";
    public const string ListingNotAvailable = "housing.listing.not-available";
    public const string ListingReserved = "housing.listing.reserved";
    public const string ListingOccupied = "housing.listing.occupied";
    public const string ListingUnavailable = "housing.listing.unavailable";
    public const string ListingArchivedImmutable = "housing.listing.archived-immutable";
    public const string ListingNotArchived = "housing.listing.not-archived";
    public const string ListingNotDeleted = "housing.listing.not-deleted";
    public const string ListingTransitionInvalid = "housing.listing.transition-invalid";
    public const string AvailableFromInPast = "housing.listing.available-from.in-past";

    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public const string TitleInvalid = "housing.title.invalid";
    public const string SlugInvalid = "housing.slug.invalid";
    public const string SlugTaken = "housing.slug.taken";
    public const string DescriptionInvalid = "housing.description.invalid";
    public const string SummaryTooLong = "housing.summary.too-long";
    public const string PriceInvalid = "housing.price.invalid";
    public const string PriceRequired = "housing.price.required";

    /// <summary>
    /// A rent was given but the period it covers was missing or unrecognised.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="PriceRequired"/> on purpose. Both used to answer
    /// "State the rent, or mark the price as on application." — which named the wrong field
    /// to a landlord who was looking at the rent they had just typed (UAT-113).
    /// </remarks>
    public const string PricePeriodRequired = "housing.price.period-required";
    public const string DepositInvalid = "housing.deposit.invalid";
    public const string CurrencyInvalid = "housing.currency.invalid";
    public const string CurrencyMismatch = "housing.currency.mismatch";
    public const string AccommodationTypeInvalid = "housing.accommodation-type.invalid";
    public const string AccommodationTypeInactive = "housing.accommodation-type.inactive";
    public const string AccommodationTypeKeyTaken = "housing.accommodation-type.key-taken";
    public const string AmenityInvalid = "housing.amenity.invalid";
    public const string AmenityInactive = "housing.amenity.inactive";
    public const string AmenityKeyTaken = "housing.amenity.key-taken";
    public const string AmenityLimit = "housing.amenities.limit";
    public const string CapacityInvalid = "housing.capacity.invalid";
    public const string OccupancyInvalid = "housing.occupancy.invalid";
    public const string LeaseTypeInvalid = "housing.lease-type.invalid";
    public const string StayPolicyInvalid = "housing.stay-policy.invalid";
    public const string HouseRuleInvalid = "housing.house-rule.invalid";
    public const string HouseRuleLimit = "housing.house-rules.limit";
    public const string MediaInvalid = "housing.media.invalid";
    public const string MediaLimit = "housing.media.limit";
    public const string CoverImageRequired = "housing.media.cover-required";
    public const string CountryInvalid = "housing.country.invalid";
    public const string LocationInvalid = "housing.location.invalid";
    public const string CoordinatesInvalid = "housing.coordinates.invalid";
    public const string AddressInvalid = "housing.address.invalid";
    public const string ModerationNoteInvalid = "housing.moderation-note.invalid";
    public const string ProviderReferenceInvalid = "housing.provider-reference.invalid";
    public const string GuideReferenceInvalid = "housing.guide-reference.invalid";

    // ── Roommate matching ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// A gender preference on a whole property. Refused, and the one content rule in this module
    /// that exists for a legal reason rather than a product one.
    /// </summary>
    public const string GenderPreferenceNotPermitted = "housing.roommate.gender-not-permitted";

    public const string RoommatePreferencesNotPermitted = "housing.roommate.not-shared";
    public const string RoommateLanguageLimit = "housing.roommate.languages.limit";

    // ── Units & availability ────────────────────────────────────────────────────────────────

    public const string UnitsInvalid = "housing.units.invalid";
    public const string NoUnitsAvailable = "housing.units.none-available";
    public const string UnitsBelowCommitted = "housing.units.below-committed";
    public const string AvailabilityWindowInvalid = "housing.availability.window-invalid";
    public const string StayLengthInvalid = "housing.stay-length.invalid";

    // ── Landlord ────────────────────────────────────────────────────────────────────────────

    public const string LandlordAlreadyExists = "housing.landlord.already-exists";
    public const string LandlordNameInvalid = "housing.landlord.name.invalid";
    public const string LandlordSlugTaken = "housing.landlord.slug.taken";
    public const string LandlordContactInvalid = "housing.landlord.contact.invalid";
    public const string LandlordKindInvalid = "housing.landlord.kind.invalid";
    public const string LandlordProviderUnknown = "housing.landlord.provider-unknown";

    // ── Viewings ────────────────────────────────────────────────────────────────────────────

    public const string ViewingsNotAccepted = "housing.viewing.not-accepted-here";
    public const string ViewingAlreadyOpen = "housing.viewing.already-open";
    public const string ViewingAlreadyClosed = "housing.viewing.already-closed";
    public const string ViewingTransitionInvalid = "housing.viewing.transition-invalid";
    public const string ViewingSelf = "housing.viewing.self";
    public const string ViewingWindowInvalid = "housing.viewing.window.invalid";
    public const string ViewingSlotInPast = "housing.viewing.slot.in-past";
    public const string TooManyViewings = "housing.viewings.too-many";

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    public const string ReservationsNotAccepted = "housing.reservation.not-accepted-here";
    public const string ReservationAlreadyOpen = "housing.reservation.already-open";
    public const string ReservationAlreadyClosed = "housing.reservation.already-closed";
    public const string ReservationTransitionInvalid = "housing.reservation.transition-invalid";
    public const string ReservationSelf = "housing.reservation.self";
    public const string ReservationWindowInvalid = "housing.reservation.window.invalid";
    public const string ReservationRequiresViewing = "housing.reservation.viewing-required";
    public const string TooManyReservations = "housing.reservations.too-many";

    // ── Neighbourhoods & insights ───────────────────────────────────────────────────────────

    public const string NeighborhoodAreaInvalid = "housing.neighborhood.area-invalid";
    public const string InsightPointInvalid = "housing.insight.point-invalid";
    public const string NeighborhoodAlreadyProfiled = "housing.neighborhood.already-profiled";
    public const string NeighborhoodSummaryInvalid = "housing.neighborhood.summary.invalid";
    public const string NeighborhoodLanguageLimit = "housing.neighborhood.languages.limit";
    public const string InsightBodyInvalid = "housing.insight.body.invalid";
    public const string InsightAlreadyDecided = "housing.insight.already-decided";
    public const string InsightNotApproved = "housing.insight.not-approved";
    public const string InsightDuplicate = "housing.insight.duplicate";
    public const string TooManyInsights = "housing.insights.too-many";

    // ── Reports & moderation ────────────────────────────────────────────────────────────────

    public const string ReportReasonInvalid = "housing.report.reason.invalid";
    public const string ReportAlreadyFiled = "housing.report.already-filed";
    public const string ReportAlreadyResolved = "housing.report.already-resolved";

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const string VerificationUnavailable = "housing.verification.unavailable";
    public const string VerificationAlreadyPending = "housing.verification.already-pending";

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public const string TooManyListings = "housing.listings.too-many";
    public const string SearchQueryInvalid = "housing.search.query-invalid";
    public const string SuspectedFraud = "housing.fraud.suspected";
    public const string DuplicateListing = "housing.listing.duplicate";

    // ── Reviews ─────────────────────────────────────────────────────────────────────────────

    public const string ReviewNotFound = "housing.review.not-found";
    public const string ReviewSelf = "housing.review.self";
    public const string ReviewRatingOutOfRange = "housing.review.rating-out-of-range";
    public const string ReviewNoTenancy = "housing.review.no-tenancy";
    public const string ReviewAlreadyWritten = "housing.review.already-written";
    public const string ReviewNotEditable = "housing.review.not-editable";
    public const string ReviewEditWindowClosed = "housing.review.edit-window-closed";
    public const string ReviewResponseEmpty = "housing.review.response-empty";
}
