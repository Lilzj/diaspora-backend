using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns
/// <c>MarketplaceErrors.ListingNotFound</c> rather than assembling a code and a message at the
/// failure site. That keeps the message consistent between the places the same failure arises,
/// and makes the set enumerable — a test asserts every constant here has a matching entry in
/// <see cref="MarketplaceErrorCodes"/> and vice versa.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. No place in the module names a number. Two choices are worth stating:
/// <see cref="RevisionConflict"/> is a conflict so an editor can offer a merge rather than an
/// error page, and everything a caller must not be able to probe for —
/// <see cref="ListingHidden"/>, <see cref="ListingDeleted"/>, <see cref="DraftNotVisible"/> — is
/// a not-found rather than a forbidden, because a 403 on a hidden listing confirms it exists.
/// </para>
/// <para>
/// The messages address the reader in the second person and say what to do next where there is
/// something to do. They are user-facing strings, not log lines.
/// </para>
/// </summary>
public static class MarketplaceErrors
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public static readonly Error ListingNotFound = Error.NotFound(
        MarketplaceErrorCodes.ListingNotFound,
        "That listing does not exist, or you cannot see it.");

    public static readonly Error SellerNotFound = Error.NotFound(
        MarketplaceErrorCodes.SellerNotFound,
        "That seller does not exist.");

    public static readonly Error OfferNotFound = Error.NotFound(
        MarketplaceErrorCodes.OfferNotFound,
        "That offer does not exist.");

    public static readonly Error ReservationNotFound = Error.NotFound(
        MarketplaceErrorCodes.ReservationNotFound,
        "That reservation does not exist.");

    public static readonly Error CategoryNotFound = Error.NotFound(
        MarketplaceErrorCodes.CategoryNotFound,
        "That category does not exist.");

    public static readonly Error AttributeNotFound = Error.NotFound(
        MarketplaceErrorCodes.AttributeNotFound,
        "That attribute is not in the catalogue.");

    public static readonly Error SavedListingNotFound = Error.NotFound(
        MarketplaceErrorCodes.SavedListingNotFound,
        "That listing is not in your saved list.");

    public static readonly Error ReportNotFound = Error.NotFound(
        MarketplaceErrorCodes.ReportNotFound,
        "That report does not exist.");

    public static readonly Error PromotionNotFound = Error.NotFound(
        MarketplaceErrorCodes.PromotionNotFound,
        "That promotion does not exist.");

    public static readonly Error ConversationNotFound = Error.NotFound(
        MarketplaceErrorCodes.ConversationNotFound,
        "That conversation does not exist.");

    // Not-found rather than forbidden: a 403 on a hidden listing confirms it exists.
    public static readonly Error ListingHidden = Error.NotFound(
        MarketplaceErrorCodes.ListingHidden,
        "That listing does not exist, or you cannot see it.");

    public static readonly Error ListingDeleted = Error.NotFound(
        MarketplaceErrorCodes.ListingDeleted,
        "That listing has been deleted.");

    public static readonly Error DraftNotVisible = Error.NotFound(
        MarketplaceErrorCodes.DraftNotVisible,
        "That listing does not exist, or you cannot see it.");

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        MarketplaceErrorCodes.NotAuthenticated,
        "Sign in to continue.");

    public static readonly Error NotSeller = Error.Forbidden(
        MarketplaceErrorCodes.NotSeller,
        "Set up a seller profile before doing that.");

    public static readonly Error NotListingOwner = Error.Forbidden(
        MarketplaceErrorCodes.NotListingOwner,
        "This listing belongs to another seller.");

    public static readonly Error NotBuyer = Error.Forbidden(
        MarketplaceErrorCodes.NotBuyer,
        "This is not your purchase.");

    public static readonly Error NotOfferParty = Error.Forbidden(
        MarketplaceErrorCodes.NotOfferParty,
        "You are not part of this negotiation.");

    public static readonly Error NotReservationParty = Error.Forbidden(
        MarketplaceErrorCodes.NotReservationParty,
        "You are not part of this reservation.");

    public static readonly Error NotModerator = Error.Forbidden(
        MarketplaceErrorCodes.NotModerator,
        "Only moderators can do that.");

    public static readonly Error SellerSuspended = Error.Forbidden(
        MarketplaceErrorCodes.SellerSuspended,
        "This seller account is suspended.");

    public static readonly Error SellerNotVerified = Error.Forbidden(
        MarketplaceErrorCodes.SellerNotVerified,
        "Verify your seller profile before doing that.");

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        MarketplaceErrorCodes.RevisionConflict,
        "Someone else changed this listing while you were editing. Reload and try again.");

    // ── Listing lifecycle ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The publication bar, stated generally. Prefer
    /// <see cref="ListingNotPublishableBecause"/>, which names the prerequisites this listing
    /// actually fails.
    /// <para>
    /// The wording used to name five of the seven conditions and omit fulfilment and the
    /// photograph — the two a seller is most likely to be missing, because the other five are
    /// supplied when the listing is created. A seller who added exactly what it asked for was
    /// refused again by the same sentence.
    /// </para>
    /// </summary>
    public static readonly Error ListingNotPublishable = Error.Validation(
        MarketplaceErrorCodes.ListingNotPublishable,
        "This listing does not yet meet the bar for publication.");

    /// <summary>
    /// The publication refusal, naming the prerequisites that are unmet.
    /// <para>
    /// The description is built from the aggregate's own evaluation rather than written out
    /// here, so the rule and its explanation cannot drift apart. The machine-readable tokens
    /// are appended in brackets so a client can render a checklist without parsing prose;
    /// <c>Error</c> carries no metadata slot, and adding one would change a primitive every
    /// module depends on for a single message.
    /// </para>
    /// </summary>
    public static Error ListingNotPublishableBecause(
        IReadOnlyList<ListingPublishRequirement> unmet)
    {
        ArgumentNullException.ThrowIfNull(unmet);

        if (unmet.Count == 0) return ListingNotPublishable;

        var described = unmet.Select(ListingPublishRequirementText.Describe).ToArray();
        var tokens = string.Join(",", unmet.Select(ListingPublishRequirementText.Token));

        var list = described.Length == 1
            ? described[0]
            : string.Join(", ", described[..^1]) + " and " + described[^1];

        return Error.Validation(
            MarketplaceErrorCodes.ListingNotPublishable,
            $"Before this listing can be published it still needs {list}. [missing: {tokens}]");
    }

    public static readonly Error ListingAlreadyPublished = Error.Conflict(
        MarketplaceErrorCodes.ListingAlreadyPublished,
        "This listing is already published.");

    public static readonly Error ListingNotPublished = Error.Conflict(
        MarketplaceErrorCodes.ListingNotPublished,
        "This listing has not been published.");

    public static readonly Error ListingNotActive = Error.Conflict(
        MarketplaceErrorCodes.ListingNotActive,
        "This listing is not available right now.");

    public static readonly Error ListingReserved = Error.Conflict(
        MarketplaceErrorCodes.ListingReserved,
        "This listing is reserved for another buyer.");

    public static readonly Error ListingSold = Error.Conflict(
        MarketplaceErrorCodes.ListingSold,
        "This listing has sold.");

    public static readonly Error ListingExpired = Error.Conflict(
        MarketplaceErrorCodes.ListingExpired,
        "This listing has expired.");

    public static readonly Error ListingArchivedImmutable = Error.Conflict(
        MarketplaceErrorCodes.ListingArchivedImmutable,
        "An archived listing cannot be edited. Restore it first.");

    public static readonly Error ListingNotArchived = Error.Conflict(
        MarketplaceErrorCodes.ListingNotArchived,
        "This listing is not archived.");

    public static readonly Error ListingNotDeleted = Error.Conflict(
        MarketplaceErrorCodes.ListingNotDeleted,
        "This listing has not been deleted.");

    public static readonly Error ListingTransitionInvalid = Error.Conflict(
        MarketplaceErrorCodes.ListingTransitionInvalid,
        "This listing cannot move to that state from where it is.");

    public static readonly Error ExpiryInPast = Error.Validation(
        MarketplaceErrorCodes.ExpiryInPast,
        "The expiry date must be in the future.");

    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        MarketplaceErrorCodes.TitleInvalid,
        "The listing title is missing or too long.");

    public static readonly Error SlugInvalid = Error.Validation(
        MarketplaceErrorCodes.SlugInvalid,
        "That link handle is not valid.");

    public static readonly Error SlugTaken = Error.Conflict(
        MarketplaceErrorCodes.SlugTaken,
        "That link handle is already in use.");

    public static readonly Error DescriptionInvalid = Error.Validation(
        MarketplaceErrorCodes.DescriptionInvalid,
        "The listing description is missing or too long.");

    public static readonly Error SummaryTooLong = Error.Validation(
        MarketplaceErrorCodes.SummaryTooLong,
        "The summary is too long.");

    public static readonly Error PriceInvalid = Error.Validation(
        MarketplaceErrorCodes.PriceInvalid,
        "That price is not valid.");

    public static readonly Error PriceRequired = Error.Validation(
        MarketplaceErrorCodes.PriceRequired,
        "This kind of listing needs a price.");

    public static readonly Error CurrencyInvalid = Error.Validation(
        MarketplaceErrorCodes.CurrencyInvalid,
        "That currency code is not valid.");

    public static readonly Error CurrencyMismatch = Error.Validation(
        MarketplaceErrorCodes.CurrencyMismatch,
        "Offers must be made in the currency the listing is priced in.");

    public static readonly Error ConditionInvalid = Error.Validation(
        MarketplaceErrorCodes.ConditionInvalid,
        "That item condition is not recognised.");

    public static readonly Error ListingTypeInvalid = Error.Validation(
        MarketplaceErrorCodes.ListingTypeInvalid,
        "That listing type is not recognised.");

    public static readonly Error AuctionNotSupported = Error.Validation(
        MarketplaceErrorCodes.AuctionNotSupported,
        "Auctions are not open yet.");

    public static readonly Error CategoryKeyInvalid = Error.Validation(
        MarketplaceErrorCodes.CategoryKeyInvalid,
        "That category is missing or not valid.");

    public static readonly Error CategoryInactive = Error.Validation(
        MarketplaceErrorCodes.CategoryInactive,
        "That category is no longer open for new listings.");

    public static readonly Error CategoryKeyTaken = Error.Conflict(
        MarketplaceErrorCodes.CategoryKeyTaken,
        "That category key is already in use.");

    public static readonly Error CategoryHasChildren = Error.Conflict(
        MarketplaceErrorCodes.CategoryHasChildren,
        "Retire the subcategories before retiring this one.");

    public static readonly Error CategoryDepthExceeded = Error.Validation(
        MarketplaceErrorCodes.CategoryDepthExceeded,
        "Categories can only be nested three levels deep.");

    public static readonly Error AttributeInvalid = Error.Validation(
        MarketplaceErrorCodes.AttributeInvalid,
        "An attribute is empty or too long.");

    public static readonly Error AttributeLimit = Error.Validation(
        MarketplaceErrorCodes.AttributeLimit,
        "This listing has too many attributes.");

    public static readonly Error AttributeRequired = Error.Validation(
        MarketplaceErrorCodes.AttributeRequired,
        "This category needs an attribute you have not supplied.");

    public static readonly Error AttributeValueInvalid = Error.Validation(
        MarketplaceErrorCodes.AttributeValueInvalid,
        "That attribute value is not one this category accepts.");

    public static readonly Error TagInvalid = Error.Validation(
        MarketplaceErrorCodes.TagInvalid,
        "A tag is empty or not valid.");

    public static readonly Error TagLimit = Error.Validation(
        MarketplaceErrorCodes.TagLimit,
        "This listing has too many tags.");

    public static readonly Error MediaInvalid = Error.Validation(
        MarketplaceErrorCodes.MediaInvalid,
        "That attachment is not valid.");

    public static readonly Error MediaLimit = Error.Validation(
        MarketplaceErrorCodes.MediaLimit,
        "This listing has as many photos as it can hold.");

    public static readonly Error CoverImageRequired = Error.Validation(
        MarketplaceErrorCodes.CoverImageRequired,
        "Add at least one photo before publishing.");

    public static readonly Error CountryInvalid = Error.Validation(
        MarketplaceErrorCodes.CountryInvalid,
        "That country code is not valid.");

    public static readonly Error LocationInvalid = Error.Validation(
        MarketplaceErrorCodes.LocationInvalid,
        "That location is not valid.");

    public static readonly Error FulfillmentInvalid = Error.Validation(
        MarketplaceErrorCodes.FulfillmentInvalid,
        "Those delivery options are not valid.");

    public static readonly Error FulfillmentRequired = Error.Validation(
        MarketplaceErrorCodes.FulfillmentRequired,
        "Say how the buyer gets the item: pickup, delivery or shipping.");

    public static readonly Error ProductIdentityInvalid = Error.Validation(
        MarketplaceErrorCodes.ProductIdentityInvalid,
        "The brand, model or reference is too long.");

    public static readonly Error ModerationNoteInvalid = Error.Validation(
        MarketplaceErrorCodes.ModerationNoteInvalid,
        "A moderation note is empty or too long.");

    public static readonly Error ProviderReferenceInvalid = Error.Validation(
        MarketplaceErrorCodes.ProviderReferenceInvalid,
        "That shop reference is not valid.");

    public static readonly Error GuideReferenceInvalid = Error.Validation(
        MarketplaceErrorCodes.GuideReferenceInvalid,
        "That guide reference is not valid.");

    // ── Inventory ───────────────────────────────────────────────────────────────────────────

    public static readonly Error QuantityInvalid = Error.Validation(
        MarketplaceErrorCodes.QuantityInvalid,
        "That quantity is not valid.");

    public static readonly Error InsufficientStock = Error.Conflict(
        MarketplaceErrorCodes.InsufficientStock,
        "There is not enough of this item left.");

    public static readonly Error StockBelowCommitted = Error.Conflict(
        MarketplaceErrorCodes.StockBelowCommitted,
        "You cannot set the stock below what is already reserved or sold.");

    public static readonly Error InventoryNotTracked = Error.Conflict(
        MarketplaceErrorCodes.InventoryNotTracked,
        "This listing does not track stock.");

    // ── Seller ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error SellerAlreadyExists = Error.Conflict(
        MarketplaceErrorCodes.SellerAlreadyExists,
        "You already have a seller profile.");

    public static readonly Error SellerNameInvalid = Error.Validation(
        MarketplaceErrorCodes.SellerNameInvalid,
        "The seller name is missing or too long.");

    public static readonly Error SellerSlugTaken = Error.Conflict(
        MarketplaceErrorCodes.SellerSlugTaken,
        "That storefront handle is already in use.");

    public static readonly Error SellerContactInvalid = Error.Validation(
        MarketplaceErrorCodes.SellerContactInvalid,
        "Those contact details are not valid.");

    public static readonly Error SellerKindInvalid = Error.Validation(
        MarketplaceErrorCodes.SellerKindInvalid,
        "That seller type is not recognised.");

    public static readonly Error StorefrontNotOwned = Error.Forbidden(
        MarketplaceErrorCodes.StorefrontNotOwned,
        "That shop is not yours to sell from.");

    public static readonly Error StorefrontProviderUnknown = Error.Validation(
        MarketplaceErrorCodes.StorefrontProviderUnknown,
        "That shop is not in the provider directory.");

    // ── Offers ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error OffersNotAccepted = Error.Conflict(
        MarketplaceErrorCodes.OffersNotAccepted,
        "This seller is not taking offers on this listing.");

    public static readonly Error OfferAmountInvalid = Error.Validation(
        MarketplaceErrorCodes.OfferAmountInvalid,
        "That offer amount is not valid.");

    public static readonly Error OfferBelowMinimum = Error.Validation(
        MarketplaceErrorCodes.OfferBelowMinimum,
        "That offer is below the seller's minimum.");

    public static readonly Error OfferAlreadyOpen = Error.Conflict(
        MarketplaceErrorCodes.OfferAlreadyOpen,
        "You already have an open offer on this listing.");

    public static readonly Error OfferAlreadyDecided = Error.Conflict(
        MarketplaceErrorCodes.OfferAlreadyDecided,
        "This offer has already been answered.");

    public static readonly Error OfferExpired = Error.Conflict(
        MarketplaceErrorCodes.OfferExpired,
        "This offer has expired.");

    public static readonly Error OfferExpiryInPast = Error.Validation(
        MarketplaceErrorCodes.OfferExpiryInPast,
        "The offer expiry must be in the future.");

    public static readonly Error OfferNotYours = Error.Forbidden(
        MarketplaceErrorCodes.OfferNotYours,
        "It is not your turn to answer this offer.");

    public static readonly Error OfferSelfBid = Error.Validation(
        MarketplaceErrorCodes.OfferSelfBid,
        "You cannot make an offer on your own listing.");

    public static readonly Error OfferRoundLimit = Error.Conflict(
        MarketplaceErrorCodes.OfferRoundLimit,
        "This negotiation has gone back and forth as far as it can. Accept, decline, or start "
        + "again.");

    public static readonly Error OfferQuantityInvalid = Error.Validation(
        MarketplaceErrorCodes.OfferQuantityInvalid,
        "That quantity is more than the seller has.");

    public static readonly Error TooManyOffers = Error.RateLimited(
        MarketplaceErrorCodes.TooManyOffers,
        "You have made a lot of offers today. Try again tomorrow.");

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    public static readonly Error ReservationsNotAccepted = Error.Conflict(
        MarketplaceErrorCodes.ReservationsNotAccepted,
        "This seller is not taking reservations on this listing.");

    public static readonly Error ReservationAlreadyOpen = Error.Conflict(
        MarketplaceErrorCodes.ReservationAlreadyOpen,
        "You already have this item reserved.");

    public static readonly Error ReservationAlreadyClosed = Error.Conflict(
        MarketplaceErrorCodes.ReservationAlreadyClosed,
        "This reservation is already finished.");

    public static readonly Error ReservationExpired = Error.Conflict(
        MarketplaceErrorCodes.ReservationExpired,
        "This reservation has expired.");

    public static readonly Error ReservationTransitionInvalid = Error.Conflict(
        MarketplaceErrorCodes.ReservationTransitionInvalid,
        "This reservation cannot move to that state from where it is.");

    public static readonly Error ReservationSelf = Error.Validation(
        MarketplaceErrorCodes.ReservationSelf,
        "You cannot reserve your own listing.");

    public static readonly Error ReservationWindowInvalid = Error.Validation(
        MarketplaceErrorCodes.ReservationWindowInvalid,
        "That collection window is not valid.");

    public static readonly Error TooManyReservations = Error.RateLimited(
        MarketplaceErrorCodes.TooManyReservations,
        "You have reserved a lot of items. Complete or cancel some before reserving more.");

    // ── Saved listings, reports & promotions ────────────────────────────────────────────────

    public static readonly Error SavedListingExists = Error.Conflict(
        MarketplaceErrorCodes.SavedListingExists,
        "That listing is already saved.");

    public static readonly Error SavedListingLimit = Error.Validation(
        MarketplaceErrorCodes.SavedListingLimit,
        "Your saved list is full.");

    public static readonly Error ReportReasonInvalid = Error.Validation(
        MarketplaceErrorCodes.ReportReasonInvalid,
        "Say why you are reporting this listing.");

    public static readonly Error ReportAlreadyFiled = Error.Conflict(
        MarketplaceErrorCodes.ReportAlreadyFiled,
        "You have already reported this listing. We are looking at it.");

    public static readonly Error ReportAlreadyResolved = Error.Conflict(
        MarketplaceErrorCodes.ReportAlreadyResolved,
        "That report has already been resolved.");

    public static readonly Error PromotionWindowInvalid = Error.Validation(
        MarketplaceErrorCodes.PromotionWindowInvalid,
        "That promotion window is not valid.");

    public static readonly Error PromotionAlreadyActive = Error.Conflict(
        MarketplaceErrorCodes.PromotionAlreadyActive,
        "This listing already has that promotion running.");

    public static readonly Error PromotionEnded = Error.Conflict(
        MarketplaceErrorCodes.PromotionEnded,
        "That promotion has already ended.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        MarketplaceErrorCodes.VerificationUnavailable,
        "Verification is unavailable right now. Try again shortly.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        MarketplaceErrorCodes.VerificationAlreadyPending,
        "A verification is already being reviewed.");

    // ── Messaging ───────────────────────────────────────────────────────────────────────────

    public static readonly Error ConversationUnavailable = Error.ServiceUnavailable(
        MarketplaceErrorCodes.ConversationUnavailable,
        "Messaging is not available on this deployment yet.");

    public static readonly Error ConversationSelf = Error.Validation(
        MarketplaceErrorCodes.ConversationSelf,
        "You cannot start a conversation with yourself.");

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public static readonly Error TooManyListings = Error.RateLimited(
        MarketplaceErrorCodes.TooManyListings,
        "You have as many listings open as we allow. Close or sell some before adding more.");

    public static readonly Error SearchQueryInvalid = Error.Validation(
        MarketplaceErrorCodes.SearchQueryInvalid,
        "That search is not valid.");

    public static readonly Error SuspectedFraud = Error.Forbidden(
        MarketplaceErrorCodes.SuspectedFraud,
        "This listing has been held for review.");

    public static readonly Error DuplicateListing = Error.Conflict(
        MarketplaceErrorCodes.DuplicateListing,
        "You already have a listing that looks exactly like this one.");
}
