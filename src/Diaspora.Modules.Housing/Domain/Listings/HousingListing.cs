using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// A place somebody could live.
/// <para>
/// <b>The aggregate is shaped by one observation: a home is chosen differently from a product.</b>
/// Nobody buys a flat from a photograph the way they buy a phone from one. They look at the
/// neighbourhood first, arrange to see it, and only then commit — which is why this aggregate has
/// a neighbourhood it belongs to rather than a location it sits at, why viewings are a first-class
/// workflow rather than a message, and why the address is stored in full and published in part.
/// </para>
/// <para>
/// <b>What it owns:</b> its own content, its amenities, its house rules, its media references, its
/// provider and guide references, its unit counts and its moderation trail. <b>What it points
/// at:</b> a landlord profile, an accommodation type by catalogue key, a B4 administrative area,
/// B5 media identifiers, B7 providers and B8 guides. Nothing about a neighbourhood is stored here
/// beyond the identifier and a cached name — the profile of the place is a separate aggregate,
/// because ten thousand listings in Peckham must not each carry their own copy of what Peckham is
/// like.
/// </para>
/// <para>
/// <b>Concurrency is optimistic and explicit through <see cref="Revision"/>.</b> An editor sends
/// back the revision it loaded and <see cref="EnsureRevision"/> rejects a stale save with a
/// conflict the client can merge from. Two landlords do not edit one property, but a landlord and
/// a moderator do, and an agency's staff genuinely do edit the same listing from two desks.
/// </para>
/// <para>
/// <b>Unit accounting lives in <see cref="Availability"/> and is settled from exactly one place:
/// the reservation status handler.</b> No command on this aggregate moves a unit directly, and
/// that is deliberate — units that could be moved from two places would be counted wrongly from
/// one of them, and the failure mode is a property nobody can ever let again.
/// </para>
/// </summary>
public sealed class HousingListing
    : AggregateRoot<HousingListingId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<HousingAmenityAssignment> _amenities = [];
    private readonly List<HouseRule> _houseRules = [];
    private readonly List<HousingMediaReference> _mediaReferences = [];
    private readonly List<HousingProviderReference> _providerReferences = [];
    private readonly List<HousingGuideReference> _guideReferences = [];
    private readonly List<HousingModerationNote> _moderationNotes = [];

    private HousingListing(
        HousingListingId id,
        LandlordProfileId landlordId,
        UserId createdByUserId,
        HousingTitle title,
        HousingSlug slug,
        HousingDescription description,
        string accommodationTypeKey,
        HousingPrice price,
        HousingCapacity capacity,
        HousingAvailability availability,
        HousingLocationContext location,
        PropertyAddress address,
        StayPolicy stayPolicy,
        DateTimeOffset createdAt) : base(id)
    {
        LandlordId = landlordId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Slug = slug;
        Description = description;
        AccommodationTypeKey = accommodationTypeKey;
        Price = price;
        Capacity = capacity;
        Availability = availability;
        Location = location;
        Address = address;
        StayPolicy = stayPolicy;
        RoommatePreferences = RoommatePreferences.None;
        TrustMark = HousingTrustMark.Unverified;
        Engagement = HousingEngagement.Empty;
        Status = HousingListingStatus.Draft;
        Visibility = HousingVisibility.Private;
        ModerationState = HousingModerationState.Clear;
        CreatedAt = createdAt;
    }

    private HousingListing() { }

    // ── Ownership & classification ──────────────────────────────────────────────────────────

    public LandlordProfileId LandlordId { get; private set; } = default!;

    /// <summary>
    /// The member who drafted it, which is not always the landlord's owner: an agency's staff
    /// create listings under the agency's profile, and an audit trail that recorded only the
    /// profile could not say which of six people published a listing that turned out to be a scam.
    /// </summary>
    public UserId CreatedByUserId { get; private set; } = default!;

    /// <summary>
    /// The accommodation-type catalogue key. A key rather than an identifier because the catalogue
    /// is configurable — see <see cref="Catalog.AccommodationType"/>.
    /// </summary>
    public string AccommodationTypeKey { get; private set; } = string.Empty;

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public HousingTitle Title { get; private set; } = default!;

    public HousingSlug Slug { get; private set; } = default!;

    public HousingDescription Description { get; private set; } = default!;

    public HousingPrice Price { get; private set; } = default!;

    public HousingCapacity Capacity { get; private set; } = default!;

    public HousingAvailability Availability { get; private set; } = default!;

    public HousingLocationContext Location { get; private set; } = default!;

    /// <summary>Stored in full, published in part. See <see cref="PropertyAddress"/>.</summary>
    public PropertyAddress Address { get; private set; } = default!;

    public StayPolicy StayPolicy { get; private set; } = default!;

    /// <summary>Empty on anything that is not shared accommodation. The aggregate enforces it.</summary>
    public RoommatePreferences RoommatePreferences { get; private set; } = default!;

    public LeaseType LeaseType { get; private set; }

    public FurnishedStatus FurnishedStatus { get; private set; }

    public ParkingAvailability Parking { get; private set; }

    public SmokingPolicy SmokingPolicy { get; private set; }

    public PetPolicy PetPolicy { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────
    //
    // Every field below is named for the property in front of it, and that is load-bearing rather
    // than tidy. Each getter hands back a fresh read-only view, so EF cannot write through it and
    // must find the backing field instead — which it does by a naming convention (`_camelCase` of
    // the property) and by nothing else. `_media` behind `MediaReferences` compiled, drafted,
    // published and saved perfectly well, and threw "Collection is read-only" the first time a
    // listing with a photograph was read back.

    public IReadOnlyList<HousingAmenityAssignment> Amenities => _amenities.AsReadOnly();

    public IReadOnlyList<HouseRule> HouseRules =>
        _houseRules.OrderBy(rule => rule.Order).ToList().AsReadOnly();

    public IReadOnlyList<HousingMediaReference> MediaReferences => _mediaReferences.AsReadOnly();

    public IReadOnlyList<HousingProviderReference> ProviderReferences =>
        _providerReferences.AsReadOnly();

    public IReadOnlyList<HousingGuideReference> GuideReferences =>
        _guideReferences.AsReadOnly();

    public IReadOnlyList<HousingModerationNote> ModerationNotes => _moderationNotes.AsReadOnly();

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public HousingListingStatus Status { get; private set; }

    public HousingVisibility Visibility { get; private set; }

    public HousingModerationState ModerationState { get; private set; }

    public HousingTrustMark TrustMark { get; private set; } = default!;

    public HousingEngagement Engagement { get; private set; } = default!;

    /// <summary>
    /// Whether this landlord asks for a viewing before a reservation. A per-listing setting rather
    /// than a per-landlord one: an agency requires a viewing on a family home and waves it for a
    /// hostel bed booked from another continent.
    /// </summary>
    public bool RequiresViewingBeforeReservation { get; private set; }

    public bool IsFeatured { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>The first publication, which never moves. What time-to-let is measured from.</summary>
    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? OccupiedAt { get; private set; }

    /// <summary>Distinct members who have reported it. Drives the auto-flag threshold.</summary>
    public int ReportCount { get; private set; }

    /// <summary>Bumped on every content change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>The image a card renders. Null until the landlord adds one.</summary>
    public HousingMediaReference? CoverImage =>
        _mediaReferences.FirstOrDefault(reference => reference.Role == HousingMediaRole.Cover)
        ?? _mediaReferences.Where(reference => reference.Role == HousingMediaRole.Gallery)
            .OrderBy(reference => reference.Order)
            .FirstOrDefault();

    /// <summary>Whether a member may currently ask to view it.</summary>
    public bool AcceptsViewings =>
        !IsDeleted
        && HousingListingStatusTransitions.AcceptsViewings(Status)
        && ModerationState is HousingModerationState.Clear or HousingModerationState.Flagged;

    /// <summary>Whether a member may currently reserve it.</summary>
    public bool AcceptsReservations(DateTimeOffset now) =>
        !IsDeleted
        && HousingListingStatusTransitions.AcceptsReservations(Status)
        && ModerationState is HousingModerationState.Clear or HousingModerationState.Flagged
        && Availability.AcceptsReservationAt(now);

    /// <summary>Whether the listing has the minimum a member needs to judge it.</summary>
    public bool IsPublishable =>
        _mediaReferences.Count > 0
        && Description.Summary.Length > 0
        && AccommodationTypeKey.Length > 0
        && ModerationState is not (HousingModerationState.Hidden
            or HousingModerationState.Suspended);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drafts a property.
    /// <para>
    /// Always into <see cref="HousingListingStatus.Draft"/> and
    /// <see cref="HousingVisibility.Private"/>, whatever the caller asked for. Publishing is a
    /// separate, deliberate act with its own permission and its own checks, and a create that
    /// could publish would let a client skip every one of them.
    /// </para>
    /// </summary>
    public static Result<HousingListing> Draft(
        HousingListingId id,
        LandlordProfileId landlordId,
        UserId createdByUserId,
        HousingTitle title,
        HousingSlug slug,
        HousingDescription description,
        string? accommodationTypeKey,
        HousingPrice price,
        HousingCapacity capacity,
        HousingAvailability availability,
        HousingLocationContext location,
        PropertyAddress address,
        StayPolicy stayPolicy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(createdByUserId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(stayPolicy);

        var typeKey = NormalizeTypeKey(accommodationTypeKey);

        if (typeKey is null)
        {
            return Result.Failure<HousingListing>(HousingErrors.AccommodationTypeInvalid);
        }

        var listing = new HousingListing(
            id,
            landlordId,
            createdByUserId,
            title,
            slug,
            description,
            typeKey,
            price,
            capacity,
            availability,
            location,
            address,
            stayPolicy,
            createdAt);

        listing.RaiseDomainEvent(new HousingListingCreatedDomainEvent
        {
            ListingId = id,
            LandlordId = landlordId,
            CreatedBy = createdByUserId,
            Title = title.Value,
            Slug = slug.Value,
            AccommodationTypeKey = typeKey,
            CountryCode = location.CountryCode,
            NeighborhoodAreaId = location.NeighborhoodAreaId
        });

        return Result.Success(listing);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the listing's descriptive content.
    /// <para>
    /// Every parameter is optional and null means "leave it alone", so a client editing one field
    /// need not round-trip the other twelve — and cannot blank a field by omitting it, which is
    /// the failure mode of a naive replace.
    /// </para>
    /// </summary>
    public Result<IReadOnlyList<string>> UpdateContent(
        int? baseRevision,
        UserId? changedBy,
        HousingTitle? title = null,
        HousingDescription? description = null,
        HousingPrice? price = null,
        HousingCapacity? capacity = null,
        LeaseType? leaseType = null,
        FurnishedStatus? furnishedStatus = null,
        ParkingAvailability? parking = null,
        SmokingPolicy? smokingPolicy = null,
        PetPolicy? petPolicy = null,
        bool? requiresViewingBeforeReservation = null)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        var changed = new List<string>();

        if (title is not null && title != Title)
        {
            Title = title;
            changed.Add(nameof(Title));
        }

        if (description is not null && description != Description)
        {
            Description = description;
            changed.Add(nameof(Description));
        }

        if (price is not null && price != Price)
        {
            Price = price;
            changed.Add(nameof(Price));
        }

        if (capacity is not null && capacity != Capacity)
        {
            Capacity = capacity;
            changed.Add(nameof(Capacity));
        }

        if (leaseType is { } lease && lease != LeaseType)
        {
            LeaseType = lease;
            changed.Add(nameof(LeaseType));
        }

        if (furnishedStatus is { } furnished && furnished != FurnishedStatus)
        {
            FurnishedStatus = furnished;
            changed.Add(nameof(FurnishedStatus));
        }

        if (parking is { } parkingValue && parkingValue != Parking)
        {
            Parking = parkingValue;
            changed.Add(nameof(Parking));
        }

        if (smokingPolicy is { } smoking && smoking != SmokingPolicy)
        {
            SmokingPolicy = smoking;
            changed.Add(nameof(SmokingPolicy));
        }

        if (petPolicy is { } pets && pets != PetPolicy)
        {
            PetPolicy = pets;
            changed.Add(nameof(PetPolicy));
        }

        if (requiresViewingBeforeReservation is { } requiresViewing
            && requiresViewing != RequiresViewingBeforeReservation)
        {
            RequiresViewingBeforeReservation = requiresViewing;
            changed.Add(nameof(RequiresViewingBeforeReservation));
        }

        if (changed.Count == 0)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        Touch();
        RaiseUpdated(changed, changedBy);

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>Moves the property to a different place in B4's hierarchy.</summary>
    public Result ChangeLocation(
        HousingLocationContext location, PropertyAddress? address, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(location);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var changed = new List<string>();

        if (location != Location)
        {
            Location = location;
            changed.Add(nameof(Location));
        }

        if (address is not null && address != Address)
        {
            Address = address;
            changed.Add(nameof(Address));
        }

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();
        RaiseUpdated(changed, null);

        return Result.Success();
    }

    /// <summary>
    /// Restates who the property is for.
    /// <para>
    /// <b>Roommate preferences are cleared when the property stops being shared</b>, rather than
    /// left behind as dead data. A gender preference surviving on a listing that has become a whole
    /// flat is precisely the discriminatory advert this module refuses to publish.
    /// </para>
    /// </summary>
    public Result SetStayPolicy(StayPolicy stayPolicy, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(stayPolicy);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        StayPolicy = stayPolicy;

        if (!stayPolicy.IsSharedAccommodation)
        {
            RoommatePreferences = RoommatePreferences.None;
        }

        Touch();
        RaiseUpdated([nameof(StayPolicy)], null);

        return Result.Success();
    }

    /// <summary>
    /// Records what an existing household is looking for.
    /// <para>
    /// <b>Refused on anything that is not shared accommodation, and a gender preference is refused
    /// even there unless the room really is in somebody's home.</b> That is the one content rule in
    /// this module written for a legal reason rather than a product one: a household choosing a
    /// flatmate is lawful in most of the markets this platform serves, and the same restriction on
    /// a whole property is housing discrimination in nearly all of them.
    /// </para>
    /// </summary>
    public Result SetRoommatePreferences(RoommatePreferences preferences, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        // The gender rule is checked first because it is the one with a legal reason behind it and
        // it earns its own error: a landlord told "preferences are not permitted here" would try
        // again with a smaller set, where one told the truth would not.
        if (preferences.GenderPreference is not GenderPreference.Any
                and not GenderPreference.FamilyOnly
            && !StayPolicy.IsSharedAccommodation)
        {
            return Result.Failure(HousingErrors.GenderPreferenceNotPermitted);
        }

        // FamilyOnly survives this, and deliberately — a family let is a condition on the property,
        // not a description of a roommate. Everything else on the type describes a roommate and so
        // means nothing on a home nobody is sharing.
        if (!preferences.IsHouseholdCompositionOnly && !StayPolicy.IsSharedAccommodation)
        {
            return Result.Failure(HousingErrors.RoommatePreferencesNotPermitted);
        }

        RoommatePreferences = preferences;

        Touch();
        RaiseUpdated([nameof(RoommatePreferences)], null);

        return Result.Success();
    }

    /// <summary>Replaces the amenity list wholesale.</summary>
    public Result SetAmenities(
        IReadOnlyList<HousingAmenityAssignment> amenities, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(amenities);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var deduplicated = amenities
            .Where(amenity => amenity.AmenityKey.Length > 0)
            .GroupBy(amenity => amenity.AmenityKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (deduplicated.Count > HousingLimits.MaxAmenities)
        {
            return Result.Failure(HousingErrors.AmenityLimit);
        }

        _amenities.Clear();
        _amenities.AddRange(deduplicated);

        Touch();
        RaiseUpdated([nameof(Amenities)], null);

        return Result.Success();
    }

    /// <summary>Replaces the house rules wholesale, renumbering them from zero.</summary>
    public Result SetHouseRules(IReadOnlyList<HouseRule> rules, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var kept = rules.Where(rule => rule.Text.Length > 0).ToList();

        if (kept.Count > HousingLimits.MaxHouseRules)
        {
            return Result.Failure(HousingErrors.HouseRuleLimit);
        }

        _houseRules.Clear();
        _houseRules.AddRange(kept);

        Touch();
        RaiseUpdated([nameof(HouseRules)], null);

        return Result.Success();
    }

    /// <summary>Restates the availability window, leaving the unit counts alone.</summary>
    public Result SetAvailabilityWindow(
        DateTimeOffset? availableFrom,
        DateTimeOffset? availableUntil,
        int? minimumStayNights,
        int? maximumStayNights,
        int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var updated = Availability.WithWindow(
            availableFrom, availableUntil, minimumStayNights, maximumStayNights);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        Availability = updated.Value;

        Touch();
        RaiseUpdated([nameof(Availability)], null);

        return Result.Success();
    }

    /// <summary>
    /// Restates how many identical units the listing advertises.
    /// <para>
    /// Refused below what is already reserved or occupied — a hostel cannot shrink to four beds
    /// while five people are booked into them.
    /// </para>
    /// </summary>
    public Result SetTotalUnits(int totalUnits, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var updated = Availability.WithTotalUnits(totalUnits);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        Availability = updated.Value;

        Touch();
        RaiseUpdated([nameof(Availability)], null);

        return Result.Success();
    }

    /// <summary>Changes how much of the address the platform will publish.</summary>
    public Result SetAddressVisibility(AddressVisibility visibility, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        Address = Address.WithVisibility(visibility);

        Touch();
        RaiseUpdated([nameof(Address)], null);

        return Result.Success();
    }

    /// <summary>Moves the listing to a different accommodation type.</summary>
    public Result ChangeAccommodationType(string? accommodationTypeKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var typeKey = NormalizeTypeKey(accommodationTypeKey);

        if (typeKey is null)
        {
            return Result.Failure(HousingErrors.AccommodationTypeInvalid);
        }

        if (string.Equals(typeKey, AccommodationTypeKey, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        AccommodationTypeKey = typeKey;

        Touch();
        RaiseUpdated([nameof(AccommodationTypeKey)], null);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Media
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attaches a Media asset by reference.
    /// <para>
    /// Idempotent on (media, role): retrying after a timeout gives the same gallery rather than
    /// two of the same photograph. Attaching a second cover replaces the first, because there is
    /// one card image and a listing with two would render whichever the database returned first.
    /// </para>
    /// </summary>
    public Result AttachMedia(
        Guid mediaId, HousingMediaRole role, string? caption, DateTimeOffset now)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty)
        {
            return Result.Failure(HousingErrors.MediaInvalid);
        }

        if (_mediaReferences.Count >= HousingLimits.MaxMediaReferences)
        {
            return Result.Failure(HousingErrors.MediaLimit);
        }

        var existing = _mediaReferences.FirstOrDefault(
            reference => reference.MediaId == mediaId && reference.Role == role);

        if (existing is not null)
        {
            return Result.Success();
        }

        if (role == HousingMediaRole.Cover)
        {
            _mediaReferences.RemoveAll(reference => reference.Role == HousingMediaRole.Cover);
        }

        var order = _mediaReferences.Count(reference => reference.Role == role);

        _mediaReferences.Add(HousingMediaReference.Create(mediaId, role, order, caption));

        Touch();
        ModifiedAt = now;

        RaiseDomainEvent(new HousingMediaAttachedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Removes a media reference. Never deletes the asset — that is Media's decision.</summary>
    public Result DetachMedia(Guid mediaId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.FirstOrDefault(item => item.MediaId == mediaId);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new HousingMediaDetachedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            MediaId = mediaId,
            Role = reference.Role
        });

        return Result.Success();
    }

    /// <summary>Replaces the provider references — the managing agent, the relocation partner.</summary>
    public Result SetProviderReferences(IReadOnlyList<HousingProviderReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (references.Count > HousingLimits.MaxProviderReferences)
        {
            return Result.Failure(HousingErrors.ProviderReferenceInvalid);
        }

        _providerReferences.Clear();
        _providerReferences.AddRange(
            references.GroupBy(reference => reference.ProviderId).Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    /// <summary>Replaces the contextual guide references.</summary>
    public Result SetGuideReferences(IReadOnlyList<HousingGuideReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (references.Count > HousingLimits.MaxGuideReferences)
        {
            return Result.Failure(HousingErrors.GuideReferenceInvalid);
        }

        _guideReferences.Clear();
        _guideReferences.AddRange(
            references.GroupBy(reference => reference.GuideId).Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Puts the property on the market.
    /// <para>
    /// Lands in <see cref="HousingListingStatus.Available"/> when the move-in date has arrived and
    /// in <see cref="HousingListingStatus.Published"/> when it has not. That is the whole reason
    /// the two states exist: a flat advertised in March for a June tenancy is discoverable and not
    /// yet occupiable, and one status cannot say both.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        if (Status is HousingListingStatus.Published or HousingListingStatus.Available)
        {
            return Result.Failure(HousingErrors.ListingAlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(HousingErrors.ListingNotPublishable);
        }

        var target = Availability.IsAvailableOn(now)
            ? HousingListingStatus.Available
            : HousingListingStatus.Published;

        var moved = MoveTo(target, now, publishedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        var isFirstPublication = FirstPublishedAt is null;

        PublishedAt = now;
        FirstPublishedAt ??= now;
        Visibility = Visibility == HousingVisibility.Private
            ? HousingVisibility.Public
            : Visibility;

        RaiseDomainEvent(new HousingListingPublishedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            Title = Title.Value,
            Slug = Slug.Value,
            AccommodationTypeKey = AccommodationTypeKey,
            CountryCode = Location.CountryCode,
            NeighborhoodAreaId = Location.NeighborhoodAreaId,
            IsFirstPublication = isFirstPublication,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The general lifecycle move. Every transition goes through here so the machine is consulted
    /// once and the status event is raised once.
    /// </summary>
    public Result MoveTo(
        HousingListingStatus target,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!HousingListingStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(HousingErrors.ListingTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        ModifiedAt = now;

        if (target is HousingListingStatus.Draft or HousingListingStatus.Archived)
        {
            Visibility = HousingVisibility.Private;
        }

        RaiseDomainEvent(new HousingListingStatusChangedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies the outcome of a reservation to the unit counts, and moves the lifecycle to match.
    /// <para>
    /// <b>The single settlement path, called only from the reservation status handler.</b> Every
    /// hold, release and occupation in the module arrives here, which is what makes the counts
    /// reconcilable: there is exactly one place to read to know how a unit can move.
    /// </para>
    /// </summary>
    public Result SettleUnits(
        int held, int released, int occupied, int vacated, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        var availability = Availability;

        if (held > 0)
        {
            var attempt = availability.TryHold(held);

            if (attempt.IsFailure)
            {
                return Result.Failure(attempt.Error);
            }

            availability = attempt.Value;
        }

        if (occupied > 0)
        {
            availability = availability.Occupy(occupied);
        }

        if (released > 0)
        {
            availability = availability.Release(released);
        }

        if (vacated > 0)
        {
            availability = availability.Vacate(vacated);
        }

        Availability = availability;
        ModifiedAt = now;

        return SynchroniseStatusWithUnits(now);
    }

    /// <summary>
    /// Records that somebody moved in, and raises the module's success event.
    /// </summary>
    public Result RecordOccupation(
        int units,
        UserId? occupantUserId,
        HousingReservationId? reservationId,
        DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        OccupiedAt ??= now;

        RaiseDomainEvent(new HousingListingOccupiedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            OccupantUserId = occupantUserId,
            ReservationId = reservationId,
            Units = units,
            IsFullyOccupied = Availability.IsFullyOccupied,
            TimeToLet = FirstPublishedAt is { } published ? now - published : null,
            OccupiedAt = now
        });

        return Result.Success();
    }

    /// <summary>Takes the property off the market without archiving it.</summary>
    public Result MakeUnavailable(DateTimeOffset now, UserId? changedBy, string? reason) =>
        MoveTo(HousingListingStatus.Unavailable, now, changedBy, reason);

    /// <summary>Withdraws the property. Reversible only through a restore into draft.</summary>
    public Result Archive(DateTimeOffset now, UserId? changedBy, string? reason) =>
        MoveTo(HousingListingStatus.Archived, now, changedBy, reason);

    /// <summary>
    /// Brings an archived property back, as a draft.
    /// <para>
    /// Never straight to available: a property that has been off the market wants its rent, its
    /// dates and its photographs looked at before anybody is shown it again.
    /// </para>
    /// </summary>
    public Result RestoreFromArchive(DateTimeOffset now, UserId? changedBy)
    {
        if (Status != HousingListingStatus.Archived)
        {
            return Result.Failure(HousingErrors.ListingNotArchived);
        }

        var moved = MoveTo(HousingListingStatus.Draft, now, changedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        RaiseDomainEvent(new HousingListingRestoredDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            WasDeleted = false
        });

        return Result.Success();
    }

    /// <summary>Changes who can reach the listing, independent of its lifecycle state.</summary>
    public Result SetVisibility(HousingVisibility visibility, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (Visibility == visibility)
        {
            return Result.Success();
        }

        Visibility = visibility;

        Touch();
        RaiseUpdated([nameof(Visibility)], null);

        return Result.Success();
    }

    /// <summary>Promotes or demotes the listing. Staff only, gated at the endpoint.</summary>
    public void SetFeatured(bool isFeatured, DateTimeOffset now)
    {
        if (IsFeatured == isFeatured)
        {
            return;
        }

        IsFeatured = isFeatured;
        ModifiedAt = now;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation & safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a report and flags the listing when the threshold is met.
    /// <para>
    /// The threshold is passed in rather than read from options here, because the domain must not
    /// know about configuration — and because a fraud report flags on the first one regardless,
    /// which the caller expresses by passing a threshold of one.
    /// </para>
    /// </summary>
    public bool RecordReport(int reportsBeforeAutoFlag, DateTimeOffset now, UserId? reportedBy)
    {
        ReportCount++;
        ModifiedAt = now;

        if (ModerationState != HousingModerationState.Clear
            || ReportCount < Math.Max(1, reportsBeforeAutoFlag))
        {
            return false;
        }

        ChangeModeration(
            HousingModerationState.Flagged, reportedBy, "Automatically flagged by reports.", now);

        return true;
    }

    /// <summary>Removes the listing from discovery. The landlord can still see and fix it.</summary>
    public void Hide(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(HousingModerationState.Hidden, moderatorId, reason, now);

    /// <summary>Removes the listing and bars republication pending a decision on the landlord.</summary>
    public void Suspend(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(HousingModerationState.Suspended, moderatorId, reason, now);

    /// <summary>Clears a flag, a hide or a suspension.</summary>
    public void Clear(UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(HousingModerationState.Clear, moderatorId, null, now);

    /// <summary>
    /// Adds a note to the editorial trail.
    /// <para>
    /// Deliberately not a content edit: it raises no update event and does not bump the revision,
    /// so annotating a listing cannot make the landlord's in-flight save fail.
    /// </para>
    /// </summary>
    public void AddModerationNote(
        Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        if (_moderationNotes.Count >= HousingLimits.MaxModerationNotes)
        {
            return;
        }

        _moderationNotes.Add(HousingModerationNote.Create(noteId, authorId, note, now));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Trust
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records that a verification claim has been lodged for this property.</summary>
    public Result SubmitForVerification(
        Guid verificationId, UserId requestedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (TrustMark.IsPending)
        {
            return Result.Failure(HousingErrors.VerificationAlreadyPending);
        }

        TrustMark = HousingTrustMark.Submitted(verificationId, now);
        ModifiedAt = now;

        RaiseDomainEvent(new HousingVerificationRequestedDomainEvent
        {
            LandlordId = LandlordId,
            ListingId = Id,
            RequestedBy = requestedBy,
            VerificationId = verificationId
        });

        return Result.Success();
    }

    /// <summary>
    /// Applies Trust's decision. The only route to a verified property, and called only from the
    /// Trust outcome handler.
    /// </summary>
    public void ApplyVerificationOutcome(
        HousingVerificationState state,
        Guid? verificationId,
        string? badge,
        string? reason,
        DateTimeOffset now)
    {
        var previous = TrustMark.State;

        if (previous == state)
        {
            return;
        }

        TrustMark = HousingTrustMark.FromOutcome(state, verificationId, badge, now, reason);
        ModifiedAt = now;

        RaiseDomainEvent(new HousingVerificationChangedDomainEvent
        {
            LandlordId = LandlordId,
            ListingId = Id,
            PreviousState = previous,
            CurrentState = state,
            VerificationId = verificationId,
            Badge = badge,
            Reason = reason,
            ChangedAt = now
        });
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a view.
    /// <para>
    /// Deliberately does not bump the revision. A member opening a listing while its landlord is
    /// editing it must not make the landlord's save fail.
    /// </para>
    /// </summary>
    public void RecordView(bool isUnique) => Engagement = Engagement.RecordView(isUnique);

    public void RecordSave() => Engagement = Engagement.RecordSave();

    public void RecordUnsave() => Engagement = Engagement.RecordUnsave();

    public void RecordViewingRequest() => Engagement = Engagement.RecordViewingRequest();

    public void RecordReservationRequest() => Engagement = Engagement.RecordReservation();

    public void RecordEnquiry() => Engagement = Engagement.RecordEnquiry();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new HousingListingDeletedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            DeletedAt = timestamp
        });
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new HousingListingRestoredDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            WasDeleted = true
        });
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save made against a revision somebody else has already superseded.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(HousingErrors.RevisionConflict)
            : Result.Success();

    /// <summary>
    /// Keeps the lifecycle honest about the unit counts.
    /// <para>
    /// Called after every settlement. A property whose last unit is held becomes Reserved; one
    /// whose last unit is lived in becomes Occupied; and one that gets a unit back comes off both
    /// — which is the transition that makes a cancelled reservation immediately re-lettable.
    /// </para>
    /// </summary>
    private Result SynchroniseStatusWithUnits(DateTimeOffset now)
    {
        // Nothing to synchronise on a listing that is not on the market. A draft with units held
        // is not a state the module can reach, and an archived one must not be resurrected by a
        // reservation expiring.
        if (Status is HousingListingStatus.Draft
            or HousingListingStatus.Archived
            or HousingListingStatus.Unavailable)
        {
            return Result.Success();
        }

        var target = Availability switch
        {
            { IsFullyOccupied: true } => HousingListingStatus.Occupied,
            { IsFullyCommitted: true } => HousingListingStatus.Reserved,
            _ when Availability.IsAvailableOn(now) => HousingListingStatus.Available,
            _ => HousingListingStatus.Published
        };

        return Status == target ? Result.Success() : MoveTo(target, now);
    }

    private void ChangeModeration(
        HousingModerationState state, UserId? moderatorId, string? reason, DateTimeOffset now)
    {
        if (ModerationState == state)
        {
            return;
        }

        var previous = ModerationState;

        ModerationState = state;
        ModifiedAt = now;

        RaiseDomainEvent(new HousingModerationChangedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            PreviousState = previous,
            CurrentState = state,
            ChangedBy = moderatorId,
            Reason = reason
        });
    }

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        if (Status == HousingListingStatus.Archived)
        {
            return Result.Failure(HousingErrors.ListingArchivedImmutable);
        }

        return EnsureRevision(baseRevision);
    }

    private void RaiseUpdated(IReadOnlyList<string> changedFields, UserId? changedBy) =>
        RaiseDomainEvent(new HousingListingUpdatedDomainEvent
        {
            ListingId = Id,
            LandlordId = LandlordId,
            ChangedBy = changedBy,
            ChangedFields = changedFields
        });

    private void Touch() => Revision++;

    /// <summary>
    /// The comparable form of an accommodation-type key. Shared with the catalogue so a
    /// contributor supplying "Shared Room" and a listing supplying "shared-room" land on one type.
    /// </summary>
    public static string? NormalizeTypeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        return normalized.Length > HousingLimits.MaxTypeKey ? null : normalized;
    }
}
