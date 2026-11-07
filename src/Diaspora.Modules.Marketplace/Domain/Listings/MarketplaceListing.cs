using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// Something a member is offering to the community. The aggregate the whole module is arranged
/// around.
/// <para>
/// <b>The listing is owned by a seller profile, not by a person.</b> That single decision is what
/// makes this a commerce platform rather than a classifieds board: a shop's listings survive the
/// member who typed them, the badge a buyer is relying on belongs to the trading entity that will
/// actually take their money, and a business can be suspended without suspending the person.
/// <see cref="CreatedByUserId"/> records who wrote it, and grants nothing.
/// </para>
/// <para>
/// <b>The identity is <c>MarketplaceListingId</c> from the kernel's identifier bag, claimed rather
/// than redeclared.</b> The kernel reserved that name for this aggregate before this module
/// existed, exactly as Media reserved <c>MediaOwnerType.MarketplaceListing</c> in B5 and Community
/// reserved <c>CommunityContentType.Marketplace</c> in B6. Declaring a second type with the same
/// name would be the collision B9 records paying for; declaring one with a different name would
/// leave the reserved one dangling forever.
/// </para>
/// <para>
/// <b>What it owns:</b> everything whose lifetime is exactly the listing's and whose consistency
/// has to hold in one transaction — its prose, its price, its stock, its attributes, tags, media,
/// provider and guide references, and its moderation trail.
/// <b>What it deliberately does not own:</b> offers, reservations, saved rows, reports,
/// promotions, conversation references and analytics are separate aggregates keyed by
/// <c>MarketplaceListingId</c>, because a listing with four thousand offers on it must never be
/// loaded to accept the four-thousand-and-first. Comments and reactions are not here at all —
/// they belong to Community, reached through the content-source seam.
/// </para>
/// <para>
/// <b>Stock is the invariant this aggregate exists to protect.</b> Holding, releasing and settling
/// units all go through <see cref="ListingInventory"/> and all of them refuse rather than clamp,
/// so the only way to oversell is to bypass the aggregate. The lifecycle follows the stock:
/// running out of available units moves an Active listing to Reserved, a release brings it back,
/// and the last unit sold moves it to Sold.
/// </para>
/// <para>
/// <b>Concurrency is a revision number, not a timestamp.</b> Every content mutation bumps
/// <see cref="Revision"/>; an editor sends back the revision it loaded and
/// <see cref="EnsureRevision"/> rejects a stale save with a conflict the client can merge from.
/// Counter mutations, stock movements driven by other aggregates, and Trust outcomes deliberately
/// do not bump it — a buyer reserving an item is not an edit, and must never make a seller's
/// in-flight save fail with a conflict they cannot explain.
/// </para>
/// <para>
/// <b>Verification is a projection.</b> Nothing here can grant a trust mark.
/// <see cref="SubmitForVerification"/> records that a claim went to Trust;
/// <see cref="ApplyVerificationDecision"/> is reachable only from a handler for a Trust
/// integration event, and is guarded on the decision timestamp because delivery is at-least-once
/// and unordered.
/// </para>
/// </summary>
public sealed class MarketplaceListing
    : AggregateRoot<MarketplaceListingId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<ListingAttribute> _attributes = [];
    private readonly List<ListingTag> _tags = [];
    private readonly List<ListingMediaReference> _mediaReferences = [];
    private readonly List<ListingProviderReference> _providerReferences = [];
    private readonly List<ListingGuideReference> _guideReferences = [];
    private readonly List<ListingModerationNote> _moderationNotes = [];

    private MarketplaceListing(
        MarketplaceListingId id,
        SellerProfileId sellerId,
        UserId createdByUserId,
        ListingTitle title,
        ListingSlug slug,
        ListingDescription description,
        string categoryKey,
        ListingType listingType,
        ProductCondition condition,
        ListingPrice price,
        ListingLocationContext location,
        DateTimeOffset createdAt)
        : base(id)
    {
        SellerId = sellerId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Slug = slug;
        Description = description;
        CategoryKey = categoryKey;
        ListingType = listingType;
        Condition = condition;
        Price = price;
        Location = location;
        Inventory = ListingInventory.SingleItem;
        Fulfillment = FulfillmentOptions.PickupOnly;
        Product = ProductIdentity.Unspecified;
        TrustMark = ListingTrustMark.None;
        Engagement = ListingEngagement.None;
        Status = ListingStatus.Draft;
        Visibility = ListingVisibility.Public;
        ModerationState = ListingModerationState.Clear;
        ContactPreference = ContactPreference.InApp;
        CreatedAt = createdAt;
    }

    private MarketplaceListing() { }

    // ── Ownership ───────────────────────────────────────────────────────────────────────────

    /// <summary>The seller profile trading it. The listing belongs to it, not to a person.</summary>
    public SellerProfileId SellerId { get; private set; } = default!;

    /// <summary>
    /// Who wrote it. Recorded for the audit trail and grants nothing — authority over a listing
    /// comes from the seller profile, which the seller aggregate answers.
    /// </summary>
    public UserId CreatedByUserId { get; private set; } = default!;

    /// <summary>
    /// The platform's ownership seam. The seller profile, so the kernel's ownership checks agree
    /// with the module's own.
    /// </summary>
    public Guid OwnerId => SellerId.Value;

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public ListingTitle Title { get; private set; } = default!;

    /// <summary>The stable handle a deep link uses. Allocated once and never recomputed.</summary>
    public ListingSlug Slug { get; private set; } = default!;

    public ListingDescription Description { get; private set; } = default!;

    /// <summary>The commerce domain, validated against the catalogue by the handler.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    public ListingType ListingType { get; private set; }

    public ProductCondition Condition { get; private set; }

    public ListingPrice Price { get; private set; } = default!;

    public ListingInventory Inventory { get; private set; } = default!;

    public ListingLocationContext Location { get; private set; } = default!;

    /// <summary>
    /// The pin the owner dropped, in WGS84 degrees. Both or neither. Stored since 14 Oct 2026 so
    /// this can be found by coordinates once the place catalogue is retired; nothing reads it yet.
    /// </summary>
    public double? Latitude { get; private set; }

    /// <inheritdoc cref="Latitude"/>
    public double? Longitude { get; private set; }

    public FulfillmentOptions Fulfillment { get; private set; } = default!;

    public ProductIdentity Product { get; private set; } = default!;

    /// <summary>How the seller would rather be contacted about this particular item.</summary>
    public ContactPreference ContactPreference { get; private set; }

    // ── Lifecycle & audience ────────────────────────────────────────────────────────────────

    public ListingStatus Status { get; private set; }

    public ListingVisibility Visibility { get; private set; }

    public ListingModerationState ModerationState { get; private set; }

    /// <summary>
    /// Whether the listing carries a live promotion that grants featured placement. Set by the
    /// promotion handler, never by a seller: a flag a seller could set is a flag every seller sets.
    /// </summary>
    public bool IsFeatured { get; private set; }

    public DateTimeOffset? FeaturedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    /// <summary>When it stops being offered. Null means it stays up until the seller acts.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? SoldAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Why the seller took it down. Shown to anyone mid-negotiation, who is owed one.</summary>
    public string? ClosureReason { get; private set; }

    // ── Denormalised blocks ─────────────────────────────────────────────────────────────────

    public ListingTrustMark TrustMark { get; private set; } = default!;

    public ListingEngagement Engagement { get; private set; } = default!;

    /// <summary>The optimistic concurrency token. Bumped by every content mutation.</summary>
    public int Revision { get; private set; }

    /// <summary>
    /// How many distinct members have reported it. Denormalised from the report aggregate so the
    /// moderation queue can sort by it without a correlated count per row.
    /// </summary>
    public int ReportCount { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<ListingAttribute> Attributes =>
        _attributes.OrderBy(attribute => attribute.Order).ToList().AsReadOnly();

    public IReadOnlyList<ListingTag> Tags =>
        _tags.OrderBy(tag => tag.Order).ToList().AsReadOnly();

    public IReadOnlyList<ListingMediaReference> MediaReferences =>
        _mediaReferences.OrderBy(reference => reference.Role)
            .ThenBy(reference => reference.Order)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<ListingProviderReference> ProviderReferences =>
        _providerReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<ListingGuideReference> GuideReferences =>
        _guideReferences.OrderBy(reference => reference.Order).ToList().AsReadOnly();

    public IReadOnlyList<ListingModerationNote> ModerationNotes =>
        _moderationNotes.OrderBy(note => note.CreatedAt).ToList().AsReadOnly();

    /// <summary>The card image, when the seller set one.</summary>
    public ListingMediaReference? CoverImage =>
        _mediaReferences.Find(reference => reference.Role == ListingMediaRole.Cover);

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The bar for publication: a title, a body, a category, a country, a price the listing type
    /// requires, a way of getting the item to a buyer, and at least one photograph.
    /// <para>
    /// <b>The photograph is not a stylistic preference.</b> A marketplace listing with no image is
    /// one nobody opens and one nobody can sanity-check against the description; requiring it is
    /// the cheapest fraud control the module has.
    /// </para>
    /// </summary>
    public bool IsPublishable => UnmetPublishRequirements.Count == 0;

    /// <summary>
    /// The publication prerequisites this listing does not yet meet, in the order a seller
    /// would work through them. Empty means <see cref="IsPublishable"/>.
    /// <para>
    /// <b>This exists so the refusal can name what is actually missing.</b> The bar has seven
    /// conditions and the error message named five of them, omitting fulfilment and the
    /// photograph — the two most likely to be unmet, because the other five are supplied at
    /// creation. A seller who added exactly what the message asked for was refused again by
    /// the same message, with nothing to act on. Enumerating the unmet conditions here keeps
    /// the rule and its explanation in one place, so they cannot drift apart again.
    /// </para>
    /// </summary>
    public IReadOnlyList<ListingPublishRequirement> UnmetPublishRequirements
    {
        get
        {
            var unmet = new List<ListingPublishRequirement>(capacity: 8);

            if (Title.Value.Length == 0) unmet.Add(ListingPublishRequirement.Title);
            if (Description.Body.Length == 0) unmet.Add(ListingPublishRequirement.Description);
            if (CategoryKey.Length == 0) unmet.Add(ListingPublishRequirement.Category);
            if (Location.CountryCode.Length == 0) unmet.Add(ListingPublishRequirement.Country);
            if (!HasRequiredPrice) unmet.Add(ListingPublishRequirement.Price);
            if (!Fulfillment.IsSpecified) unmet.Add(ListingPublishRequirement.Fulfillment);
            // A buying request has no item to photograph yet (16 Oct 2026): the photo is
            // optional there and required for everything that offers an item.
            if (ListingType != ListingType.Wanted
                && !_mediaReferences.Exists(reference =>
                    reference.Role is ListingMediaRole.Cover or ListingMediaRole.Gallery))
            {
                unmet.Add(ListingPublishRequirement.Photograph);
            }

            if (ListingType == ListingType.Swap && SwapTerms.WantsOf(_attributes) is null)
            {
                unmet.Add(ListingPublishRequirement.SwapWants);
            }

            return unmet;
        }
    }

    /// <summary>Whether the listing may appear in search, browse, category and nearby results.</summary>
    public bool IsDiscoverable =>
        !IsDeleted
        && ListingStatusTransitions.IsPubliclyVisible(Status)
        && Visibility == ListingVisibility.Public
        && ModerationState is ListingModerationState.Clear or ListingModerationState.Flagged;

    /// <summary>
    /// Whether a buyer could commit to it right now. Takes the clock, because a listing past its
    /// expiry is shut whether or not the sweep has run to notice.
    /// </summary>
    public bool AcceptsCommitments(DateTimeOffset now) =>
        !IsDeleted
        && ModerationState is ListingModerationState.Clear or ListingModerationState.Flagged
        && ListingStatusTransitions.AcceptsCommitments(Status)
        && Inventory.HasStock
        && (ExpiresAt is not { } expiry || expiry > now);

    /// <summary>
    /// Whether the seller invited offers and the listing is in a state to take one. A swap is
    /// answered with a conversation about what is offered back, not with a sum of money.
    /// </summary>
    public bool AcceptsOffers(DateTimeOffset now) =>
        AcceptsCommitments(now)
        && ListingType is not (ListingType.Giveaway or ListingType.Wanted or ListingType.Swap)
        && (Price.IsNegotiable || ListingType == ListingType.Negotiable);

    /// <summary>
    /// Whether a buyer may reserve. A wanted advert has nothing to hold, and a swap is agreed
    /// between the two members before anything is held — a reservation would snapshot the top-up
    /// as though it were the price of the item.
    /// </summary>
    public bool AcceptsReservations(DateTimeOffset now) =>
        AcceptsCommitments(now) && ListingType is not (ListingType.Wanted or ListingType.Swap);

    /// <summary>For a swap, what the seller would take in exchange. Null for every other type.</summary>
    public string? SwapWants =>
        ListingType == ListingType.Swap ? SwapTerms.WantsOf(_attributes) : null;

    /// <summary>For a donated item, the cause it goes to. Null for anything else.</summary>
    public Guid? DonationCauseId =>
        ListingType == ListingType.Giveaway ? DonationTerms.CauseIdOf(_attributes) : null;

    /// <summary>The donated item's cause title, as recorded when it was linked.</summary>
    public string? DonationCauseTitle =>
        DonationCauseId is null ? null : DonationTerms.CauseTitleOf(_attributes);

    /// <summary>For a swap, who adds money, if anybody. Null for every other type.</summary>
    public SwapTopUpDirection? SwapTopUp =>
        ListingType == ListingType.Swap ? SwapTerms.TopUpOf(_attributes) : null;

    /// <summary>Whether the expiry has passed. Drives the expiry sweep.</summary>
    public bool HasExpired(DateTimeOffset now) =>
        ExpiresAt is { } expiry && expiry <= now;

    /// <summary>Whether the listing type obliges a figure and one is present.</summary>
    private bool HasRequiredPrice => ListingType switch
    {
        // A wanted advert with no budget is ordinary — the whole point is asking what people want
        // for it.
        ListingType.Wanted => true,

        // A giveaway must say zero rather than say nothing, so the card can read "Free" and a
        // price filter can find it.
        ListingType.Giveaway => Price.IsDisclosed && Price.Amount == 0m,

        // An even swap has no price at all. A swap where somebody adds money has to say how much,
        // and that figure is what the price carries.
        ListingType.Swap => SwapTerms.TopUpOf(_attributes) == SwapTopUpDirection.None
            || (Price.IsDisclosed && Price.Amount is > 0m),

        _ => Price.IsDisclosed && Price.Amount is > 0m
    };

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a listing as a draft, visible only to its seller.
    /// <para>
    /// A draft rather than a live listing, deliberately: photographs are uploaded over several
    /// minutes and prices are reconsidered, and publishing straight from creation would put
    /// half-written listings with no images in front of buyers. Identifiers and the clock are
    /// passed in — the aggregate never generates either.
    /// </para>
    /// <para>
    /// <b><see cref="ListingType.Auction"/> is refused here.</b> The value exists so a future
    /// auction module has a name already in the wire contract and the database enum; accepting one
    /// today would create a listing that quietly behaves like a fixed price and would tell the
    /// seller nothing.
    /// </para>
    /// </summary>
    public static Result<MarketplaceListing> Create(
        MarketplaceListingId id,
        SellerProfileId sellerId,
        UserId createdByUserId,
        ListingTitle title,
        ListingSlug slug,
        ListingDescription description,
        string? categoryKey,
        ListingType listingType,
        ProductCondition condition,
        ListingPrice price,
        ListingLocationContext location,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(createdByUserId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(location);

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure<MarketplaceListing>(MarketplaceErrors.CategoryKeyInvalid);
        }

        if (!Enum.IsDefined(listingType))
        {
            return Result.Failure<MarketplaceListing>(MarketplaceErrors.ListingTypeInvalid);
        }

        if (listingType == ListingType.Auction)
        {
            return Result.Failure<MarketplaceListing>(MarketplaceErrors.AuctionNotSupported);
        }

        if (!Enum.IsDefined(condition))
        {
            return Result.Failure<MarketplaceListing>(MarketplaceErrors.ConditionInvalid);
        }

        var listing = new MarketplaceListing(
            id, sellerId, createdByUserId, title, slug, description, key,
            listingType, condition, price, location, now);

        // A giveaway that is negotiable is a contradiction the seller cannot see, and a wanted
        // advert holds no stock at all.
        if (listingType == ListingType.Wanted)
        {
            listing.Inventory = ListingInventory.Untracked;
        }

        listing.RaiseDomainEvent(new ListingCreatedDomainEvent
        {
            ListingId = id,
            SellerId = sellerId,
            CreatedBy = createdByUserId,
            Title = title.Value,
            Slug = slug.Value,
            CategoryKey = key,
            CountryCode = location.CountryCode
        });

        return Result.Success(listing);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Rejects a save based on a revision the editor no longer holds.
    /// <para>
    /// Null means the caller opted out — an internal write, or a client that genuinely wants
    /// last-writer-wins. A conflict is an <see cref="ErrorType.Conflict"/> so the client can offer
    /// a merge rather than an error page.
    /// </para>
    /// </summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(MarketplaceErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Pins this to a point. Both or neither: half a coordinate is a point on the equator or the
    /// prime meridian, not a missing value. Passing neither clears the pin.
    /// </summary>
    public Result SetCoordinates(double? latitude, double? longitude)
    {
        if ((latitude is null) != (longitude is null)
            || (latitude is { } lat && (double.IsNaN(lat) || lat is < -90d or > 90d))
            || (longitude is { } lon && (double.IsNaN(lon) || lon is < -180d or > 180d)))
        {
            return Result.Failure(MarketplaceErrors.CoordinatesInvalid);
        }

        Latitude = latitude;
        Longitude = longitude;

        return Result.Success();
    }

    /// <summary>
    /// Files an unfiled listing under the area its own geography lands in, without editing it.
    /// <para>
    /// <b>A correction, not an edit — so no event and no touch.</b> Before 16 Sep 2026 nothing on the
    /// platform was filed under an area, so the map's cluster counts and every neighbourhood count
    /// read zero. Putting a listing where it already was is not news to anybody, and must not move it
    /// up a "recently updated" list.
    /// </para>
    /// <para>
    /// Only the filing may change. A context that disagrees about the country, region or town the
    /// seller named is refused, so this can never become a back door for moving a listing.
    /// </para>
    /// </summary>
    /// <returns>Whether the listing was filed by this call.</returns>
    public bool FileUnderArea(ListingLocationContext filed)
    {
        ArgumentNullException.ThrowIfNull(filed);

        if (!string.IsNullOrWhiteSpace(Location.AreaPath)
            || string.IsNullOrWhiteSpace(filed.AreaPath)
            || filed.CountryCode != Location.CountryCode
            || filed.RegionName != Location.RegionName
            || filed.CityName != Location.CityName
            || filed.Neighbourhood != Location.Neighbourhood)
        {
            return false;
        }

        Location = filed;

        return true;
    }

    /// <summary>
    /// Updates everything a buyer reads that is not a list.
    /// <para>
    /// Reports which fields actually moved, so a consumer can ignore a change it does not care
    /// about. A price cut matters to everyone watching; a caption edit does not.
    /// </para>
    /// </summary>
    public Result UpdateDetails(
        ListingTitle title,
        ListingDescription description,
        ProductCondition condition,
        ListingPrice price,
        ListingLocationContext location,
        FulfillmentOptions fulfillment,
        ProductIdentity product,
        ContactPreference contactPreference,
        ListingVisibility visibility,
        int? baseRevision,
        UserId? changedBy = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(fulfillment);
        ArgumentNullException.ThrowIfNull(product);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (!Enum.IsDefined(condition) || !Enum.IsDefined(contactPreference)
            || !Enum.IsDefined(visibility))
        {
            return Result.Failure(MarketplaceErrors.ConditionInvalid);
        }

        // A giveaway that acquires a price is a listing type change, not a price edit, and
        // letting it happen quietly would take a free item off the free filters.
        if (ListingType == ListingType.Giveaway && price.IsDisclosed && price.Amount != 0m)
        {
            return Result.Failure(MarketplaceErrors.PriceInvalid);
        }

        var changed = new List<string>();

        Apply(!Title.Equals(title), "title", () => Title = title, changed);
        Apply(!Description.Equals(description), "description", () => Description = description, changed);
        Apply(Condition != condition, "condition", () => Condition = condition, changed);
        Apply(!Price.Equals(price), "price", () => Price = price, changed);
        Apply(!Location.Equals(location), "location", () => Location = location, changed);
        Apply(!Fulfillment.Equals(fulfillment), "fulfillment", () => Fulfillment = fulfillment, changed);
        Apply(!Product.Equals(product), "product", () => Product = product, changed);
        Apply(ContactPreference != contactPreference, "contact",
            () => ContactPreference = contactPreference, changed);
        Apply(Visibility != visibility, "visibility", () => Visibility = visibility, changed);

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();
        RaiseUpdated(changed, changedBy);

        return Result.Success();
    }

    /// <summary>
    /// Restates how many units there are.
    /// <para>
    /// Refuses a total below what is already reserved or sold, because the seller is correcting
    /// the wrong number: units a buyer is holding cannot be un-held by editing a form.
    /// </para>
    /// </summary>
    public Result SetQuantity(int quantity, bool isTracked, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (quantity < 0 || quantity > MarketplaceLimits.MaxQuantity)
        {
            return Result.Failure(MarketplaceErrors.QuantityInvalid);
        }

        var restated = Inventory.WithQuantity(quantity)?.WithTracking(isTracked);

        if (restated is null)
        {
            return Result.Failure(MarketplaceErrors.StockBelowCommitted);
        }

        if (Inventory.Equals(restated))
        {
            return Result.Success();
        }

        Inventory = restated;
        Touch();
        RaiseUpdated(["stock"], changedBy: null);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the attribute set wholesale.
    /// <para>
    /// Wholesale rather than per-attribute, because that is how the editor works: a seller fills
    /// in a category's form and the client saves it, and a per-attribute API would make every save
    /// a diff protocol with a partial-failure story. It is also what makes the category's required
    /// attributes checkable — they are validated against each other as a set, by the handler that
    /// knows the definitions.
    /// </para>
    /// </summary>
    public Result SetAttributes(
        IReadOnlyList<ListingAttributeDraft> attributes, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (attributes.Count > MarketplaceLimits.MaxAttributes)
        {
            return Result.Failure(MarketplaceErrors.AttributeLimit);
        }

        var rebuilt = new List<ListingAttribute>(attributes.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var draft in attributes)
        {
            if (draft is null)
            {
                return Result.Failure(MarketplaceErrors.AttributeInvalid);
            }

            var attribute = ListingAttribute.Create(
                draft.Key, draft.Value, draft.DisplayName, rebuilt.Count);

            if (attribute is null)
            {
                return Result.Failure(MarketplaceErrors.AttributeInvalid);
            }

            // A repeated key is a paste artefact rather than an error worth refusing the save
            // over; the first value wins, which is what the form shows.
            if (seen.Add(attribute.Key))
            {
                rebuilt.Add(attribute);
            }
        }

        _attributes.Clear();
        _attributes.AddRange(rebuilt);

        Touch();
        RaiseUpdated(["attributes"], changedBy: null);

        return Result.Success();
    }

    /// <summary>Replaces the tag set wholesale — that is how the client's chip editor saves.</summary>
    public Result SetTags(IReadOnlyList<string?> tags, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in tags)
        {
            var slug = ListingTag.NormalizeSlug(raw);

            if (slug is null)
            {
                return Result.Failure(MarketplaceErrors.TagInvalid);
            }

            if (seen.Add(slug))
            {
                resolved.Add(slug);
            }
        }

        if (resolved.Count > MarketplaceLimits.MaxTags)
        {
            return Result.Failure(MarketplaceErrors.TagLimit);
        }

        _tags.Clear();
        _tags.AddRange(resolved.Select((slug, index) => ListingTag.Create(slug, index)));

        Touch();
        RaiseUpdated(["tags"], changedBy: null);

        return Result.Success();
    }

    /// <summary>Moves the listing to another category.</summary>
    public Result ChangeCategory(string? categoryKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var key = NormalizeCategoryKey(categoryKey);

        if (key is null)
        {
            return Result.Failure(MarketplaceErrors.CategoryKeyInvalid);
        }

        if (string.Equals(CategoryKey, key, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var previous = CategoryKey;

        CategoryKey = key;
        Touch();

        RaiseDomainEvent(new ListingCategoryChangedDomainEvent
        {
            ListingId = Id,
            PreviousCategoryKey = previous,
            CurrentCategoryKey = key,
            IsDiscoverable = IsDiscoverable
        });

        RaiseUpdated(["category"], changedBy: null);

        return Result.Success();
    }

    /// <summary>
    /// Sets or clears the expiry.
    /// <para>
    /// Extending the date on an expired listing brings it back, which is the behaviour a seller
    /// expects and the reason expiry is modelled as reversible. Anything else would make them
    /// re-create the listing, losing every offer and every saved-by already attached.
    /// </para>
    /// </summary>
    public Result SetExpiry(DateTimeOffset? expiresAt, DateTimeOffset now, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (expiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure(MarketplaceErrors.ExpiryInPast);
        }

        if (ExpiresAt == expiresAt)
        {
            return Result.Success();
        }

        ExpiresAt = expiresAt;
        Touch();
        RaiseUpdated(["expiry"], changedBy: null);

        if (Status == ListingStatus.Expired && expiresAt is not null)
        {
            return MoveTo(ListingStatus.Active, now, changedBy: null, reason: null);
        }

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // References
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attaches a media asset by reference. Idempotent on (media, role).
    /// <para>
    /// A second cover replaces the first rather than being refused: a seller picking a new main
    /// photograph is doing the obvious thing, and an error telling them to remove the old one
    /// first would be the module being pedantic about its own data model.
    /// </para>
    /// </summary>
    public Result AttachMedia(Guid mediaId, ListingMediaRole role, string? caption = null)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty || !Enum.IsDefined(role))
        {
            return Result.Failure(MarketplaceErrors.MediaInvalid);
        }

        if (_mediaReferences.Exists(existing =>
                existing.MediaId == mediaId && existing.Role == role))
        {
            return Result.Success();
        }

        if (_mediaReferences.Count >= MarketplaceLimits.MaxMediaReferences)
        {
            return Result.Failure(MarketplaceErrors.MediaLimit);
        }

        if (role == ListingMediaRole.Cover)
        {
            _mediaReferences.RemoveAll(existing => existing.Role == ListingMediaRole.Cover);
        }

        _mediaReferences.Add(ListingMediaReference.Create(
            mediaId, role, _mediaReferences.Count(existing => existing.Role == role), caption));

        Touch();

        RaiseDomainEvent(new ListingMediaAttachedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Detaches a media asset. Idempotent — detaching what is not there succeeds.</summary>
    public Result DetachMedia(Guid mediaId, ListingMediaRole role)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.Find(existing =>
            existing.MediaId == mediaId && existing.Role == role);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new ListingMediaDetachedDomainEvent
        {
            ListingId = Id,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Points buyers at a shop in the Providers directory. Idempotent on the identifier.</summary>
    public Result AddProviderReference(Guid providerId, string? role, string? note)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (providerId == Guid.Empty)
        {
            return Result.Failure(MarketplaceErrors.ProviderReferenceInvalid);
        }

        if (_providerReferences.Exists(existing => existing.ProviderId == providerId))
        {
            return Result.Success();
        }

        if (_providerReferences.Count >= MarketplaceLimits.MaxProviderReferences)
        {
            return Result.Failure(MarketplaceErrors.ProviderReferenceInvalid);
        }

        _providerReferences.Add(ListingProviderReference.Create(
            providerId, role, note, _providerReferences.Count));

        Touch();

        return Result.Success();
    }

    /// <summary>Removes a provider reference. Idempotent.</summary>
    public Result RemoveProviderReference(Guid providerId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _providerReferences.Find(existing => existing.ProviderId == providerId);

        if (reference is null)
        {
            return Result.Success();
        }

        _providerReferences.Remove(reference);
        Touch();

        return Result.Success();
    }

    /// <summary>Links a guide beside the listing. Idempotent on the guide identifier.</summary>
    public Result AddGuideReference(Guid guideId, string? label, bool isAutomatic = false)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (guideId == Guid.Empty)
        {
            return Result.Failure(MarketplaceErrors.GuideReferenceInvalid);
        }

        if (_guideReferences.Exists(existing => existing.GuideId == guideId))
        {
            return Result.Success();
        }

        if (_guideReferences.Count >= MarketplaceLimits.MaxGuideReferences)
        {
            return Result.Failure(MarketplaceErrors.GuideReferenceInvalid);
        }

        _guideReferences.Add(ListingGuideReference.Create(
            guideId, label, isAutomatic, _guideReferences.Count));

        Touch();

        return Result.Success();
    }

    /// <summary>Removes a guide reference. Idempotent.</summary>
    public Result RemoveGuideReference(Guid guideId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _guideReferences.Find(existing => existing.GuideId == guideId);

        if (reference is null)
        {
            return Result.Success();
        }

        _guideReferences.Remove(reference);
        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Replaces every platform-suggested guide link, leaving the seller's own choices alone.
    /// <para>
    /// The split is the whole point of <see cref="ListingGuideReference.IsAutomatic"/>: suggestions
    /// are refreshed when the listing's country or category changes, and a refresh must never
    /// discard a link a person deliberately added.
    /// </para>
    /// </summary>
    public Result ReplaceAutomaticGuideReferences(
        IReadOnlyList<(Guid GuideId, string? Label)> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        _guideReferences.RemoveAll(reference => reference.IsAutomatic);

        var order = _guideReferences.Count;

        foreach (var (guideId, label) in suggestions)
        {
            if (guideId == Guid.Empty
                || _guideReferences.Count >= MarketplaceLimits.MaxGuideReferences
                || _guideReferences.Exists(existing => existing.GuideId == guideId))
            {
                continue;
            }

            _guideReferences.Add(
                ListingGuideReference.Create(guideId, label, isAutomatic: true, order));

            order++;
        }

        // Deliberately no Touch(): a platform-generated suggestion is not an editor's change and
        // must not invalidate a seller's in-flight save.
        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Publishes the listing, optionally making it available at once.
    /// <para>
    /// Guarded by <see cref="IsPublishable"/>, because a listing with no photograph and no price
    /// in a search result is worse than no listing. <paramref name="activateImmediately"/> is the
    /// ordinary case; publishing without activating is the shop that wants tomorrow's drop visible
    /// today.
    /// </para>
    /// </summary>
    public Result Publish(
        DateTimeOffset now, UserId? publishedBy = null, bool activateImmediately = true)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        var unmet = UnmetPublishRequirements;
        if (unmet.Count > 0)
        {
            return Result.Failure(MarketplaceErrors.ListingNotPublishableBecause(unmet));
        }

        if (ExpiresAt is { } expiry && expiry <= now)
        {
            return Result.Failure(MarketplaceErrors.ExpiryInPast);
        }

        var target = activateImmediately ? ListingStatus.Active : ListingStatus.Published;

        if (Status == target || !ListingStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(MarketplaceErrors.ListingAlreadyPublished);
        }

        var isFirstPublication = FirstPublishedAt is null;
        var previous = Status;

        Status = target;
        PublishedAt = now;
        FirstPublishedAt ??= now;
        ArchivedAt = null;
        ClosureReason = null;
        Touch();

        RaiseStatusChanged(previous, target, now, publishedBy, reason: null);

        RaiseDomainEvent(new ListingPublishedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            Title = Title.Value,
            Slug = Slug.Value,
            CategoryKey = CategoryKey,
            CountryCode = Location.CountryCode,
            PublishedBy = publishedBy,
            IsFirstPublication = isFirstPublication,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Moves the listing to another lifecycle state, subject to
    /// <see cref="ListingStatusTransitions"/>.
    /// <para>
    /// The single mutator every lifecycle command goes through, so the transition table is
    /// consulted in one place rather than at a dozen call sites. Selling and archiving have their
    /// own methods because they do more than move the status.
    /// </para>
    /// </summary>
    public Result MoveTo(
        ListingStatus target, DateTimeOffset now, UserId? changedBy, string? reason)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!ListingStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(ErrorForRefusedTransition());
        }

        var previous = Status;
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (trimmedReason is { Length: > MarketplaceLimits.MaxNoteLength })
        {
            trimmedReason = trimmedReason[..MarketplaceLimits.MaxNoteLength];
        }

        Status = target;

        switch (target)
        {
            case ListingStatus.Sold:
                SoldAt ??= now;
                break;

            case ListingStatus.Archived:
                ArchivedAt = now;

                // An archived listing keeps no featured placement: it is off every surface a
                // placement would apply to, and leaving the flag set would resurface it the
                // moment it was restored.
                IsFeatured = false;
                FeaturedAt = null;
                break;

            case ListingStatus.Active:
            case ListingStatus.Published:
                ArchivedAt = null;
                ClosureReason = null;
                break;
        }

        if (trimmedReason is not null)
        {
            ClosureReason = trimmedReason;
        }

        Touch();
        RaiseStatusChanged(previous, target, now, changedBy, trimmedReason);

        return Result.Success();
    }

    /// <summary>Expires the listing because its date passed. The sweep's entry point.</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (!HasExpired(now))
        {
            return Result.Success();
        }

        return ListingStatusTransitions.CanTransition(Status, ListingStatus.Expired)
            ? MoveTo(ListingStatus.Expired, now, changedBy: null, reason: null)
            : Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Stock
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds units for a reservation, and moves the listing to Reserved when the last one goes.
    /// <para>
    /// <b>Called from inside the reservation command's unit of work, never from an event
    /// handler.</b> A reservation that existed for even a moment without its hold is a window in
    /// which a second buyer takes the same item, and a handler runs after the commit.
    /// </para>
    /// <para>
    /// Deliberately no <c>Touch()</c>: a buyer reserving an item is not an edit, and must not make
    /// the seller's in-flight save fail.
    /// </para>
    /// </summary>
    public Result HoldStock(int units, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (!AcceptsCommitments(now))
        {
            return Result.Failure(StateError(now));
        }

        var held = Inventory.Hold(units);

        if (held is null)
        {
            return Result.Failure(MarketplaceErrors.InsufficientStock);
        }

        Inventory = held;
        Engagement = Engagement.WithReservationDelta(1);

        return Inventory.HasStock
            ? Result.Success()
            : MoveTo(ListingStatus.Reserved, now, changedBy: null, reason: null);
    }

    /// <summary>
    /// Returns held units to the shelf, and reopens the listing if it had gone Reserved.
    /// <para>
    /// The path a cancelled or expired reservation takes. Reopening is what makes a cancellation
    /// harmless to everybody but the buyer who cancelled.
    /// </para>
    /// </summary>
    public Result ReleaseStock(int units, DateTimeOffset now)
    {
        var released = Inventory.Release(units);

        if (released is null)
        {
            return Result.Failure(MarketplaceErrors.InsufficientStock);
        }

        Inventory = released;
        Engagement = Engagement.WithReservationDelta(-1);

        return Status == ListingStatus.Reserved && Inventory.HasStock
            ? MoveTo(ListingStatus.Active, now, changedBy: null, reason: null)
            : Result.Success();
    }

    /// <summary>
    /// Converts held units into sold units. The completion path of a reservation.
    /// <para>
    /// The only route from held to sold, which is what stops a completed reservation
    /// double-counting and a cancelled one selling anything.
    /// </para>
    /// </summary>
    public Result SettleStock(
        int units,
        DateTimeOffset now,
        UserId? buyerUserId,
        ListingReservationId? reservationId,
        decimal? amount,
        string? currencyCode)
    {
        var settled = Inventory.Settle(units);

        if (settled is null)
        {
            return Result.Failure(MarketplaceErrors.InsufficientStock);
        }

        Inventory = settled;
        Engagement = Engagement.WithReservationDelta(-1);
        SoldAt ??= now;

        RaiseSold(units, now, buyerUserId, reservationId, amount, currencyCode);

        if (Inventory.IsSoldOut)
        {
            return MoveTo(ListingStatus.Sold, now, changedBy: null, reason: null);
        }

        // Stock came back into circulation as the reservation resolved, so a listing that had
        // gone Reserved is available again.
        return Status == ListingStatus.Reserved && Inventory.HasStock
            ? MoveTo(ListingStatus.Active, now, changedBy: null, reason: null)
            : Result.Success();
    }

    /// <summary>
    /// Records a sale the seller made without a reservation behind it.
    /// <para>
    /// The "mark as sold" path, and the one most peer-to-peer sales actually take: two people meet,
    /// money changes hands, and the seller tells the platform afterwards. <paramref name="units"/>
    /// null sells whatever is left, which is what "mark sold" means on a single item.
    /// </para>
    /// </summary>
    public Result MarkSold(
        int? units,
        DateTimeOffset now,
        UserId? buyerUserId = null,
        decimal? amount = null,
        string? currencyCode = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (Status is ListingStatus.Draft or ListingStatus.Archived)
        {
            return Result.Failure(MarketplaceErrors.ListingNotPublished);
        }

        if (Status == ListingStatus.Sold)
        {
            return Result.Failure(MarketplaceErrors.ListingSold);
        }

        var sold = units is { } count ? Inventory.SellDirectly(count) : Inventory.SellOut();

        if (sold is null)
        {
            return Result.Failure(MarketplaceErrors.InsufficientStock);
        }

        var quantity = sold.Sold - Inventory.Sold;

        Inventory = sold;
        SoldAt ??= now;
        Touch();

        RaiseSold(quantity, now, buyerUserId, reservationId: null, amount, currencyCode);

        return Inventory.IsSoldOut || units is null
            ? MoveTo(ListingStatus.Sold, now, changedBy: null, reason: null)
            : Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Promotion
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gives the listing featured placement. Publicly visible listings only.
    /// <para>
    /// Reachable from the promotion aggregate's handler and from a staff command, and from nothing
    /// a seller can call directly — see <see cref="IsFeatured"/>.
    /// </para>
    /// </summary>
    public Result Feature(DateTimeOffset now, UserId? featuredBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (!ListingStatusTransitions.IsPubliclyVisible(Status))
        {
            return Result.Failure(MarketplaceErrors.ListingNotPublished);
        }

        if (IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = true;
        FeaturedAt = now;

        // Deliberately no Touch(): placement is not the seller's edit.
        RaiseDomainEvent(new ListingFeaturedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            IsFeatured = true,
            ChangedBy = featuredBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Withdraws featured placement. Idempotent.</summary>
    public Result Unfeature(DateTimeOffset now, UserId? changedBy = null)
    {
        if (!IsFeatured)
        {
            return Result.Success();
        }

        IsFeatured = false;
        FeaturedAt = null;

        RaiseDomainEvent(new ListingFeaturedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            IsFeatured = false,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Verification — projection of Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that a verification claim has gone to Trust. Says nothing about the outcome.
    /// </summary>
    /// <param name="verificationId">
    /// Trust's case identifier, when submission returned one. Stamping it here is what lets an
    /// outcome find this listing again: Trust's events name a subject, and a claim submitted
    /// without recording its identifier is one whose approval arrives with nowhere to land.
    /// </param>
    public Result SubmitForVerification(
        DateTimeOffset now, UserId requestedBy, Guid? verificationId = null)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (!ListingStatusTransitions.IsPubliclyVisible(Status))
        {
            return Result.Failure(MarketplaceErrors.ListingNotPublished);
        }

        if (TrustMark.State == ListingVerificationState.Pending)
        {
            return Result.Failure(MarketplaceErrors.VerificationAlreadyPending);
        }

        TrustMark = ListingTrustMark.Pending(now, verificationId);
        Touch();

        RaiseDomainEvent(new ListingVerificationRequestedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            RequestedBy = requestedBy,
            VerificationId = verificationId,
            RequestedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies an outcome Trust decided. The only path to a verified mark, reachable only from a
    /// handler for a Trust integration event.
    /// <para>
    /// Guarded on <paramref name="decidedAt"/> because delivery is at-least-once and unordered: a
    /// delayed redelivery of an old approval must not re-verify a listing Trust has since revoked.
    /// A stale decision is dropped silently — success, because from the handler's point of view
    /// the event was processed correctly.
    /// </para>
    /// </summary>
    public Result ApplyVerificationDecision(
        ListingVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset decidedAt)
    {
        if (TrustMark.IsNewerThan(decidedAt))
        {
            return Result.Success();
        }

        var previous = TrustMark.State;

        TrustMark = state switch
        {
            ListingVerificationState.Verified =>
                ListingTrustMark.Verified(verificationId, badge, decidedAt),
            ListingVerificationState.Revoked =>
                ListingTrustMark.Revoked(verificationId, reason, decidedAt),
            ListingVerificationState.Rejected =>
                ListingTrustMark.Rejected(verificationId, reason, decidedAt),
            ListingVerificationState.Pending =>
                ListingTrustMark.Pending(decidedAt, verificationId),
            _ => ListingTrustMark.Cleared(reason, decidedAt)
        };

        // Deliberately no Touch(): a Trust decision is not an editor's change.
        if (previous != TrustMark.State)
        {
            RaiseDomainEvent(new ListingVerificationChangedDomainEvent
            {
                ListingId = Id,
                PreviousState = previous,
                CurrentState = TrustMark.State,
                VerificationId = verificationId,
                Badge = badge,
                Reason = reason,
                ChangedAt = decidedAt
            });
        }

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Removes the listing from every public surface, reversibly. The listing's own state is
    /// untouched — lifting the decision restores exactly what was there.
    /// </summary>
    public Result Hide(string? reason, UserId? moderatorId, DateTimeOffset now)
    {
        // A null moderator is the system: the automatic hide after many serious reports.
        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MarketplaceLimits.MaxModerationNote)
        {
            return Result.Failure(MarketplaceErrors.ModerationNoteInvalid);
        }

        return SetModeration(ListingModerationState.Hidden, moderatorId, trimmed, now);
    }

    /// <summary>
    /// Hides the listing and marks it suspended — the end of a substantiated fraud report.
    /// <para>
    /// Distinct from <see cref="Hide"/> because the two say different things to the seller: hidden
    /// is "we are looking at this", and suspended is "we looked, and it stays down".
    /// </para>
    /// </summary>
    public Result Suspend(string? reason, UserId? moderatorId, DateTimeOffset now)
    {
        var trimmed = reason?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MarketplaceLimits.MaxModerationNote)
        {
            return Result.Failure(MarketplaceErrors.ModerationNoteInvalid);
        }

        return SetModeration(ListingModerationState.Suspended, moderatorId, trimmed, now);
    }

    /// <summary>Lifts a moderation hide or suspension. Idempotent.</summary>
    public Result Unhide(UserId? moderatorId, DateTimeOffset now) =>
        ModerationState is ListingModerationState.Hidden or ListingModerationState.Suspended
            ? SetModeration(ListingModerationState.Clear, moderatorId, reason: null, now)
            : Result.Success();

    /// <summary>
    /// Takes an automatic flag off, and nothing stronger. What dismissing a report does: before
    /// 7 Nov 2026 dismissal called <see cref="Unhide"/>, which ignores a flag, so the listing stayed
    /// flagged and came back to the top of the queue.
    /// </summary>
    public Result ClearFlag(UserId? moderatorId, DateTimeOffset now) =>
        ModerationState == ListingModerationState.Flagged
            ? SetModeration(ListingModerationState.Clear, moderatorId, reason: null, now)
            : Result.Success();

    /// <summary>
    /// Puts the listing in the moderation queue without taking it down. What a report produces.
    /// </summary>
    public Result Flag(UserId? flaggedBy, string? reason, DateTimeOffset now) =>
        ModerationState == ListingModerationState.Clear
            ? SetModeration(ListingModerationState.Flagged, flaggedBy, reason, now)
            : Result.Success();

    /// <summary>Appends to the editorial audit trail. The trail is never edited or pruned.</summary>
    public Result AddModerationNote(
        Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var trimmed = note?.Trim();

        if (noteId == Guid.Empty
            || string.IsNullOrEmpty(trimmed)
            || trimmed.Length > MarketplaceLimits.MaxModerationNote
            || _moderationNotes.Count >= MarketplaceLimits.MaxModerationNotes)
        {
            return Result.Failure(MarketplaceErrors.ModerationNoteInvalid);
        }

        _moderationNotes.Add(ListingModerationNote.Create(noteId, authorId, trimmed, now));

        // No Touch(): a moderator's note is not a content edit.
        return Result.Success();
    }

    /// <summary>Records that another member reported it. Applied by the report handler.</summary>
    public void RecordReport(int totalReports) =>
        ReportCount = Math.Max(ReportCount, totalReports);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement counters — never a revision bump, never an update event
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>A buyer opened the listing. Not an edit — no revision bump, no event.</summary>
    public void RecordView(bool unique = false) => Engagement = Engagement.WithView(unique);

    /// <summary>A member saved or unsaved it. Applied as a delta by the saved-listing handler.</summary>
    public void ApplySaveDelta(int delta) => Engagement = Engagement.WithSaveDelta(delta);

    /// <summary>A negotiation opened or closed. Keeps the card's offer count honest.</summary>
    public void ApplyOfferDelta(int delta) => Engagement = Engagement.WithOfferDelta(delta);

    /// <summary>A member shared it.</summary>
    public void RecordShare() => Engagement = Engagement.WithShare();

    /// <summary>The listing appeared in a result set.</summary>
    public void RecordImpressions(long count) => Engagement = Engagement.WithImpressions(count);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the listing deleted. Sets the fields directly rather than relying on the change
    /// tracker, for the reason B6 records: a delete that happens in the tracker cannot also raise
    /// an event, and downstream de-indexing needs one.
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new ListingDeletedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            DeletedAt = timestamp
        });
    }

    /// <summary>Undoes a soft delete. The listing resumes in whatever status it held.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new ListingRestoredDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            WasDeleted = true
        });
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    /// <summary>The shared guard for every edit: not deleted, not archived, revision current.</summary>
    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ListingDeleted);
        }

        if (Status == ListingStatus.Archived)
        {
            return Result.Failure(MarketplaceErrors.ListingArchivedImmutable);
        }

        return EnsureRevision(baseRevision);
    }

    private Result SetModeration(
        ListingModerationState target, UserId? actorId, string? reason, DateTimeOffset now)
    {
        if (ModerationState == target)
        {
            return Result.Success();
        }

        var previous = ModerationState;
        ModerationState = target;

        RaiseDomainEvent(new ListingModerationChangedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            PreviousState = previous,
            CurrentState = target,
            ModeratorId = actorId,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    private void RaiseSold(
        int units,
        DateTimeOffset now,
        UserId? buyerUserId,
        ListingReservationId? reservationId,
        decimal? amount,
        string? currencyCode) =>
        RaiseDomainEvent(new ListingSoldDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            BuyerUserId = buyerUserId,
            ReservationId = reservationId,
            Quantity = units,
            Amount = amount,
            CurrencyCode = currencyCode ?? Price.CurrencyCode,
            IsSoldOut = Inventory.IsSoldOut,
            TimeToSale = FirstPublishedAt is { } published ? now - published : null,
            SoldAt = now
        });

    private void RaiseStatusChanged(
        ListingStatus previous,
        ListingStatus current,
        DateTimeOffset now,
        UserId? changedBy,
        string? reason) =>
        RaiseDomainEvent(new ListingStatusChangedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            PreviousStatus = previous,
            CurrentStatus = current,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

    private void RaiseUpdated(IReadOnlyList<string> changedFields, UserId? changedBy) =>
        RaiseDomainEvent(new ListingUpdatedDomainEvent
        {
            ListingId = Id,
            SellerId = SellerId,
            ChangedBy = changedBy,
            ChangedFields = changedFields
        });

    private static void Apply(
        bool changed, string field, Action mutate, List<string> changedFields)
    {
        if (!changed)
        {
            return;
        }

        mutate();
        changedFields.Add(field);
    }

    /// <summary>
    /// The most specific reason a commitment was refused. A buyer told only "not available"
    /// cannot tell a sold item from an expired one, and the difference decides whether it is
    /// worth asking the seller.
    /// </summary>
    private Error StateError(DateTimeOffset now) => Status switch
    {
        ListingStatus.Sold => MarketplaceErrors.ListingSold,
        ListingStatus.Reserved => MarketplaceErrors.ListingReserved,
        ListingStatus.Expired => MarketplaceErrors.ListingExpired,
        ListingStatus.Draft or ListingStatus.Published =>
            MarketplaceErrors.ListingNotPublished,
        ListingStatus.Archived => MarketplaceErrors.ListingArchivedImmutable,
        _ when HasExpired(now) => MarketplaceErrors.ListingExpired,
        _ when !Inventory.HasStock => MarketplaceErrors.InsufficientStock,
        _ => MarketplaceErrors.ListingNotActive
    };

    private Error ErrorForRefusedTransition() => Status switch
    {
        ListingStatus.Sold => MarketplaceErrors.ListingSold,
        ListingStatus.Archived => MarketplaceErrors.ListingArchivedImmutable,
        ListingStatus.Expired => MarketplaceErrors.ListingExpired,
        ListingStatus.Draft => MarketplaceErrors.ListingNotPublished,
        _ => MarketplaceErrors.ListingTransitionInvalid
    };

    private static string? NormalizeCategoryKey(string? categoryKey)
    {
        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return null;
        }

        var key = categoryKey.Trim().ToLowerInvariant();

        var valid = key.Length <= MarketplaceLimits.MaxCategoryKey
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !key.StartsWith('-')
            && !key.EndsWith('-')
            && !key.Contains("--", StringComparison.Ordinal);

        return valid ? key : null;
    }
}
