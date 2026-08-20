namespace Diaspora.Modules.Marketplace;

/// <summary>
/// The stable wire-level error codes the Marketplace module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them, and
/// translations key off them. Change the description on an <c>Error</c> freely; never change a
/// code. New failures get new codes.
/// </para>
/// </summary>
public static class MarketplaceErrorCodes
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public const string ListingNotFound = "marketplace.listing.not-found";
    public const string SellerNotFound = "marketplace.seller.not-found";
    public const string OfferNotFound = "marketplace.offer.not-found";
    public const string ReservationNotFound = "marketplace.reservation.not-found";
    public const string CategoryNotFound = "marketplace.category.not-found";
    public const string AttributeNotFound = "marketplace.attribute.not-found";
    public const string SavedListingNotFound = "marketplace.saved.not-found";
    public const string ReportNotFound = "marketplace.report.not-found";
    public const string PromotionNotFound = "marketplace.promotion.not-found";
    public const string ConversationNotFound = "marketplace.conversation.not-found";
    public const string ListingHidden = "marketplace.listing.hidden";
    public const string ListingDeleted = "marketplace.listing.deleted";
    public const string DraftNotVisible = "marketplace.draft.not-visible";

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public const string NotAuthenticated = "marketplace.not-authenticated";
    public const string NotSeller = "marketplace.not-seller";
    public const string NotListingOwner = "marketplace.listing.not-owned";
    public const string NotBuyer = "marketplace.not-buyer";
    public const string NotOfferParty = "marketplace.offer.not-party";
    public const string NotReservationParty = "marketplace.reservation.not-party";
    public const string NotModerator = "marketplace.not-moderator";
    public const string SellerSuspended = "marketplace.seller.suspended";
    public const string SellerNotVerified = "marketplace.seller.not-verified";

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public const string RevisionConflict = "marketplace.revision-conflict";

    // ── Listing lifecycle ───────────────────────────────────────────────────────────────────

    public const string ListingNotPublishable = "marketplace.listing.not-publishable";
    public const string ListingAlreadyPublished = "marketplace.listing.already-published";
    public const string ListingNotPublished = "marketplace.listing.not-published";
    public const string ListingNotActive = "marketplace.listing.not-active";
    public const string ListingReserved = "marketplace.listing.reserved";
    public const string ListingSold = "marketplace.listing.sold";
    public const string ListingExpired = "marketplace.listing.expired";
    public const string ListingArchivedImmutable = "marketplace.listing.archived-immutable";
    public const string ListingNotArchived = "marketplace.listing.not-archived";
    public const string ListingNotDeleted = "marketplace.listing.not-deleted";
    public const string ListingTransitionInvalid = "marketplace.listing.transition-invalid";
    public const string ExpiryInPast = "marketplace.listing.expiry.in-past";

    // ── Listing content ─────────────────────────────────────────────────────────────────────

    public const string TitleInvalid = "marketplace.title.invalid";
    public const string SlugInvalid = "marketplace.slug.invalid";
    public const string SlugTaken = "marketplace.slug.taken";
    public const string DescriptionInvalid = "marketplace.description.invalid";
    public const string SummaryTooLong = "marketplace.summary.too-long";
    public const string PriceInvalid = "marketplace.price.invalid";
    public const string PriceRequired = "marketplace.price.required";
    public const string CurrencyInvalid = "marketplace.currency.invalid";
    public const string CurrencyMismatch = "marketplace.currency.mismatch";
    public const string ConditionInvalid = "marketplace.condition.invalid";
    public const string ListingTypeInvalid = "marketplace.listing-type.invalid";
    public const string AuctionNotSupported = "marketplace.listing-type.auction-unsupported";
    public const string CategoryKeyInvalid = "marketplace.category.key-invalid";
    public const string CategoryInactive = "marketplace.category.inactive";
    public const string CategoryKeyTaken = "marketplace.category.key-taken";
    public const string CategoryHasChildren = "marketplace.category.has-children";
    public const string CategoryDepthExceeded = "marketplace.category.depth-exceeded";
    public const string AttributeInvalid = "marketplace.attribute.invalid";
    public const string AttributeLimit = "marketplace.attributes.limit";
    public const string AttributeRequired = "marketplace.attribute.required";
    public const string AttributeValueInvalid = "marketplace.attribute.value-invalid";
    public const string TagInvalid = "marketplace.tag.invalid";
    public const string TagLimit = "marketplace.tags.limit";
    public const string MediaInvalid = "marketplace.media.invalid";
    public const string MediaLimit = "marketplace.media.limit";
    public const string CoverImageRequired = "marketplace.media.cover-required";
    public const string CountryInvalid = "marketplace.country.invalid";
    public const string LocationInvalid = "marketplace.location.invalid";
    public const string FulfillmentInvalid = "marketplace.fulfillment.invalid";
    public const string FulfillmentRequired = "marketplace.fulfillment.required";
    public const string ProductIdentityInvalid = "marketplace.product-identity.invalid";
    public const string ModerationNoteInvalid = "marketplace.moderation-note.invalid";
    public const string ProviderReferenceInvalid = "marketplace.provider-reference.invalid";
    public const string GuideReferenceInvalid = "marketplace.guide-reference.invalid";

    // ── Inventory ───────────────────────────────────────────────────────────────────────────

    public const string QuantityInvalid = "marketplace.quantity.invalid";
    public const string InsufficientStock = "marketplace.stock.insufficient";
    public const string StockBelowCommitted = "marketplace.stock.below-committed";
    public const string InventoryNotTracked = "marketplace.inventory.not-tracked";

    // ── Seller ──────────────────────────────────────────────────────────────────────────────

    public const string SellerAlreadyExists = "marketplace.seller.already-exists";
    public const string SellerNameInvalid = "marketplace.seller.name.invalid";
    public const string SellerSlugTaken = "marketplace.seller.slug.taken";
    public const string SellerContactInvalid = "marketplace.seller.contact.invalid";
    public const string SellerKindInvalid = "marketplace.seller.kind.invalid";
    public const string StorefrontNotOwned = "marketplace.storefront.not-owned";
    public const string StorefrontProviderUnknown = "marketplace.storefront.provider-unknown";

    // ── Offers ──────────────────────────────────────────────────────────────────────────────

    public const string OffersNotAccepted = "marketplace.offer.not-accepted-here";
    public const string OfferAmountInvalid = "marketplace.offer.amount.invalid";
    public const string OfferBelowMinimum = "marketplace.offer.below-minimum";
    public const string OfferAlreadyOpen = "marketplace.offer.already-open";
    public const string OfferAlreadyDecided = "marketplace.offer.already-decided";
    public const string OfferExpired = "marketplace.offer.expired";
    public const string OfferExpiryInPast = "marketplace.offer.expiry.in-past";
    public const string OfferNotYours = "marketplace.offer.not-yours";
    public const string OfferSelfBid = "marketplace.offer.self";
    public const string OfferRoundLimit = "marketplace.offer.rounds.limit";
    public const string OfferQuantityInvalid = "marketplace.offer.quantity.invalid";
    public const string TooManyOffers = "marketplace.offers.too-many";

    // ── Reservations ────────────────────────────────────────────────────────────────────────

    public const string ReservationsNotAccepted = "marketplace.reservation.not-accepted-here";
    public const string ReservationAlreadyOpen = "marketplace.reservation.already-open";
    public const string ReservationAlreadyClosed = "marketplace.reservation.already-closed";
    public const string ReservationExpired = "marketplace.reservation.expired";
    public const string ReservationTransitionInvalid = "marketplace.reservation.transition-invalid";
    public const string ReservationSelf = "marketplace.reservation.self";
    public const string ReservationWindowInvalid = "marketplace.reservation.window.invalid";
    public const string TooManyReservations = "marketplace.reservations.too-many";

    // ── Saved listings, reports & promotions ────────────────────────────────────────────────

    public const string SavedListingExists = "marketplace.saved.exists";
    public const string SavedListingLimit = "marketplace.saved.limit";
    public const string ReportReasonInvalid = "marketplace.report.reason.invalid";
    public const string ReportAlreadyFiled = "marketplace.report.already-filed";
    public const string ReportAlreadyResolved = "marketplace.report.already-resolved";
    public const string PromotionWindowInvalid = "marketplace.promotion.window.invalid";
    public const string PromotionAlreadyActive = "marketplace.promotion.already-active";
    public const string PromotionEnded = "marketplace.promotion.ended";

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const string VerificationUnavailable = "marketplace.verification.unavailable";
    public const string VerificationAlreadyPending = "marketplace.verification.already-pending";

    // ── Messaging ───────────────────────────────────────────────────────────────────────────

    public const string ConversationUnavailable = "marketplace.conversation.unavailable";
    public const string ConversationSelf = "marketplace.conversation.self";

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public const string TooManyListings = "marketplace.listings.too-many";
    public const string SearchQueryInvalid = "marketplace.search.query-invalid";
    public const string SuspectedFraud = "marketplace.fraud.suspected";
    public const string DuplicateListing = "marketplace.listing.duplicate";
}
