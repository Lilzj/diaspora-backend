using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns <c>HousingErrors.ListingNotFound</c>
/// rather than assembling a code and a message at the failure site. That keeps the message
/// consistent between the places the same failure arises, and makes the set enumerable — a unit
/// test asserts every constant here has a matching entry in <see cref="HousingErrorCodes"/> and
/// vice versa.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. No place in the module names a number. Two choices are worth stating:
/// <see cref="RevisionConflict"/> is a conflict so an editor can offer a merge rather than an
/// error page, and everything a caller must not be able to probe for — a hidden listing, a
/// deleted one, an unapproved insight — is a not-found rather than a forbidden, because a 403 on
/// a hidden listing confirms it exists.
/// </para>
/// <para>
/// <b><see cref="AddressNotDisclosed"/> is the deliberate exception to that rule.</b> It is a
/// forbidden rather than a not-found, because the listing plainly exists — the caller is reading
/// it — and pretending the address field is absent would leave a member unable to learn that
/// booking a viewing is how they get it.
/// </para>
/// </summary>
public static class HousingErrors
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public static readonly Error ListingNotFound = Error.NotFound(
        HousingErrorCodes.ListingNotFound,
        "That property does not exist, or you cannot see it.");

    public static readonly Error LandlordNotFound = Error.NotFound(
        HousingErrorCodes.LandlordNotFound,
        "That landlord does not exist.");

    public static readonly Error ViewingNotFound = Error.NotFound(
        HousingErrorCodes.ViewingNotFound,
        "That viewing does not exist.");

    public static readonly Error ReservationNotFound = Error.NotFound(
        HousingErrorCodes.ReservationNotFound,
        "That reservation does not exist.");

    public static readonly Error NeighborhoodNotFound = Error.NotFound(
        HousingErrorCodes.NeighborhoodNotFound,
        "We do not know that neighbourhood.");

    public static readonly Error NeighborhoodProfileNotFound = Error.NotFound(
        HousingErrorCodes.NeighborhoodProfileNotFound,
        "Nobody has described that neighbourhood yet.");

    public static readonly Error InsightNotFound = Error.NotFound(
        HousingErrorCodes.InsightNotFound,
        "That insight does not exist, or it has not been published.");

    public static readonly Error AccommodationTypeNotFound = Error.NotFound(
        HousingErrorCodes.AccommodationTypeNotFound,
        "That accommodation type is not in the catalogue.");

    public static readonly Error AmenityNotFound = Error.NotFound(
        HousingErrorCodes.AmenityNotFound,
        "That amenity is not in the catalogue.");

    public static readonly Error ReportNotFound = Error.NotFound(
        HousingErrorCodes.ReportNotFound,
        "That report does not exist.");

    public static readonly Error ListingHidden = Error.NotFound(
        HousingErrorCodes.ListingHidden,
        "That property does not exist, or you cannot see it.");

    public static readonly Error ListingDeleted = Error.NotFound(
        HousingErrorCodes.ListingDeleted,
        "That property does not exist, or you cannot see it.");

    public static readonly Error DraftNotVisible = Error.NotFound(
        HousingErrorCodes.DraftNotVisible,
        "That property does not exist, or you cannot see it.");

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        HousingErrorCodes.NotAuthenticated,
        "Sign in to do that.");

    public static readonly Error NotLandlord = Error.Forbidden(
        HousingErrorCodes.NotLandlord,
        "Set up a landlord profile before listing a property.");

    public static readonly Error NotListingOwner = Error.Forbidden(
        HousingErrorCodes.NotListingOwner,
        "That property belongs to somebody else.");

    public static readonly Error NotSeeker = Error.Forbidden(
        HousingErrorCodes.NotSeeker,
        "That belongs to somebody else.");

    public static readonly Error NotViewingParty = Error.NotFound(
        HousingErrorCodes.NotViewingParty,
        "That viewing does not exist.");

    public static readonly Error NotReservationParty = Error.NotFound(
        HousingErrorCodes.NotReservationParty,
        "That reservation does not exist.");

    public static readonly Error NotInsightAuthor = Error.Forbidden(
        HousingErrorCodes.NotInsightAuthor,
        "That insight was written by somebody else.");

    public static readonly Error NotModerator = Error.Forbidden(
        HousingErrorCodes.NotModerator,
        "That needs a moderator.");

    public static readonly Error LandlordSuspended = Error.Forbidden(
        HousingErrorCodes.LandlordSuspended,
        "This landlord account is suspended and cannot let property.");

    public static readonly Error LandlordNotVerified = Error.Forbidden(
        HousingErrorCodes.LandlordNotVerified,
        "This action needs a verified landlord.");

    /// <summary>
    /// Forbidden rather than not-found, and deliberately. See the class summary.
    /// </summary>
    public static readonly Error AddressNotDisclosed = Error.Forbidden(
        HousingErrorCodes.AddressNotDisclosed,
        "The full address is shared once a viewing is confirmed.");

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        HousingErrorCodes.RevisionConflict,
        "Somebody else changed this while you were editing. Reload and try again.");

    // ── Listing lifecycle ───────────────────────────────────────────────────────────────────

    public static readonly Error ListingNotPublishable = Error.Validation(
        HousingErrorCodes.ListingNotPublishable,
        "This property is not ready to publish. Check the required details.");

    public static readonly Error ListingAlreadyPublished = Error.Conflict(
        HousingErrorCodes.ListingAlreadyPublished,
        "This property is already published.");

    public static readonly Error ListingNotPublished = Error.Validation(
        HousingErrorCodes.ListingNotPublished,
        "This property has not been published.");

    public static readonly Error ListingNotAvailable = Error.Conflict(
        HousingErrorCodes.ListingNotAvailable,
        "This property is not available right now.");

    public static readonly Error ListingReserved = Error.Conflict(
        HousingErrorCodes.ListingReserved,
        "This property is reserved.");

    public static readonly Error ListingOccupied = Error.Conflict(
        HousingErrorCodes.ListingOccupied,
        "This property is occupied.");

    public static readonly Error ListingUnavailable = Error.Conflict(
        HousingErrorCodes.ListingUnavailable,
        "This property has been taken off the market for now.");

    public static readonly Error ListingArchivedImmutable = Error.Conflict(
        HousingErrorCodes.ListingArchivedImmutable,
        "This property is archived. Restore it before editing.");

    public static readonly Error ListingNotArchived = Error.Validation(
        HousingErrorCodes.ListingNotArchived,
        "This property is not archived.");

    public static readonly Error ListingNotDeleted = Error.Validation(
        HousingErrorCodes.ListingNotDeleted,
        "This property has not been deleted.");

    public static readonly Error ListingTransitionInvalid = Error.Conflict(
        HousingErrorCodes.ListingTransitionInvalid,
        "This property cannot move to that state from where it is.");

    public static readonly Error AvailableFromInPast = Error.Validation(
        HousingErrorCodes.AvailableFromInPast,
        "The available-from date is in the past.");

    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        HousingErrorCodes.TitleInvalid,
        "Give the property a title between 3 and 140 characters.");

    public static readonly Error SlugInvalid = Error.Validation(
        HousingErrorCodes.SlugInvalid,
        "That web address is not valid.");

    public static readonly Error SlugTaken = Error.Conflict(
        HousingErrorCodes.SlugTaken,
        "That web address is already in use.");

    public static readonly Error DescriptionInvalid = Error.Validation(
        HousingErrorCodes.DescriptionInvalid,
        "Describe the property. A summary is required.");

    public static readonly Error SummaryTooLong = Error.Validation(
        HousingErrorCodes.SummaryTooLong,
        "The summary is too long.");

    public static readonly Error PriceInvalid = Error.Validation(
        HousingErrorCodes.PriceInvalid,
        "That rent is not a valid amount.");

    public static readonly Error PriceRequired = Error.Validation(
        HousingErrorCodes.PriceRequired,
        "State the rent, or mark the price as on application.");

    public static readonly Error DepositInvalid = Error.Validation(
        HousingErrorCodes.DepositInvalid,
        "That deposit is not a valid amount.");

    public static readonly Error CurrencyInvalid = Error.Validation(
        HousingErrorCodes.CurrencyInvalid,
        "That is not a currency we recognise.");

    public static readonly Error CurrencyMismatch = Error.Validation(
        HousingErrorCodes.CurrencyMismatch,
        "The deposit must be in the same currency as the rent.");

    public static readonly Error AccommodationTypeInvalid = Error.Validation(
        HousingErrorCodes.AccommodationTypeInvalid,
        "Choose an accommodation type.");

    public static readonly Error AccommodationTypeInactive = Error.Validation(
        HousingErrorCodes.AccommodationTypeInactive,
        "That accommodation type is no longer in use.");

    public static readonly Error AccommodationTypeKeyTaken = Error.Conflict(
        HousingErrorCodes.AccommodationTypeKeyTaken,
        "An accommodation type with that key already exists.");

    public static readonly Error AmenityInvalid = Error.Validation(
        HousingErrorCodes.AmenityInvalid,
        "That amenity is not valid.");

    public static readonly Error AmenityInactive = Error.Validation(
        HousingErrorCodes.AmenityInactive,
        "That amenity is no longer in use.");

    public static readonly Error AmenityKeyTaken = Error.Conflict(
        HousingErrorCodes.AmenityKeyTaken,
        "An amenity with that key already exists.");

    public static readonly Error AmenityLimit = Error.Validation(
        HousingErrorCodes.AmenityLimit,
        "That is more amenities than one property can list.");

    public static readonly Error CapacityInvalid = Error.Validation(
        HousingErrorCodes.CapacityInvalid,
        "The bedroom and bathroom counts are not valid.");

    public static readonly Error OccupancyInvalid = Error.Validation(
        HousingErrorCodes.OccupancyInvalid,
        "The maximum number of occupants is not valid.");

    public static readonly Error LeaseTypeInvalid = Error.Validation(
        HousingErrorCodes.LeaseTypeInvalid,
        "That lease type is not valid.");

    public static readonly Error StayPolicyInvalid = Error.Validation(
        HousingErrorCodes.StayPolicyInvalid,
        "A property must accept either short stays or long stays.");

    public static readonly Error HouseRuleInvalid = Error.Validation(
        HousingErrorCodes.HouseRuleInvalid,
        "That house rule is not valid.");

    public static readonly Error HouseRuleLimit = Error.Validation(
        HousingErrorCodes.HouseRuleLimit,
        "That is more house rules than one property can list.");

    public static readonly Error MediaInvalid = Error.Validation(
        HousingErrorCodes.MediaInvalid,
        "That media reference is not valid.");

    public static readonly Error MediaLimit = Error.Validation(
        HousingErrorCodes.MediaLimit,
        "That is more media than one property can carry.");

    public static readonly Error CoverImageRequired = Error.Validation(
        HousingErrorCodes.CoverImageRequired,
        "Add at least one photograph before publishing.");

    public static readonly Error CountryInvalid = Error.Validation(
        HousingErrorCodes.CountryInvalid,
        "Say which country the property is in.");

    public static readonly Error LocationInvalid = Error.Validation(
        HousingErrorCodes.LocationInvalid,
        "That location is not valid.");

    public static readonly Error CoordinatesInvalid = Error.Validation(
        HousingErrorCodes.CoordinatesInvalid,
        "Those coordinates are not on the Earth.");

    public static readonly Error AddressInvalid = Error.Validation(
        HousingErrorCodes.AddressInvalid,
        "That address is not valid.");

    public static readonly Error ModerationNoteInvalid = Error.Validation(
        HousingErrorCodes.ModerationNoteInvalid,
        "That moderation note is not valid.");

    public static readonly Error ProviderReferenceInvalid = Error.Validation(
        HousingErrorCodes.ProviderReferenceInvalid,
        "That provider reference is not valid.");

    public static readonly Error GuideReferenceInvalid = Error.Validation(
        HousingErrorCodes.GuideReferenceInvalid,
        "That guide reference is not valid.");

    // ── Roommate matching ───────────────────────────────────────────────────────────────────

    public static readonly Error GenderPreferenceNotPermitted = Error.Validation(
        HousingErrorCodes.GenderPreferenceNotPermitted,
        "A gender preference can only be set on a shared room or a shared property.");

    public static readonly Error RoommatePreferencesNotPermitted = Error.Validation(
        HousingErrorCodes.RoommatePreferencesNotPermitted,
        "Roommate preferences only apply to shared accommodation.");

    public static readonly Error RoommateLanguageLimit = Error.Validation(
        HousingErrorCodes.RoommateLanguageLimit,
        "That is more languages than one household can list.");

    // ── Units & availability ────────────────────────────────────────────────────────────────

    public static readonly Error UnitsInvalid = Error.Validation(
        HousingErrorCodes.UnitsInvalid,
        "The number of units is not valid.");

    public static readonly Error NoUnitsAvailable = Error.Conflict(
        HousingErrorCodes.NoUnitsAvailable,
        "There is nothing left to reserve here.");

    public static readonly Error UnitsBelowCommitted = Error.Conflict(
        HousingErrorCodes.UnitsBelowCommitted,
        "You cannot reduce the units below what is already reserved or occupied.");

    public static readonly Error AvailabilityWindowInvalid = Error.Validation(
        HousingErrorCodes.AvailabilityWindowInvalid,
        "The availability dates are the wrong way round.");

    public static readonly Error StayLengthInvalid = Error.Validation(
        HousingErrorCodes.StayLengthInvalid,
        "The minimum stay cannot be longer than the maximum stay.");

    // ── Landlord ────────────────────────────────────────────────────────────────────────────

    public static readonly Error LandlordAlreadyExists = Error.Conflict(
        HousingErrorCodes.LandlordAlreadyExists,
        "You already have a landlord profile.");

    public static readonly Error LandlordNameInvalid = Error.Validation(
        HousingErrorCodes.LandlordNameInvalid,
        "Give the landlord profile a name.");

    public static readonly Error LandlordSlugTaken = Error.Conflict(
        HousingErrorCodes.LandlordSlugTaken,
        "That landlord web address is already in use.");

    public static readonly Error LandlordContactInvalid = Error.Validation(
        HousingErrorCodes.LandlordContactInvalid,
        "Those contact details are not valid.");

    public static readonly Error LandlordKindInvalid = Error.Validation(
        HousingErrorCodes.LandlordKindInvalid,
        "That landlord type is not valid.");

    public static readonly Error LandlordProviderUnknown = Error.Validation(
        HousingErrorCodes.LandlordProviderUnknown,
        "We cannot find that provider, or it is not yours.");

    // ── Viewings ────────────────────────────────────────────────────────────────────────────

    public static readonly Error ViewingsNotAccepted = Error.Conflict(
        HousingErrorCodes.ViewingsNotAccepted,
        "This property is not taking viewing requests.");

    public static readonly Error ViewingAlreadyOpen = Error.Conflict(
        HousingErrorCodes.ViewingAlreadyOpen,
        "You already have a viewing open on this property.");

    public static readonly Error ViewingAlreadyClosed = Error.Conflict(
        HousingErrorCodes.ViewingAlreadyClosed,
        "That viewing has already finished.");

    public static readonly Error ViewingTransitionInvalid = Error.Conflict(
        HousingErrorCodes.ViewingTransitionInvalid,
        "That viewing cannot move to that state from where it is.");

    public static readonly Error ViewingSelf = Error.Validation(
        HousingErrorCodes.ViewingSelf,
        "You cannot book a viewing on your own property.");

    public static readonly Error ViewingWindowInvalid = Error.Validation(
        HousingErrorCodes.ViewingWindowInvalid,
        "That viewing time is not valid.");

    public static readonly Error ViewingSlotInPast = Error.Validation(
        HousingErrorCodes.ViewingSlotInPast,
        "That viewing time has already passed.");

    public static readonly Error TooManyViewings = Error.RateLimited(
        HousingErrorCodes.TooManyViewings,
        "You have too many viewings open. Close some before booking more.");

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    public static readonly Error ReservationsNotAccepted = Error.Conflict(
        HousingErrorCodes.ReservationsNotAccepted,
        "This property is not taking reservations.");

    public static readonly Error ReservationAlreadyOpen = Error.Conflict(
        HousingErrorCodes.ReservationAlreadyOpen,
        "You already have a reservation open on this property.");

    public static readonly Error ReservationAlreadyClosed = Error.Conflict(
        HousingErrorCodes.ReservationAlreadyClosed,
        "That reservation has already finished.");

    public static readonly Error ReservationTransitionInvalid = Error.Conflict(
        HousingErrorCodes.ReservationTransitionInvalid,
        "That reservation cannot move to that state from where it is.");

    public static readonly Error ReservationSelf = Error.Validation(
        HousingErrorCodes.ReservationSelf,
        "You cannot reserve your own property.");

    public static readonly Error ReservationWindowInvalid = Error.Validation(
        HousingErrorCodes.ReservationWindowInvalid,
        "Those reservation dates are not valid.");

    public static readonly Error ReservationRequiresViewing = Error.Conflict(
        HousingErrorCodes.ReservationRequiresViewing,
        "This landlord asks for a viewing before a reservation.");

    public static readonly Error TooManyReservations = Error.RateLimited(
        HousingErrorCodes.TooManyReservations,
        "You are holding too many properties. Release one before reserving another.");

    // ── Neighbourhoods & insights ───────────────────────────────────────────────────────────

    public static readonly Error NeighborhoodAreaInvalid = Error.Validation(
        HousingErrorCodes.NeighborhoodAreaInvalid,
        "A neighbourhood profile must name a neighbourhood in the location catalogue.");

    public static readonly Error NeighborhoodAlreadyProfiled = Error.Conflict(
        HousingErrorCodes.NeighborhoodAlreadyProfiled,
        "That neighbourhood already has a profile.");

    public static readonly Error NeighborhoodSummaryInvalid = Error.Validation(
        HousingErrorCodes.NeighborhoodSummaryInvalid,
        "That neighbourhood summary is too long.");

    public static readonly Error NeighborhoodLanguageLimit = Error.Validation(
        HousingErrorCodes.NeighborhoodLanguageLimit,
        "That is more languages than one neighbourhood profile can list.");

    public static readonly Error InsightBodyInvalid = Error.Validation(
        HousingErrorCodes.InsightBodyInvalid,
        "Say something about the neighbourhood, in 400 characters or fewer.");

    public static readonly Error InsightAlreadyDecided = Error.Conflict(
        HousingErrorCodes.InsightAlreadyDecided,
        "A moderator has already decided on that insight.");

    public static readonly Error InsightNotApproved = Error.Conflict(
        HousingErrorCodes.InsightNotApproved,
        "That insight has not been approved.");

    public static readonly Error InsightDuplicate = Error.Conflict(
        HousingErrorCodes.InsightDuplicate,
        "You have already said that about this neighbourhood.");

    public static readonly Error TooManyInsights = Error.RateLimited(
        HousingErrorCodes.TooManyInsights,
        "You have submitted a lot of insights today. Try again tomorrow.");

    // ── Reports & moderation ────────────────────────────────────────────────────────────────

    public static readonly Error ReportReasonInvalid = Error.Validation(
        HousingErrorCodes.ReportReasonInvalid,
        "Choose a reason for the report.");

    public static readonly Error ReportAlreadyFiled = Error.Conflict(
        HousingErrorCodes.ReportAlreadyFiled,
        "You have already reported this.");

    public static readonly Error ReportAlreadyResolved = Error.Conflict(
        HousingErrorCodes.ReportAlreadyResolved,
        "That report has already been dealt with.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        HousingErrorCodes.VerificationUnavailable,
        "Verification is not available right now. Try again shortly.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        HousingErrorCodes.VerificationAlreadyPending,
        "A verification for this is already under review.");

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public static readonly Error TooManyListings = Error.RateLimited(
        HousingErrorCodes.TooManyListings,
        "You have reached the limit for live properties.");

    public static readonly Error SearchQueryInvalid = Error.Validation(
        HousingErrorCodes.SearchQueryInvalid,
        "That search is not valid.");

    public static readonly Error SuspectedFraud = Error.Forbidden(
        HousingErrorCodes.SuspectedFraud,
        "This listing has been held for review. We will be in touch.");

    public static readonly Error DuplicateListing = Error.Conflict(
        HousingErrorCodes.DuplicateListing,
        "You already have a live listing with that title.");
}
