using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for housing listings.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded listing cannot validate an amenity replacement against amenities it never
/// loaded. <see cref="GetForReadAsync"/> loads the same graph untracked for the detail screen.
/// <see cref="QueryAsync"/> loads the <em>card</em> graph: media and amenities, nothing else.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and
/// owned collections only when asked — exactly the trap that makes a card query fast and a command
/// silently operate on a listing with no amenities in it.
/// </para>
/// <para>
/// <b>Visibility is applied here, from the filter's audience fields, and nowhere else.</b> Drafts
/// and private listings appear only to their own landlord and to staff; hidden ones only to staff.
/// Soft-deleted rows never appear at all — the shared context's global filter owns that predicate.
/// </para>
/// </summary>
public sealed class HousingListingRepository : IHousingListingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public HousingListingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingListing> Listings => _dbContext.Set<HousingListing>();

    public async Task<HousingListing?> GetAsync(
        HousingListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings)
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<HousingListing?> GetForReadAsync(
        HousingListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<HousingListing?> GetIncludingDeletedAsync(
        HousingListingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings.IgnoreQueryFilters())
            .FirstOrDefaultAsync(listing => listing.Id == id, cancellationToken);

    public async Task<HousingListing?> GetBySlugAsync(
        HousingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Listings.AsNoTracking())
            .FirstOrDefaultAsync(listing => listing.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<HousingListing>> GetManyAsync(
        IReadOnlyCollection<HousingListingId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Compare whole identifiers, not their inner Guids. `listing.Id.Value` reaches through the
        // strongly-typed id to a member that has no column of its own — the converter maps the id
        // as a unit — so EF cannot translate it and throws at query time rather than compile time.
        var keys = ids.ToList();

        return await CardGraph(Listings.AsNoTracking())
            .Where(listing => keys.Contains(listing.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<HousingListing?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await FullGraph(Listings)
            .FirstOrDefaultAsync(
                listing => listing.TrustMark.VerificationId == verificationId, cancellationToken);

    public async Task<CursorPagedResult<HousingListing>> QueryAsync(
        HousingListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = CardGraph(Apply(Listings.AsNoTracking(), filter));

        query = ApplyOrderAndCursor(query, filter, HousingCursor.Parse(cursor));

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);

        // One more than asked for, so "is there a next page" is answered by the same query rather
        // than by a second count over the same predicate.
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1], filter.Sort) : null;

        return CursorPagedResult<HousingListing>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        HousingListingQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Listings.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        HousingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // Ignores the soft-delete filter: a deleted listing still holds its slug, and handing it to
        // a new property would break every link ever shared to the old one — which on this platform
        // are in emails between people arranging to move.
        return await Listings.IgnoreQueryFilters()
            .AnyAsync(listing => listing.Slug.Value == value, cancellationToken);
    }

    public async Task<int> CountActiveByLandlordAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        var live = new[]
        {
            HousingListingStatus.Draft,
            HousingListingStatus.Published,
            HousingListingStatus.Available,
            HousingListingStatus.Reserved,
            HousingListingStatus.Occupied,
            HousingListingStatus.Unavailable
        };

        // Drafts count. It is drafts a script can create for free that the abuse bound exists to
        // stop, not published listings a landlord wrote.
        return await Listings.CountAsync(
            listing => listing.LandlordId == landlordId && live.Contains(listing.Status),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HousingListing>> GetByLandlordAsync(
        LandlordProfileId landlordId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        return await CardGraph(Listings.AsNoTracking())
            .Where(listing => listing.LandlordId == landlordId)
            .OrderByDescending(listing => listing.CreatedAt)
            .Take(Math.Clamp(limit, 1, 1_000))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDuplicateAsync(
        LandlordProfileId landlordId,
        string title,
        HousingListingId? excluding = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        var normalized = HousingTitle.Normalize(title);

        if (normalized is null)
        {
            return false;
        }

        var query = Listings.AsNoTracking()
            .Where(listing =>
                listing.LandlordId == landlordId
                && listing.Title.Value == normalized
                && listing.Status != HousingListingStatus.Archived);

        if (excluding is not null)
        {
            query = query.Where(listing => listing.Id != excluding);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HousingListing>> GetBecomingAvailableAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Listings
            .Where(listing =>
                listing.Status == HousingListingStatus.Published
                && listing.Availability.AvailableFrom != null
                && listing.Availability.AvailableFrom <= asOf)
            .OrderBy(listing => listing.Availability.AvailableFrom)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<int> CountInAreaAsync(
        string areaPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(areaPath))
        {
            return 0;
        }

        return await PubliclyVisible(Listings.AsNoTracking())
            .Where(listing =>
                listing.Location.AreaPath != null
                && listing.Location.AreaPath.StartsWith(areaPath))
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = HousingLocationContext.NormalizeCountry(countryCode);

        if (country is null)
        {
            return 0;
        }

        return await PubliclyVisible(Listings.AsNoTracking())
            .Where(listing => listing.Location.CountryCode == country)
            .CountAsync(cancellationToken);
    }

    public async Task<int> CountInNeighborhoodAsync(
        Guid areaId, CancellationToken cancellationToken = default)
    {
        if (areaId == Guid.Empty)
        {
            return 0;
        }

        return await PubliclyVisible(Listings.AsNoTracking())
            .Where(listing => listing.Location.NeighborhoodAreaId == areaId)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// One grouped query rather than one per neighbourhood. The directory renders dozens at a time
    /// and a count per row would be a fan of aggregates on every page.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> CountByNeighborhoodAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areaIds);

        if (areaIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var keys = areaIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await PubliclyVisible(Listings.AsNoTracking())
            .Where(listing =>
                listing.Location.NeighborhoodAreaId != null
                && keys.Contains(listing.Location.NeighborhoodAreaId.Value))
            .GroupBy(listing => listing.Location.NeighborhoodAreaId!.Value)
            .Select(group => new { AreaId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.AreaId, row => row.Count);
    }

    public async Task AddAsync(
        HousingListing listing, CancellationToken cancellationToken = default) =>
        await Listings.AddAsync(listing, cancellationToken);

    public void Remove(HousingListing listing) => Listings.Remove(listing);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Graphs
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Everything. For a command that must enforce the aggregate's own invariants.</summary>
    private static IQueryable<HousingListing> FullGraph(IQueryable<HousingListing> query) =>
        query
            .Include(listing => listing.Amenities)
            .Include(listing => listing.HouseRules)
            .Include(listing => listing.MediaReferences)
            .Include(listing => listing.ProviderReferences)
            .Include(listing => listing.GuideReferences)
            .Include(listing => listing.ModerationNotes);

    /// <summary>
    /// What a card renders: the cover image and the amenity chips. Deliberately excludes the house
    /// rules, the references and the moderation trail.
    /// </summary>
    private static IQueryable<HousingListing> CardGraph(IQueryable<HousingListing> query) =>
        query
            .Include(listing => listing.Amenities)
            .Include(listing => listing.MediaReferences);

    /// <summary>
    /// The predicate every count-for-another-module uses. Written once because Location's counter,
    /// the neighbourhood profile's count and the country page must all mean the same thing by
    /// "a property that is here".
    /// </summary>
    private static IQueryable<HousingListing> PubliclyVisible(IQueryable<HousingListing> query) =>
        query.Where(listing =>
            listing.Visibility == HousingVisibility.Public
            && listing.ModerationState == HousingModerationState.Clear
            && (listing.Status == HousingListingStatus.Available
                || listing.Status == HousingListingStatus.Published
                || listing.Status == HousingListingStatus.Reserved));

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Filtering
    // ════════════════════════════════════════════════════════════════════════════════════════

    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls are inside an expression tree. They are never executed in .NET: EF "
            + "translates them to SQL lower(), which runs under the database collation. There is no "
            + "CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "The StringComparison overloads have no translation on either provider, so they would "
            + "move the search into memory. Lowering both sides is what makes the match "
            + "case-insensitive on Postgres, where LIKE and = are not.")]
    private IQueryable<HousingListing> Apply(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        query = ApplyAudience(query, filter);

        if (filter.ListingIds is { Count: > 0 })
        {
            var ids = filter.ListingIds.ToList();
            query = query.Where(listing => ids.Contains(listing.Id));
        }

        if (filter.LandlordId is { } landlordId)
        {
            query = query.Where(listing => listing.LandlordId == landlordId);
        }

        if (filter.ProviderId is { } providerId)
        {
            // Correlated against the landlord rather than denormalised onto the listing: an agency
            // that unlinks its provider should stop being that agency everywhere at once.
            var landlords = _dbContext.Set<LandlordProfile>()
                .Where(landlord => landlord.ProviderId == providerId)
                .Select(landlord => landlord.Id);

            query = query.Where(listing => landlords.Contains(listing.LandlordId));
        }

        query = ApplyLifecycle(query, filter);
        query = ApplyClassification(query, filter);
        query = ApplyCapacity(query, filter);
        query = ApplyMoney(query, filter);
        query = ApplyAudienceFit(query, filter);
        query = ApplyGeography(query, filter);
        query = ApplyTrustAndModeration(query, filter);

        if (filter.PublishedAfter is { } publishedAfter)
        {
            query = query.Where(listing => listing.PublishedAt > publishedAfter);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // A contains-match over the write model. Honest and adequate until a real engine
            // arrives; IHousingSearchEngine is the seam that replaces it without a domain change.
            //
            // Lowered on both sides since 15 Sep 2026. LIKE is case-sensitive on Postgres, so a
            // member typing "rose hill" did not find the flat in "Rose Hill". The same lesson the
            // community directory learned; ILike would not translate on the SQLite test host.
            var term = filter.Text.Trim().ToLowerInvariant();

            query = query.Where(listing =>
                listing.Title.Value.ToLower().Contains(term)
                || listing.Description.Summary.ToLower().Contains(term)
                || (listing.Location.NeighborhoodName != null
                    && listing.Location.NeighborhoodName.ToLower().Contains(term))
                || (listing.Location.CityName != null
                    && listing.Location.CityName.ToLower().Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// The one place drafts, unlisted and hidden listings are let through. Every listing query in
    /// the module goes through it, so the rule is written once rather than remembered per caller.
    /// </summary>
    private static IQueryable<HousingListing> ApplyAudience(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.ViewerIsStaff)
        {
            return query;
        }

        if (filter.IncludeNonPublic && filter.ViewerLandlordId is { } viewerLandlordId)
        {
            // The landlord's own dashboard: everything of theirs, nothing of anyone else's that is
            // not already public.
            return query.Where(listing =>
                listing.LandlordId == viewerLandlordId
                || (listing.Visibility == HousingVisibility.Public
                    && (listing.ModerationState == HousingModerationState.Clear
                        || listing.ModerationState == HousingModerationState.Flagged)
                    && listing.Status != HousingListingStatus.Draft
                    && listing.Status != HousingListingStatus.Archived));
        }

        return query.Where(listing =>
            listing.Visibility == HousingVisibility.Public
            && (listing.ModerationState == HousingModerationState.Clear
                || listing.ModerationState == HousingModerationState.Flagged)
            && listing.Status != HousingListingStatus.Draft
            && listing.Status != HousingListingStatus.Archived);
    }

    private static IQueryable<HousingListing> ApplyLifecycle(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(listing => statuses.Contains(listing.Status));
        }

        if (filter.AvailableOnly)
        {
            query = query.Where(listing =>
                listing.Status == HousingListingStatus.Available
                && listing.Availability.TotalUnits
                    - listing.Availability.ReservedUnits
                    - listing.Availability.OccupiedUnits > 0);
        }

        if (filter.AvailableBy is { } availableBy)
        {
            // A null move-in date means "now", which satisfies every by-date. Excluding it would
            // hide exactly the properties somebody in a hurry most wants.
            query = query.Where(listing =>
                listing.Availability.AvailableFrom == null
                || listing.Availability.AvailableFrom <= availableBy);
        }

        return query;
    }

    private IQueryable<HousingListing> ApplyClassification(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.AccommodationTypeKeys is { Count: > 0 })
        {
            var keys = filter.AccommodationTypeKeys
                .Select(HousingListing.NormalizeTypeKey)
                .OfType<string>()
                .ToList();

            if (keys.Count > 0)
            {
                query = query.Where(listing => keys.Contains(listing.AccommodationTypeKey));
            }
        }

        if (filter.LeaseTypes is { Count: > 0 })
        {
            var leaseTypes = filter.LeaseTypes.ToList();
            query = query.Where(listing => leaseTypes.Contains(listing.LeaseType));
        }

        if (filter.MinimumFurnishedStatus is { } furnished)
        {
            // Ordered least to most, so "at least part-furnished" is a range rather than a set
            // membership test over a hand-maintained list.
            query = query.Where(listing => listing.FurnishedStatus >= furnished);
        }

        if (filter.AmenityKeys is { Count: > 0 })
        {
            // Every one must be present, unlike a tag search where any match qualifies: somebody
            // asking for a washing machine and a lift means both. One EXISTS per key, which the
            // index on the amenity key serves.
            foreach (var key in filter.AmenityKeys
                .Select(HousingAmenityAssignment.NormalizeKey)
                .OfType<string>())
            {
                var amenityKey = key;

                query = query.Where(listing =>
                    listing.Amenities.Any(amenity => amenity.AmenityKey == amenityKey));
            }
        }

        if (filter.AccessibleOnly)
        {
            // Correlated against the catalogue rather than a flag on the assignment: whether an
            // amenity is an accessibility feature is the catalogue's decision, and copying it onto
            // every assignment would freeze the answer at the moment the listing was written.
            var accessible = _dbContext.Set<HousingAmenity>()
                .Where(amenity => amenity.IsAccessibilityFeature && amenity.IsActive)
                .Select(amenity => amenity.Key);

            query = query.Where(listing =>
                listing.Amenities.Any(amenity => accessible.Contains(amenity.AmenityKey)));
        }

        return query;
    }

    private static IQueryable<HousingListing> ApplyCapacity(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.MinimumBedrooms is { } minimumBedrooms)
        {
            query = query.Where(listing => listing.Capacity.Bedrooms >= minimumBedrooms);
        }

        if (filter.MaximumBedrooms is { } maximumBedrooms)
        {
            query = query.Where(listing => listing.Capacity.Bedrooms <= maximumBedrooms);
        }

        if (filter.MinimumBathrooms is { } minimumBathrooms)
        {
            query = query.Where(listing => listing.Capacity.Bathrooms >= minimumBathrooms);
        }

        if (filter.MinimumOccupants is { } occupants)
        {
            // A property that did not state a maximum is not excluded: an unstated ceiling is
            // unknown rather than zero, and hiding it would remove most private landlords.
            query = query.Where(listing =>
                listing.Capacity.MaximumOccupants == null
                || listing.Capacity.MaximumOccupants >= occupants);
        }

        return query;
    }

    /// <summary>
    /// Rent narrowing, scoped to one currency and one period.
    /// <para>
    /// Nothing here converts and nothing normalises. A conversion needs a rate, a rate has a date,
    /// and a rent silently restated is a figure the landlord did not write. A period normalisation
    /// is worse: a nightly hostel rate divided into a monthly equivalent is a number nobody could
    /// ever pay. A filter that matches nothing is better than one that matches wrongly.
    /// </para>
    /// </summary>
    private static IQueryable<HousingListing> ApplyMoney(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.UtilitiesIncludedOnly)
        {
            query = query.Where(listing => listing.Price.UtilitiesIncluded);
        }

        if (filter.InternetIncludedOnly)
        {
            query = query.Where(listing => listing.Price.InternetIncluded);
        }

        if (filter.NoDepositOnly)
        {
            query = query.Where(listing =>
                listing.Price.DepositAmount == null || listing.Price.DepositAmount == 0m);
        }

        // Currency and period narrow on their own, and that is not a convenience. "Monthly rents
        // in pounds" is a search somebody actually makes, and answering it with nightly hostel
        // beds priced in naira is worse than answering it with nothing: the figures look like
        // bargains and they are not comparable at all.
        if (!string.IsNullOrWhiteSpace(filter.Currency))
        {
            var scoped = filter.Currency.Trim().ToUpperInvariant();

            query = query.Where(listing =>
                listing.Price.IsDisclosed && listing.Price.CurrencyCode == scoped);
        }

        if (filter.Period is { } scopedPeriod)
        {
            query = query.Where(listing => listing.Price.Period == scopedPeriod);
        }

        if (filter.MinimumPrice is null && filter.MaximumPrice is null)
        {
            return query;
        }

        if (string.IsNullOrWhiteSpace(filter.Currency) || filter.Period is null)
        {
            // A range needs both to mean anything. £60 and ₦60,000 are not near each other, and
            // £60 a night and £60 a month are not either — so the amounts are left alone while
            // whichever of the two was supplied still narrows. The validator warns the caller.
            return query;
        }

        if (filter.MinimumPrice is { } minimum)
        {
            query = query.Where(listing => listing.Price.Amount >= minimum);
        }

        if (filter.MaximumPrice is { } maximum)
        {
            query = query.Where(listing => listing.Price.Amount <= maximum);
        }

        return query;
    }

    private static IQueryable<HousingListing> ApplyAudienceFit(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (filter.StudentFriendlyOnly)
        {
            query = query.Where(listing => listing.StayPolicy.IsStudentFriendly);
        }

        if (filter.FamilyFriendlyOnly)
        {
            query = query.Where(listing => listing.StayPolicy.IsFamilyFriendly);
        }

        if (filter.ShortStayOnly)
        {
            query = query.Where(listing => listing.StayPolicy.AcceptsShortStay);
        }

        if (filter.LongStayOnly)
        {
            query = query.Where(listing => listing.StayPolicy.AcceptsLongStay);
        }

        if (filter.SharedOnly)
        {
            query = query.Where(listing => listing.StayPolicy.IsSharedAccommodation);
        }

        if (filter.RoommateWantedOnly)
        {
            query = query.Where(listing => listing.StayPolicy.IsLookingForRoommate);
        }

        if (filter.PetsAllowedOnly)
        {
            query = query.Where(listing =>
                listing.PetPolicy == PetPolicy.Allowed
                || listing.PetPolicy == PetPolicy.Negotiable
                || listing.PetPolicy == PetPolicy.CatsOnly
                || listing.PetPolicy == PetPolicy.DogsOnly);
        }

        if (filter.ParkingOnly)
        {
            query = query.Where(listing =>
                listing.Parking != ParkingAvailability.None
                && listing.Parking != ParkingAvailability.Unknown);
        }

        return query;
    }

    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls are inside an expression tree. They are never executed in .NET: EF "
            + "translates them to SQL lower(), which runs under the database collation. There is no "
            + "CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "The StringComparison overloads have no translation on either provider, so they would "
            + "move the search into memory. Lowering both sides is what makes the match "
            + "case-insensitive on Postgres, where LIKE and = are not.")]
    private static IQueryable<HousingListing> ApplyGeography(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(listing => listing.Location.CountryCode == country);
        }

        if (filter.NeighborhoodAreaId is { } areaId && areaId != Guid.Empty)
        {
            // Exact, not a prefix. A neighbourhood page means this neighbourhood.
            query = query.Where(listing => listing.Location.NeighborhoodAreaId == areaId);
        }
        else if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            // A plain string prefix match — B4's EF lesson. The materialised path is stored
            // precisely so this is a LIKE 'prefix%' against an indexed column rather than a
            // recursive descent through the hierarchy.
            var prefix = filter.AreaPathPrefix;

            if (filter.AreaPathCityFallback is { Length: > 0 } fallback)
            {
                // A listing with no area is still in its town. Lowered on both sides rather than
                // ILike, which is Npgsql-only and does not translate on the SQLite test host.
                var town = fallback.Trim().ToLowerInvariant();

                query = query.Where(listing =>
                    (listing.Location.AreaPath != null
                        && listing.Location.AreaPath.StartsWith(prefix))
                    || (listing.Location.AreaPath == null
                        && listing.Location.CityName != null
                        && listing.Location.CityName.ToLower() == town));
            }
            else
            {
                query = query.Where(listing =>
                    listing.Location.AreaPath != null
                    && listing.Location.AreaPath.StartsWith(prefix));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(listing => listing.Location.CityName == city);
        }

        if (filter.HasBounds)
        {
            var south = filter.BoundsSouth!.Value;
            var north = filter.BoundsNorth!.Value;
            var west = filter.BoundsWest!.Value;
            var east = filter.BoundsEast!.Value;

            // A listing with no coordinate cannot be inside a rectangle, and is excluded rather
            // than kept "just in case" — a map that shows a pin for a property whose position
            // nobody recorded is showing it somewhere it is not.
            //
            // Two branches rather than one predicate carrying a captured bool, so each shape
            // stays a plain pair of range comparisons over the indexed columns.
            query = filter.BoundsCrossAntimeridian
                ? query.Where(listing =>
                    listing.Address.Latitude != null
                    && listing.Address.Longitude != null
                    && listing.Address.Latitude >= south
                    && listing.Address.Latitude <= north
                    && (listing.Address.Longitude >= west || listing.Address.Longitude <= east))
                : query.Where(listing =>
                    listing.Address.Latitude != null
                    && listing.Address.Longitude != null
                    && listing.Address.Latitude >= south
                    && listing.Address.Latitude <= north
                    && listing.Address.Longitude >= west
                    && listing.Address.Longitude <= east);
        }

        return query;
    }

    private IQueryable<HousingListing> ApplyTrustAndModeration(
        IQueryable<HousingListing> query, HousingListingQueryFilter filter)
    {
        // Correlated against the landlord rather than a denormalised flag on the listing. A badge
        // copied onto the listing at publication would keep saying "verified" after Trust revoked
        // it — which is the platform vouching for exactly the wrong party, and on a housing
        // platform the wrong party is collecting deposits.
        if (filter.VerifiedLandlordsOnly)
        {
            var verified = _dbContext.Set<LandlordProfile>()
                .Where(landlord =>
                    landlord.TrustMark.State == HousingVerificationState.Verified
                    && landlord.Status == LandlordStatus.Active)
                .Select(landlord => landlord.Id);

            query = query.Where(listing => verified.Contains(listing.LandlordId));
        }

        if (filter.LandlordKinds is { Count: > 0 })
        {
            var kinds = filter.LandlordKinds.ToList();

            var matching = _dbContext.Set<LandlordProfile>()
                .Where(landlord => kinds.Contains(landlord.Identity.Kind))
                .Select(landlord => landlord.Id);

            query = query.Where(listing => matching.Contains(listing.LandlordId));
        }

        // The moderation queue's own filters. The audience predicate has already refused these to
        // anybody but staff, so applying them for a member narrows an already-empty set.
        if (filter.ReportedOnly)
        {
            query = query.Where(listing => listing.ReportCount > 0);
        }

        if (filter.ModerationState is { } moderationState)
        {
            query = query.Where(listing => listing.ModerationState == moderationState);
        }

        return query;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Ordering & cursors
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orders the query and applies the continuation.
    /// <para>
    /// Every ordering ends with the slug, which is the tie-break: unique, indexed and a string,
    /// because the primary key is a value-converted identifier EF cannot compare in SQL. Without a
    /// stable tie-break, two properties published in the same millisecond can swap places between
    /// pages and one of them is never shown — which on this platform is a home somebody never saw.
    /// </para>
    /// <para>
    /// <b>CA1309 is suppressed across the whole method, and it has to be.</b> The analyser wants
    /// <c>string.Compare(a, b, StringComparison.Ordinal)</c>, and EF cannot translate that overload
    /// — the query would fall back to client evaluation, which for a cursor predicate means loading
    /// the table to find page two. The two-argument overload translates to the database's own
    /// collation, which is what actually orders the rows, so comparing any other way would in fact
    /// be the bug. B8, B9 and B10 carry the same suppression for the same reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<HousingListing> ApplyOrderAndCursor(
        IQueryable<HousingListing> query,
        HousingListingQueryFilter filter,
        HousingCursor? cursor)
    {
        switch (filter.Sort)
        {
            case HousingSortOrder.PriceLowToHigh:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Price.Amount > (decimal)value
                        || (listing.Price.Amount == (decimal)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderBy(listing => listing.Price.Amount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case HousingSortOrder.PriceHighToLow:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Price.Amount < (decimal)value
                        || (listing.Price.Amount == (decimal)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.Price.Amount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case HousingSortOrder.MostViewed:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Engagement.ViewCount < (long)value
                        || (listing.Engagement.ViewCount == (long)value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.Engagement.ViewCount)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case HousingSortOrder.AvailableSoonest:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.Availability.AvailableFrom > value
                        || (listing.Availability.AvailableFrom == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                // A null move-in date means "now" and sorts first, which is exactly right for
                // somebody with a flight booked: available today beats available in June.
                return query
                    .OrderBy(listing => listing.Availability.AvailableFrom != null)
                    .ThenBy(listing => listing.Availability.AvailableFrom)
                    .ThenBy(listing => listing.Slug.Value);
            }

            case HousingSortOrder.Verified:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.PublishedAt < value
                        || (listing.PublishedAt == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                // Verified first, then newest. A badge lifts a property and never buries a better
                // match, which is why this is a secondary key rather than a score.
                return query
                    .OrderByDescending(listing =>
                        listing.TrustMark.State == HousingVerificationState.Verified)
                    .ThenByDescending(listing => listing.PublishedAt)
                    .ThenBy(listing => listing.Slug.Value);
            }

            // Relevance and Nearest both degrade to newest here, and honestly: the shipped EF
            // engine has no score and no geometry. A real engine implements both properly behind
            // IHousingSearchEngine, and nothing above it changes.
            default:
            {
                if (cursor?.AsTimestamp() is { } value)
                {
                    query = query.Where(listing =>
                        listing.PublishedAt < value
                        || (listing.PublishedAt == value
                            && string.Compare(listing.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(listing => listing.IsFeatured)
                    .ThenByDescending(listing => listing.PublishedAt)
                    .ThenBy(listing => listing.Slug.Value);
            }
        }
    }
#pragma warning restore CA1309

    private static string EncodeCursor(HousingListing last, HousingSortOrder sort) => sort switch
    {
        HousingSortOrder.PriceLowToHigh or HousingSortOrder.PriceHighToLow =>
            HousingCursor.FromNumber((double)(last.Price.Amount ?? 0m), last.Slug.Value).Encode(),

        HousingSortOrder.MostViewed =>
            HousingCursor.FromNumber(last.Engagement.ViewCount, last.Slug.Value).Encode(),

        HousingSortOrder.AvailableSoonest =>
            HousingCursor.FromTimestamp(
                last.Availability.AvailableFrom ?? DateTimeOffset.MinValue,
                last.Slug.Value).Encode(),

        _ => HousingCursor.FromTimestamp(
            last.PublishedAt ?? last.CreatedAt, last.Slug.Value).Encode()
    };
}
