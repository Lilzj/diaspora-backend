namespace Diaspora.Modules.Providers;

/// <summary>
/// The stable, machine-readable error codes the module emits.
/// <para>
/// A client branches on these. <c>providers.review.duplicate</c> becomes "you have already
/// reviewed this provider — edit your review?" rather than a generic failure toast, and
/// <c>providers.member.last-owner</c> becomes an explanation instead of an unexplained 409.
/// That is why they are constants in one file with a frozen prefix rather than strings at throw
/// sites: a renamed code silently turns a handled case into a generic failure on every client
/// that shipped before the rename.
/// </para>
/// <para>
/// Namespaced by capability — <c>providers.provider.*</c>, <c>providers.service.*</c> — so a
/// client can prefix-match a whole family when it does not care which member of it fired.
/// </para>
/// </summary>
public static class ProviderErrorCodes
{
    public const string NotAuthenticated = "providers.not-authenticated";
    public const string Forbidden = "providers.forbidden";
    public const string EnumUnknown = "providers.enum.unknown";

    public const string ProviderNotFound = "providers.provider.not-found";
    public const string ProviderNameEmpty = "providers.provider.name-empty";
    public const string ProviderNameTooLong = "providers.provider.name-too-long";
    public const string ProviderDescriptionTooLong = "providers.provider.description-too-long";
    public const string ProviderSlugTaken = "providers.provider.slug-taken";
    public const string ProviderSlugInvalid = "providers.provider.slug-invalid";
    public const string ProviderNotOwned = "providers.provider.not-owned";
    public const string ProviderNotEditable = "providers.provider.not-editable";
    public const string ProviderAlreadyPublished = "providers.provider.already-published";
    public const string ProviderNotPublishable = "providers.provider.not-publishable";
    public const string ProviderSuspended = "providers.provider.suspended";
    public const string ProviderClosed = "providers.provider.closed";
    public const string ProviderLimitReached = "providers.provider.limit-reached";
    public const string ProviderTypeUnsupported = "providers.provider.type-unsupported";
    public const string ProviderRemoved = "providers.provider.removed";

    public const string ProfileTaglineTooLong = "providers.profile.tagline-too-long";
    public const string ProfileMissionTooLong = "providers.profile.mission-too-long";
    public const string ProfileFoundedInFuture = "providers.profile.founded-in-future";

    public const string OrganizationRegistrationRequired = "providers.organization.registration-required";
    public const string OrganizationNumberTooLong = "providers.organization.number-too-long";

    public const string MemberNotFound = "providers.member.not-found";
    public const string MemberDuplicate = "providers.member.duplicate";
    public const string MemberLastOwner = "providers.member.last-owner";
    public const string MemberSelfDemotion = "providers.member.self-demotion";
    public const string MemberLimitReached = "providers.member.limit-reached";
    public const string MemberRoleInsufficient = "providers.member.role-insufficient";

    public const string CategoryNotFound = "providers.category.not-found";
    public const string CategoryInactive = "providers.category.inactive";
    public const string CategorySlugTaken = "providers.category.slug-taken";
    public const string CategoryCycle = "providers.category.cycle";
    public const string CategoryDepthExceeded = "providers.category.depth-exceeded";
    public const string CategoryHasChildren = "providers.category.has-children";

    public const string ServiceNotFound = "providers.service.not-found";
    public const string ServiceNameEmpty = "providers.service.name-empty";
    public const string ServiceNameTooLong = "providers.service.name-too-long";
    public const string ServiceLimitReached = "providers.service.limit-reached";
    public const string ServiceDuplicate = "providers.service.duplicate";

    public const string OfferingNotFound = "providers.offering.not-found";
    public const string OfferingWindowInvalid = "providers.offering.window-invalid";
    public const string OfferingLimitReached = "providers.offering.limit-reached";

    public const string PricingRangeInverted = "providers.pricing.range-inverted";
    public const string PricingNegative = "providers.pricing.negative";
    public const string PricingCurrencyInvalid = "providers.pricing.currency-invalid";
    public const string PricingModelMismatch = "providers.pricing.model-mismatch";

    public const string ServiceAreaNotFound = "providers.service-area.not-found";
    public const string ServiceAreaDuplicate = "providers.service-area.duplicate";
    public const string ServiceAreaLimitReached = "providers.service-area.limit-reached";
    public const string ServiceAreaRadiusInvalid = "providers.service-area.radius-invalid";
    public const string ServiceAreaScopeRequired = "providers.service-area.scope-required";

    public const string LocationRequired = "providers.location.required";
    public const string LocationUnresolvable = "providers.location.unresolvable";
    public const string LocationCoordinatesInvalid = "providers.location.coordinates-invalid";

    public const string OperatingHoursOverlap = "providers.operating-hours.overlap";
    public const string OperatingHoursWindowInverted = "providers.operating-hours.window-inverted";
    public const string OperatingHoursWindowLimit = "providers.operating-hours.window-limit";
    public const string AvailabilityTimeZoneInvalid = "providers.availability.timezone-invalid";
    public const string AvailabilityExceptionNotFound = "providers.availability.exception-not-found";
    public const string AvailabilityExceptionOverlap = "providers.availability.exception-overlap";
    public const string AvailabilityExceptionRangeInverted = "providers.availability.exception-range-inverted";
    public const string AvailabilityExceptionLimit = "providers.availability.exception-limit";

    public const string LanguageNotFound = "providers.language.not-found";
    public const string LanguageDuplicate = "providers.language.duplicate";
    public const string LanguageCodeInvalid = "providers.language.code-invalid";
    public const string LanguageLimitReached = "providers.language.limit-reached";

    public const string ContactMethodNotFound = "providers.contact-method.not-found";
    public const string ContactMethodDuplicate = "providers.contact-method.duplicate";
    public const string ContactMethodValueInvalid = "providers.contact-method.value-invalid";
    public const string ContactMethodLimitReached = "providers.contact-method.limit-reached";

    public const string GalleryItemNotFound = "providers.gallery.not-found";
    public const string GalleryLimitReached = "providers.gallery.limit-reached";
    public const string GalleryDuplicate = "providers.gallery.duplicate";

    public const string CertificationNotFound = "providers.certification.not-found";
    public const string CertificationLimitReached = "providers.certification.limit-reached";
    public const string CertificationExpired = "providers.certification.expired";
    public const string CertificationIssuerRequired = "providers.certification.issuer-required";

    public const string VerificationNotFound = "providers.verification.not-found";
    public const string VerificationAlreadyPending = "providers.verification.already-pending";
    public const string VerificationAlreadyGranted = "providers.verification.already-granted";
    public const string VerificationKindUnsupported = "providers.verification.kind-unsupported";
    public const string VerificationProviderUnavailable = "providers.verification.provider-unavailable";

    public const string ReviewNotFound = "providers.review.not-found";
    public const string ReviewDuplicate = "providers.review.duplicate";
    public const string ReviewSelf = "providers.review.self";
    public const string ReviewRatingOutOfRange = "providers.review.rating-out-of-range";
    public const string ReviewBodyTooLong = "providers.review.body-too-long";
    public const string ReviewNotOwned = "providers.review.not-owned";
    public const string ReviewRemoved = "providers.review.removed";
    public const string ReviewEditWindowClosed = "providers.review.edit-window-closed";

    public const string RecommendationNotFound = "providers.recommendation.not-found";
    public const string RecommendationDuplicate = "providers.recommendation.duplicate";
    public const string RecommendationSelf = "providers.recommendation.self";
    public const string RecommendationNoteTooLong = "providers.recommendation.note-too-long";

    public const string BookmarkFailed = "providers.bookmark.failed";

    public const string VisibilityDenied = "providers.visibility.denied";
    public const string RankingUnknown = "providers.ranking.unknown";
    public const string SortUnknown = "providers.sort.unknown";
}
