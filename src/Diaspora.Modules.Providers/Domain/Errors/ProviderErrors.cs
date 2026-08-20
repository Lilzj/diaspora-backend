using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns <c>ProviderErrors.NotFound</c>
/// rather than assembling a code and a message at the throw site. That is what keeps the
/// message consistent between the two or three places the same failure can arise, and it is
/// what makes the set enumerable — a test asserts that every constant here has a matching entry
/// in <see cref="ProviderErrorCodes"/>, which is how a code added in one place and forgotten in
/// the other is caught before it reaches a client.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. Getting it right here is the whole of B7's status-code contract:
/// there is no place in the module that names a number.
/// </para>
/// </summary>
public static class ProviderErrors
{
    public static readonly Error NotAuthenticated = Error.Unauthorized(
        ProviderErrorCodes.NotAuthenticated,
        "You must be signed in to do this.");

    public static readonly Error Forbidden = Error.Forbidden(
        ProviderErrorCodes.Forbidden,
        "You do not have permission to do this.");

    public static readonly Error EnumUnknown = Error.Validation(
        ProviderErrorCodes.EnumUnknown,
        "That value is not one this endpoint accepts.");

    // ── Provider ────────────────────────────────────────────────────────────────────────────

    public static readonly Error NotFound = Error.NotFound(
        ProviderErrorCodes.ProviderNotFound,
        "That provider does not exist, or you cannot see it.");

    public static readonly Error NameEmpty = Error.Validation(
        ProviderErrorCodes.ProviderNameEmpty,
        "A provider needs a name.");

    public static readonly Error NameTooLong = Error.Validation(
        ProviderErrorCodes.ProviderNameTooLong,
        "That name is too long.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        ProviderErrorCodes.ProviderDescriptionTooLong,
        "That description is too long.");

    public static readonly Error SlugTaken = Error.Conflict(
        ProviderErrorCodes.ProviderSlugTaken,
        "That web address is already in use by another provider.");

    public static readonly Error SlugInvalid = Error.Validation(
        ProviderErrorCodes.ProviderSlugInvalid,
        "A web address may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error NotOwned = Error.Forbidden(
        ProviderErrorCodes.ProviderNotOwned,
        "You are not an owner or manager of this provider.");

    public static readonly Error NotEditable = Error.Conflict(
        ProviderErrorCodes.ProviderNotEditable,
        "This provider cannot be edited in its current state.");

    public static readonly Error AlreadyPublished = Error.Conflict(
        ProviderErrorCodes.ProviderAlreadyPublished,
        "This provider is already published.");

    public static readonly Error NotPublishable = Error.Conflict(
        ProviderErrorCodes.ProviderNotPublishable,
        "This provider is not complete enough to publish. It needs a name, a category and a way to be contacted.");

    public static readonly Error Suspended = Error.Forbidden(
        ProviderErrorCodes.ProviderSuspended,
        "This provider is suspended.");

    public static readonly Error Closed = Error.Conflict(
        ProviderErrorCodes.ProviderClosed,
        "This provider has closed.");

    public static readonly Error LimitReached = Error.Conflict(
        ProviderErrorCodes.ProviderLimitReached,
        "You have reached the maximum number of providers one member may own.");

    public static readonly Error TypeUnsupported = Error.Validation(
        ProviderErrorCodes.ProviderTypeUnsupported,
        "That provider type is not available on this deployment.");

    public static readonly Error Removed = Error.NotFound(
        ProviderErrorCodes.ProviderRemoved,
        "This provider has been removed.");

    // ── Profile & organization ──────────────────────────────────────────────────────────────

    public static readonly Error TaglineTooLong = Error.Validation(
        ProviderErrorCodes.ProfileTaglineTooLong,
        "That tagline is too long.");

    public static readonly Error MissionTooLong = Error.Validation(
        ProviderErrorCodes.ProfileMissionTooLong,
        "That mission statement is too long.");

    public static readonly Error FoundedInFuture = Error.Validation(
        ProviderErrorCodes.ProfileFoundedInFuture,
        "A founding date cannot be in the future.");

    public static readonly Error OrganizationRegistrationRequired = Error.Validation(
        ProviderErrorCodes.OrganizationRegistrationRequired,
        "This provider type needs a registration number.");

    public static readonly Error OrganizationNumberTooLong = Error.Validation(
        ProviderErrorCodes.OrganizationNumberTooLong,
        "That registration number is too long.");

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    public static readonly Error MemberNotFound = Error.NotFound(
        ProviderErrorCodes.MemberNotFound,
        "That person is not attached to this provider.");

    public static readonly Error MemberDuplicate = Error.Conflict(
        ProviderErrorCodes.MemberDuplicate,
        "That person already has a role on this provider.");

    public static readonly Error MemberLastOwner = Error.Conflict(
        ProviderErrorCodes.MemberLastOwner,
        "A provider must keep at least one owner. Make somebody else an owner first.");

    public static readonly Error MemberSelfDemotion = Error.Conflict(
        ProviderErrorCodes.MemberSelfDemotion,
        "You cannot reduce your own role. Ask another owner to do it.");

    public static readonly Error MemberLimitReached = Error.Conflict(
        ProviderErrorCodes.MemberLimitReached,
        "This provider has reached its maximum number of people.");

    public static readonly Error MemberRoleInsufficient = Error.Forbidden(
        ProviderErrorCodes.MemberRoleInsufficient,
        "Your role on this provider does not allow that.");

    // ── Categories ──────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryNotFound = Error.NotFound(
        ProviderErrorCodes.CategoryNotFound,
        "That category does not exist.");

    public static readonly Error CategoryInactive = Error.Validation(
        ProviderErrorCodes.CategoryInactive,
        "That category is no longer available. Choose another.");

    public static readonly Error CategorySlugTaken = Error.Conflict(
        ProviderErrorCodes.CategorySlugTaken,
        "A category with that slug already exists.");

    public static readonly Error CategoryCycle = Error.Validation(
        ProviderErrorCodes.CategoryCycle,
        "A category cannot be moved beneath itself.");

    public static readonly Error CategoryDepthExceeded = Error.Validation(
        ProviderErrorCodes.CategoryDepthExceeded,
        "The category tree cannot be nested that deeply.");

    public static readonly Error CategoryHasChildren = Error.Conflict(
        ProviderErrorCodes.CategoryHasChildren,
        "Retire this category's subcategories first.");

    // ── Services, offerings, pricing ────────────────────────────────────────────────────────

    public static readonly Error ServiceNotFound = Error.NotFound(
        ProviderErrorCodes.ServiceNotFound,
        "That service does not exist on this provider.");

    public static readonly Error ServiceNameEmpty = Error.Validation(
        ProviderErrorCodes.ServiceNameEmpty,
        "A service needs a name.");

    public static readonly Error ServiceNameTooLong = Error.Validation(
        ProviderErrorCodes.ServiceNameTooLong,
        "That service name is too long.");

    public static readonly Error ServiceLimitReached = Error.Conflict(
        ProviderErrorCodes.ServiceLimitReached,
        "This provider has reached its maximum number of services.");

    public static readonly Error ServiceDuplicate = Error.Conflict(
        ProviderErrorCodes.ServiceDuplicate,
        "This provider already offers a service with that name.");

    public static readonly Error OfferingNotFound = Error.NotFound(
        ProviderErrorCodes.OfferingNotFound,
        "That offering does not exist.");

    public static readonly Error OfferingWindowInvalid = Error.Validation(
        ProviderErrorCodes.OfferingWindowInvalid,
        "An offering must end after it starts.");

    public static readonly Error OfferingLimitReached = Error.Conflict(
        ProviderErrorCodes.OfferingLimitReached,
        "This service has reached its maximum number of offerings.");

    public static readonly Error PricingRangeInverted = Error.Validation(
        ProviderErrorCodes.PricingRangeInverted,
        "A price range must end at or above where it starts.");

    public static readonly Error PricingNegative = Error.Validation(
        ProviderErrorCodes.PricingNegative,
        "A price cannot be negative.");

    public static readonly Error PricingCurrencyInvalid = Error.Validation(
        ProviderErrorCodes.PricingCurrencyInvalid,
        "A published price needs a three-letter currency code.");

    public static readonly Error PricingModelMismatch = Error.Validation(
        ProviderErrorCodes.PricingModelMismatch,
        "Those amounts do not match the pricing model you chose.");

    // ── Service areas & location ────────────────────────────────────────────────────────────

    public static readonly Error ServiceAreaNotFound = Error.NotFound(
        ProviderErrorCodes.ServiceAreaNotFound,
        "That service area does not exist on this provider.");

    public static readonly Error ServiceAreaDuplicate = Error.Conflict(
        ProviderErrorCodes.ServiceAreaDuplicate,
        "This provider already covers that area.");

    public static readonly Error ServiceAreaLimitReached = Error.Conflict(
        ProviderErrorCodes.ServiceAreaLimitReached,
        "This provider has reached its maximum number of service areas.");

    public static readonly Error ServiceAreaRadiusInvalid = Error.Validation(
        ProviderErrorCodes.ServiceAreaRadiusInvalid,
        "A service radius must be greater than zero and within the platform maximum.");

    public static readonly Error ServiceAreaScopeRequired = Error.Validation(
        ProviderErrorCodes.ServiceAreaScopeRequired,
        "That kind of service area needs a country, an area or a centre point.");

    public static readonly Error LocationRequired = Error.Validation(
        ProviderErrorCodes.LocationRequired,
        "A provider that serves people in person needs a location.");

    public static readonly Error LocationUnresolvable = Error.Validation(
        ProviderErrorCodes.LocationUnresolvable,
        "We could not work out where that is.");

    public static readonly Error LocationCoordinatesInvalid = Error.Validation(
        ProviderErrorCodes.LocationCoordinatesInvalid,
        "Those coordinates are not on Earth.");

    // ── Availability ────────────────────────────────────────────────────────────────────────

    public static readonly Error OperatingHoursOverlap = Error.Validation(
        ProviderErrorCodes.OperatingHoursOverlap,
        "Two opening times on the same day overlap.");

    public static readonly Error OperatingHoursWindowInverted = Error.Validation(
        ProviderErrorCodes.OperatingHoursWindowInverted,
        "A closing time must be after its opening time.");

    public static readonly Error OperatingHoursWindowLimit = Error.Validation(
        ProviderErrorCodes.OperatingHoursWindowLimit,
        "That is more opening periods than one day can hold.");

    public static readonly Error TimeZoneInvalid = Error.Validation(
        ProviderErrorCodes.AvailabilityTimeZoneInvalid,
        "That is not a time zone we recognise.");

    public static readonly Error AvailabilityExceptionNotFound = Error.NotFound(
        ProviderErrorCodes.AvailabilityExceptionNotFound,
        "That availability exception does not exist.");

    public static readonly Error AvailabilityExceptionOverlap = Error.Conflict(
        ProviderErrorCodes.AvailabilityExceptionOverlap,
        "Another exception already covers part of those dates.");

    public static readonly Error AvailabilityExceptionRangeInverted = Error.Validation(
        ProviderErrorCodes.AvailabilityExceptionRangeInverted,
        "An exception must end on or after the day it starts.");

    public static readonly Error AvailabilityExceptionLimit = Error.Conflict(
        ProviderErrorCodes.AvailabilityExceptionLimit,
        "This provider has reached its maximum number of availability exceptions.");

    // ── Languages, contact methods, gallery, certifications ─────────────────────────────────

    public static readonly Error LanguageNotFound = Error.NotFound(
        ProviderErrorCodes.LanguageNotFound,
        "That language is not listed on this provider.");

    public static readonly Error LanguageDuplicate = Error.Conflict(
        ProviderErrorCodes.LanguageDuplicate,
        "That language is already listed.");

    public static readonly Error LanguageCodeInvalid = Error.Validation(
        ProviderErrorCodes.LanguageCodeInvalid,
        "That is not a language code we recognise.");

    public static readonly Error LanguageLimitReached = Error.Conflict(
        ProviderErrorCodes.LanguageLimitReached,
        "This provider has reached its maximum number of languages.");

    public static readonly Error ContactMethodNotFound = Error.NotFound(
        ProviderErrorCodes.ContactMethodNotFound,
        "That contact method does not exist on this provider.");

    public static readonly Error ContactMethodDuplicate = Error.Conflict(
        ProviderErrorCodes.ContactMethodDuplicate,
        "That contact method is already listed.");

    public static readonly Error ContactMethodValueInvalid = Error.Validation(
        ProviderErrorCodes.ContactMethodValueInvalid,
        "That does not look like a valid value for that kind of contact.");

    public static readonly Error ContactMethodLimitReached = Error.Conflict(
        ProviderErrorCodes.ContactMethodLimitReached,
        "This provider has reached its maximum number of contact methods.");

    public static readonly Error GalleryItemNotFound = Error.NotFound(
        ProviderErrorCodes.GalleryItemNotFound,
        "That gallery item does not exist on this provider.");

    public static readonly Error GalleryLimitReached = Error.Conflict(
        ProviderErrorCodes.GalleryLimitReached,
        "This provider's gallery is full.");

    public static readonly Error GalleryDuplicate = Error.Conflict(
        ProviderErrorCodes.GalleryDuplicate,
        "That item is already in the gallery.");

    public static readonly Error CertificationNotFound = Error.NotFound(
        ProviderErrorCodes.CertificationNotFound,
        "That certification does not exist on this provider.");

    public static readonly Error CertificationLimitReached = Error.Conflict(
        ProviderErrorCodes.CertificationLimitReached,
        "This provider has reached its maximum number of certifications.");

    public static readonly Error CertificationExpired = Error.Validation(
        ProviderErrorCodes.CertificationExpired,
        "That certification's expiry date has already passed.");

    public static readonly Error CertificationIssuerRequired = Error.Validation(
        ProviderErrorCodes.CertificationIssuerRequired,
        "A certification needs to say who issued it.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationNotFound = Error.NotFound(
        ProviderErrorCodes.VerificationNotFound,
        "That verification claim does not exist.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        ProviderErrorCodes.VerificationAlreadyPending,
        "That verification is already being reviewed.");

    public static readonly Error VerificationAlreadyGranted = Error.Conflict(
        ProviderErrorCodes.VerificationAlreadyGranted,
        "That verification has already been granted.");

    public static readonly Error VerificationKindUnsupported = Error.Validation(
        ProviderErrorCodes.VerificationKindUnsupported,
        "That kind of verification is not available for this provider type.");

    public static readonly Error VerificationProviderUnavailable = Error.ServiceUnavailable(
        ProviderErrorCodes.VerificationProviderUnavailable,
        "Verification is temporarily unavailable. Try again shortly.");

    // ── Reviews & recommendations ───────────────────────────────────────────────────────────

    public static readonly Error ReviewNotFound = Error.NotFound(
        ProviderErrorCodes.ReviewNotFound,
        "That review does not exist.");

    public static readonly Error ReviewDuplicate = Error.Conflict(
        ProviderErrorCodes.ReviewDuplicate,
        "You have already reviewed this provider. Edit your review instead.");

    public static readonly Error ReviewSelf = Error.Forbidden(
        ProviderErrorCodes.ReviewSelf,
        "You cannot review a provider you are attached to.");

    public static readonly Error ReviewRatingOutOfRange = Error.Validation(
        ProviderErrorCodes.ReviewRatingOutOfRange,
        "A rating must be between one and five stars.");

    public static readonly Error ReviewBodyTooLong = Error.Validation(
        ProviderErrorCodes.ReviewBodyTooLong,
        "That review is too long.");

    public static readonly Error ReviewNotOwned = Error.Forbidden(
        ProviderErrorCodes.ReviewNotOwned,
        "That is not your review.");

    public static readonly Error ReviewRemoved = Error.Conflict(
        ProviderErrorCodes.ReviewRemoved,
        "That review has been removed.");

    public static readonly Error ReviewEditWindowClosed = Error.Conflict(
        ProviderErrorCodes.ReviewEditWindowClosed,
        "Reviews can no longer be edited after this long. Withdraw it and write a new one.");

    public static readonly Error RecommendationNotFound = Error.NotFound(
        ProviderErrorCodes.RecommendationNotFound,
        "That recommendation does not exist.");

    public static readonly Error RecommendationDuplicate = Error.Conflict(
        ProviderErrorCodes.RecommendationDuplicate,
        "You have already recommended this provider.");

    public static readonly Error RecommendationSelf = Error.Forbidden(
        ProviderErrorCodes.RecommendationSelf,
        "You cannot recommend a provider you are attached to.");

    public static readonly Error RecommendationNoteTooLong = Error.Validation(
        ProviderErrorCodes.RecommendationNoteTooLong,
        "That note is too long.");

    // ── Miscellaneous ───────────────────────────────────────────────────────────────────────

    public static readonly Error BookmarkFailed = Error.Unexpected(
        ProviderErrorCodes.BookmarkFailed,
        "We could not save that just now.");

    public static readonly Error VisibilityDenied = Error.Forbidden(
        ProviderErrorCodes.VisibilityDenied,
        "This provider is not visible to you.");

    public static readonly Error RankingUnknown = Error.Validation(
        ProviderErrorCodes.RankingUnknown,
        "That ranking is not one this deployment offers.");

    public static readonly Error SortUnknown = Error.Validation(
        ProviderErrorCodes.SortUnknown,
        "That sort order is not one this endpoint accepts.");
}
