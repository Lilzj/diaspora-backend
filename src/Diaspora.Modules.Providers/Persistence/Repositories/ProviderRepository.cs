using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Search;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence.Repositories;

/// <summary>
/// EF Core repository for providers.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded provider cannot check the last-owner rule. <see cref="GetForReadAsync"/>
/// loads what the profile screen renders. <see cref="QueryAsync"/> loads only what a card needs;
/// a page of twenty cards that eagerly loaded every gallery, service list and weekly schedule
/// would be several thousand rows to render twenty names.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned <em>references</em>
/// eagerly and owned <em>collections</em> only when asked, which is exactly the trap that makes
/// a card query fast and a command silently operate on an aggregate with no members in it.
/// </para>
/// </summary>
public sealed class ProviderRepository : IProviderRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProviderRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Provider> Providers => _dbContext.Set<Provider>();

    public async Task<Provider?> GetAsync(
        ProviderId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Providers)
            .FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task<Provider?> GetForReadAsync(
        ProviderId id, CancellationToken cancellationToken = default) =>
        await ReadingGraph(Providers.AsNoTracking())
            .FirstOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task<Provider?> GetBySlugAsync(
        ProviderSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await ReadingGraph(Providers.AsNoTracking())
            .FirstOrDefaultAsync(provider => provider.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<Provider>> GetManyAsync(
        IReadOnlyCollection<ProviderId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Compared as ProviderId, not as Guid. EF translates Contains over a value-converted
        // property; projecting .Value inside the expression makes the whole query untranslatable,
        // which is a run-time failure the compiler cannot see.
        var keys = ids.ToList();

        return await CardGraph(Providers.AsNoTracking())
            .Where(provider => keys.Contains(provider.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<Provider>> QueryAsync(
        ProviderQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = CardGraph(Apply(Providers.AsNoTracking(), filter));

        var cursor = ProviderCursor.Parse(filter.Cursor);

        query = ApplyOrderAndCursor(query, filter, cursor);

        var limit = filter.EffectiveLimit;

        // One more than asked for, so "is there a next page" is answered by the same query
        // rather than by a second count.
        var items = await query.Take(limit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        int? total = filter.IncludeTotalCount
            ? await Apply(Providers.AsNoTracking(), filter).CountAsync(cancellationToken)
            : null;

        var nextCursor = hasMore && items.Count > 0
            ? BuildCursor(items[^1], filter.Sort).Encode()
            : null;

        return CursorPagedResult<Provider>.Create(items, nextCursor, hasMore, total);
    }

    public async Task<IReadOnlyList<ProviderNearestCandidate>> QueryNearestCandidatesAsync(
        ProviderQueryFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // The same predicate as a page, so a nearest-first search only reorders what an ordinary
        // search returns. Four columns and no graph; the handler reads cards for one page after.
        return await Apply(Providers.AsNoTracking(), filter)
            .OrderBy(provider => provider.Slug.Value)
            .Take(Math.Max(1, limit))
            .Select(provider => new ProviderNearestCandidate(
                provider.Id,
                provider.Slug.Value,
                provider.Location.Latitude,
                provider.Location.Longitude))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        ProviderQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Providers.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        ProviderSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // IgnoreQueryFilters, because a soft-deleted provider still holds its slug: reusing it
        // would break every link to the deleted one if it is ever restored, and would make the
        // unique index refuse the insert anyway.
        return await Providers
            .IgnoreQueryFilters()
            .AnyAsync(provider => provider.Slug.Value == value, cancellationToken);
    }

    public async Task<int> CountOwnedByAsync(
        UserId ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        return await Providers
            .AsNoTracking()
            .CountAsync(
                provider => provider.Members.Any(member =>
                    member.UserId == ownerId && member.Role == ProviderRole.Owner),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Provider>> ListForMemberAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await CardGraph(Providers.AsNoTracking())
            .Where(provider => provider.Members.Any(member => member.UserId == userId))
            .OrderByDescending(provider => provider.CreatedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProviderRole>> GetRolesForAsync(
        UserId userId,
        IReadOnlyCollection<ProviderId> providerIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, ProviderRole>();
        }

        var keys = providerIds.ToList();

        // Loaded through an Include and reduced in memory rather than projected in SQL. A
        // SelectMany over an owned collection is not a shape EF translates, and the failure is a
        // run-time exception inside a page render rather than a compile error. The set is one
        // page of providers, so the cost is a handful of rows either way.
        var rows = await Providers
            .AsNoTracking()
            .Where(provider => keys.Contains(provider.Id))
            .Include(provider => provider.Members)
            .Select(provider => new { provider.Id, provider.Members })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new
            {
                row.Id,
                Membership = row.Members.FirstOrDefault(member => member.UserId == userId)
            })
            .Where(row => row.Membership is not null)
            .ToDictionary(row => row.Id.Value, row => row.Membership!.Role);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetOwnersAsync(
        IReadOnlyCollection<Guid> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var keys = providerIds.Select(ProviderId.From).ToList();

        var rows = await Providers
            .AsNoTracking()
            .Where(provider => keys.Contains(provider.Id))
            .Include(provider => provider.Members)
            .Select(provider => new { provider.Id, provider.Members })
            .ToListAsync(cancellationToken);

        // The earliest-joined owner, matching Provider.PrimaryOwnerId. Reduced in memory because
        // the alternative — a correlated subquery per row — is the shape that makes a twenty-card
        // page issue twenty queries.
        return rows
            .Select(row => new
            {
                row.Id,
                Owner = row.Members
                    .Where(member => member.Role == ProviderRole.Owner)
                    .OrderBy(member => member.JoinedAt)
                    .FirstOrDefault()
            })
            .Where(row => row.Owner is not null)
            .ToDictionary(row => row.Id.Value, row => row.Owner!.UserId.Value);
    }

    public async Task<int> CountInAreaAsync(
        string hierarchyPathPrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hierarchyPathPrefix))
        {
            return 0;
        }

        return await Providers
            .AsNoTracking()
            .CountAsync(
                provider => provider.Status == ProviderStatus.Active
                    && provider.Location.HierarchyPath != null
                    && provider.Location.HierarchyPath.StartsWith(hierarchyPathPrefix),
                cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return 0;
        }

        var normalized = countryCode.Trim().ToUpperInvariant();

        return await Providers
            .AsNoTracking()
            .CountAsync(
                provider => provider.Status == ProviderStatus.Active
                    && provider.Location.CountryCode == normalized,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await Providers
            .AsNoTracking()
            .Where(provider => provider.Status == ProviderStatus.Active)
            .GroupBy(provider => provider.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.CategoryId.Value, row => row.Count);
    }

    public async Task<IReadOnlyList<Provider>> ListWithLapsedVerificationsAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await FullGraph(Providers)
            .Where(provider =>
                provider.VerificationSummary.IsVerified
                && provider.VerificationSummary.ExpiresAt != null
                && provider.VerificationSummary.ExpiresAt <= asOf)
            .OrderBy(provider => provider.VerificationSummary.ExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Provider provider, CancellationToken cancellationToken = default) =>
        await Providers.AddAsync(provider, cancellationToken);

    public void Remove(Provider provider) => Providers.Remove(provider);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Graph shapes
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Everything. For commands, which need an aggregate that can check its invariants.</summary>
    private static IQueryable<Provider> FullGraph(IQueryable<Provider> query) =>
        query
            .Include(provider => provider.Members)
            .Include(provider => provider.Services)
                .ThenInclude(service => service.Offerings)
            .Include(provider => provider.ServiceAreas)
            .Include(provider => provider.Locations)
            .Include(provider => provider.Languages)
            .Include(provider => provider.ContactMethods)
            .Include(provider => provider.Gallery)
            .Include(provider => provider.Certifications)
            .Include(provider => provider.Tags)
            .Include(provider => provider.OperatingHours)
                .ThenInclude(hours => hours.Windows)
            .Include(provider => provider.AvailabilityExceptions)
            .Include(provider => provider.Verifications);

    /// <summary>
    /// What the client's provider detail screen renders. Everything except branches, which the
    /// screen does not show, and which a provider with twenty-five of them would make expensive.
    /// </summary>
    private static IQueryable<Provider> ReadingGraph(IQueryable<Provider> query) =>
        query
            .Include(provider => provider.Members)
            .Include(provider => provider.Services)
                .ThenInclude(service => service.Offerings)
            .Include(provider => provider.ServiceAreas)
            .Include(provider => provider.Languages)
            .Include(provider => provider.ContactMethods)
            .Include(provider => provider.Gallery)
            .Include(provider => provider.Certifications)
            .Include(provider => provider.Tags)
            .Include(provider => provider.OperatingHours)
                .ThenInclude(hours => hours.Windows)
            .Include(provider => provider.AvailabilityExceptions)
            .Include(provider => provider.Verifications);

    /// <summary>
    /// What a discovery card needs: tags for chips, hours for the open-now indicator, contact
    /// methods for the call button. Not services, not the gallery, not certifications — twenty
    /// cards' worth of those is several thousand rows nobody renders.
    /// </summary>
    private static IQueryable<Provider> CardGraph(IQueryable<Provider> query) =>
        query
            .Include(provider => provider.Tags)
            .Include(provider => provider.ContactMethods)
            .Include(provider => provider.OperatingHours)
                .ThenInclude(hours => hours.Windows)
            .Include(provider => provider.AvailabilityExceptions);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Filtering
    // ════════════════════════════════════════════════════════════════════════════════════════

    private static IQueryable<Provider> Apply(IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        query = ApplyLifecycle(query, filter);
        query = ApplyClassification(query, filter);
        query = ApplyQuality(query, filter);
        query = ApplyGeography(query, filter);
        query = ApplyAudience(query, filter);

        return query;
    }

    private static IQueryable<Provider> ApplyLifecycle(
        IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(provider => statuses.Contains(provider.Status));
        }

        if (filter.ProviderIds is { Count: > 0 })
        {
            var ids = filter.ProviderIds.ToList();
            query = query.Where(provider => ids.Contains(provider.Id));
        }

        if (filter.FeaturedOnly)
        {
            query = query.Where(provider => provider.IsFeatured);
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
            + "case-insensitive on Postgres, where LIKE is not.")]
    private static IQueryable<Provider> ApplyClassification(
        IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        if (filter.QueryWords is { Count: > 0 } words)
        {
            // Any meaningful word of the search, lower-cased on both sides (PostgreSQL's LIKE is
            // case-sensitive), in the name, the summary or a tag, or a category the search names.
            // Matching the summary rather than the whole body keeps the scan off an
            // eight-thousand-character column on every row.
            var terms = words
                .Select(word => (Expression<Func<Provider, bool>>)(provider =>
                    provider.Name.Value.ToLower().Contains(word)
                    || provider.Profile.Description.Summary.ToLower().Contains(word)
                    || provider.Tags.Any(tag => tag.Label.ToLower().Contains(word))))
                .ToList();

            if (filter.QueryCategoryIds is { Count: > 0 } named)
            {
                var namedIds = named.ToList();
                terms.Add(provider => namedIds.Contains(provider.CategoryId));
            }

            query = query.Where(SearchWords.AnyOf(terms)!);
        }

        if (filter.ProviderTypes is { Count: > 0 })
        {
            var types = filter.ProviderTypes.ToList();
            query = query.Where(provider => types.Contains(provider.Type));
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(provider => provider.CategoryId == categoryId);
        }

        if (filter.CategoryIds is { Count: > 0 } subtree)
        {
            var subtreeIds = subtree.ToList();
            query = query.Where(provider => subtreeIds.Contains(provider.CategoryId));
        }

        if (filter.TagSlugs is { Count: > 0 })
        {
            var slugs = filter.TagSlugs.ToList();
            query = query.Where(provider => provider.Tags.Any(tag => slugs.Contains(tag.Slug)));
        }

        if (filter.LanguageCodes is { Count: > 0 })
        {
            var codes = filter.LanguageCodes.ToList();

            query = query.Where(provider =>
                provider.Languages.Any(language => codes.Contains(language.Code)));
        }

        if (filter.DeliveryModes is { Count: > 0 })
        {
            var modes = filter.DeliveryModes.ToList();
            query = query.Where(provider => modes.Contains(provider.DeliveryMode));
        }

        return query;
    }

    private static IQueryable<Provider> ApplyQuality(
        IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        if (filter.VerifiedOnly)
        {
            query = query.Where(provider => provider.VerificationSummary.IsVerified);
        }

        if (filter.CertifiedOnly)
        {
            query = query.Where(provider =>
                provider.Certifications.Any(certification =>
                    certification.Status == CertificationStatus.Verified));
        }

        if (filter.MinimumRating is { } minimumRating)
        {
            query = query.Where(provider => provider.ReviewSummary.Average >= minimumRating);
        }

        if (filter.MinimumReviewCount is { } minimumReviews)
        {
            query = query.Where(provider => provider.ReviewSummary.TotalReviews >= minimumReviews);
        }

        return query;
    }

    /// <summary>
    /// Narrows by geography.
    /// <para>
    /// <b>The remote clause is the subtle part.</b> A provider that serves remotely has no
    /// meaningful location, and excluding it from a city or radius filter is a bug rather than
    /// the filter working — which is why every geographic predicate here is written as "matches
    /// the scope <em>or</em> serves remotely" when the caller asked for that, rather than as a
    /// plain equality.
    /// </para>
    /// </summary>
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
            + "case-insensitive on Postgres, where = is not.")]
    private static IQueryable<Provider> ApplyGeography(
        IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        var includeRemote = filter.IncludeRemoteProviders;
        var matchAreas = filter.MatchServiceAreas;

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();

            query = query.Where(provider =>
                provider.Location.CountryCode == country
                || (matchAreas && provider.ServiceAreas.Any(area => area.CountryCode == country))
                || (includeRemote && (provider.DeliveryMode == ServiceDeliveryMode.Remote
                    || provider.ServiceAreas.Any(area => area.Kind == ServiceAreaKind.Remote))));
        }

        if (!string.IsNullOrWhiteSpace(filter.HierarchyPathPrefix))
        {
            var prefix = filter.HierarchyPathPrefix;

            // Containment runs in two directions here, and only one is a prefix match.
            //
            //   "provider is inside London"  → provider.path LIKE '/gb/london/%'
            //   "service area covers Peckham" → area.path IN (every ancestor of the request)
            //
            // The second cannot be written as prefix.StartsWith(column): EF Core does not
            // translate a parameter-receiver StartsWith, and the query would compile, read
            // correctly, and throw the first time anybody ran it. See
            // ProviderQueryFilter.AncestorHierarchyPaths.
            var ancestors = filter.AncestorHierarchyPaths?.ToList() ?? [prefix];

            query = query.Where(provider =>
                (provider.Location.HierarchyPath != null
                    && provider.Location.HierarchyPath.StartsWith(prefix))
                || (matchAreas && provider.ServiceAreas.Any(area =>
                    area.HierarchyPath != null && ancestors.Contains(area.HierarchyPath)))
                || (includeRemote && (provider.DeliveryMode == ServiceDeliveryMode.Remote
                    || provider.ServiceAreas.Any(area => area.Kind == ServiceAreaKind.Remote))));
        }
        else if (filter.AreaId is { } areaId)
        {
            query = query.Where(provider =>
                provider.Location.AreaId == areaId
                || (matchAreas && provider.ServiceAreas.Any(area => area.AreaId == areaId))
                || (includeRemote && provider.DeliveryMode == ServiceDeliveryMode.Remote));
        }
        else if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides: Postgres compares text case-sensitively and SQLite does not,
            // so "mombasa" found "Mombasa" in every test and nothing in production.
            var city = filter.CityName.Trim().ToLowerInvariant();

            // A provider that serves the town by name counts as being there (14 Oct 2026).
            query = query.Where(provider =>
                (provider.Location.CityName != null && provider.Location.CityName.ToLower() == city)
                || (matchAreas && provider.ServiceAreas.Any(area =>
                    area.Kind == ServiceAreaKind.Town
                    && area.CityName != null
                    && area.CityName.ToLower() == city))
                || (includeRemote && provider.DeliveryMode == ServiceDeliveryMode.Remote));
        }

        if (filter.HasBoundingBox)
        {
            var minLatitude = filter.MinLatitude!.Value;
            var maxLatitude = filter.MaxLatitude!.Value;
            var minLongitude = filter.MinLongitude!.Value;
            var maxLongitude = filter.MaxLongitude!.Value;

            // A provider with no coordinates that names the town the request named (14 Oct 2026):
            // a circle is answered by coordinates, and most providers predate the pin.
            var unpinned = string.IsNullOrWhiteSpace(filter.UnpinnedCityName)
                ? null
                : filter.UnpinnedCityName.Trim().ToLowerInvariant();
            var keepUnpinned = unpinned is not null;

            // Two branches rather than one predicate carrying a captured bool, so each query
            // shape is a plain pair of range comparisons the planner can still use an index for.
            // The wrapped branch is an OR of two half-open ranges — the same split
            // BoundingBox.Split() performs, expressed inline because there is only one column.
            query = filter.BoundingBoxCrossesAntimeridian
                ? query.Where(provider =>
                    (provider.Location.Latitude != null
                        && provider.Location.Longitude != null
                        && provider.Location.Latitude >= minLatitude
                        && provider.Location.Latitude <= maxLatitude
                        && (provider.Location.Longitude >= minLongitude
                            || provider.Location.Longitude <= maxLongitude))
                    || (keepUnpinned
                        && provider.Location.Latitude == null
                        && provider.Location.CityName != null
                        && provider.Location.CityName.ToLower() == unpinned)
                    || (includeRemote && provider.DeliveryMode == ServiceDeliveryMode.Remote))
                : query.Where(provider =>
                    (provider.Location.Latitude != null
                        && provider.Location.Longitude != null
                        && provider.Location.Latitude >= minLatitude
                        && provider.Location.Latitude <= maxLatitude
                        && provider.Location.Longitude >= minLongitude
                        && provider.Location.Longitude <= maxLongitude)
                    || (keepUnpinned
                        && provider.Location.Latitude == null
                        && provider.Location.CityName != null
                        && provider.Location.CityName.ToLower() == unpinned)
                    || (includeRemote && provider.DeliveryMode == ServiceDeliveryMode.Remote));
        }

        return query;
    }

    private static IQueryable<Provider> ApplyAudience(
        IQueryable<Provider> query, ProviderQueryFilter filter)
    {
        if (filter.AllowedVisibility is { Count: > 0 })
        {
            var levels = filter.AllowedVisibility.ToList();
            query = query.Where(provider => levels.Contains(provider.Visibility.Level));
        }

        if (filter.OwnedByViewerOnly && filter.ViewerId is { } viewerId)
        {
            query = query.Where(provider =>
                provider.Members.Any(member => member.UserId == viewerId));
        }

        if (filter.ExcludedOwnerIds is { Count: > 0 })
        {
            var excluded = filter.ExcludedOwnerIds.Select(UserId.From).ToList();

            query = query.Where(provider =>
                !provider.Members.Any(member =>
                    member.Role == ProviderRole.Owner && excluded.Contains(member.UserId)));
        }

        return query;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Ordering
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orders the page and applies the cursor.
    /// <para>
    /// <b>Every order ends with the slug, not the identifier.</b> A total order needs a unique
    /// tie-break, and the obvious candidate — the primary key — is a value-converted
    /// <c>ProviderId</c>: EF cannot translate a comparison on it, and reaching through to
    /// <c>.Value</c> makes the whole query untranslatable at run time rather than at compile
    /// time. The slug is unique, indexed and a string, so it compares in SQL on every provider.
    /// </para>
    /// <para>
    /// Without a tie-break, two providers published in the same tick order arbitrarily between
    /// pages and a member sees one twice and misses the other — the exact bug cursor paging
    /// exists to avoid.
    /// </para>
    /// <para>
    /// <see cref="ProviderSortOrder.Recommended"/> and <see cref="ProviderSortOrder.Distance"/>
    /// are ordered here only as a stable base: the registered ranking strategy reorders the page
    /// in memory afterwards, and distances come from Location. Sorting them in SQL would require
    /// B7 to compute a score or a haversine in the database, which are the two things it is not
    /// allowed to own.
    /// </para>
    /// </summary>
    private static IQueryable<Provider> ApplyOrderAndCursor(
        IQueryable<Provider> query, ProviderQueryFilter filter, ProviderCursor? cursor)
    {
        var lastSlug = cursor?.LastSlug;

        switch (filter.Sort)
        {
            case ProviderSortOrder.Rating:
            {
                if (cursor?.AsNumber() is { } afterRating && lastSlug is not null)
                {
#pragma warning disable CA1309 // The ordinal overload is untranslatable. See the class summary.
                    query = query.Where(provider =>
                        provider.ReviewSummary.Average < afterRating
                        || (provider.ReviewSummary.Average == afterRating
                            && string.Compare(provider.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(provider => provider.ReviewSummary.Average)
                    .ThenBy(provider => provider.Slug.Value);
            }

            case ProviderSortOrder.MostReviewed:
            {
                if (cursor?.AsNumber() is { } afterCount && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(provider =>
                        provider.ReviewSummary.TotalReviews < afterCount
                        || (provider.ReviewSummary.TotalReviews == afterCount
                            && string.Compare(provider.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(provider => provider.ReviewSummary.TotalReviews)
                    .ThenBy(provider => provider.Slug.Value);
            }

            case ProviderSortOrder.Name:
            {
                var afterName = cursor?.SortKey;

                if (afterName is not null && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(provider =>
                        string.Compare(provider.Name.Value, afterName) > 0
                        || (provider.Name.Value == afterName
                            && string.Compare(provider.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderBy(provider => provider.Name.Value)
                    .ThenBy(provider => provider.Slug.Value);
            }

            default:
            {
                // Newest, Recommended and Distance all page on publication. The first because
                // that is the sort; the other two because they need a stable, indexed base order
                // that the in-memory ranker then rearranges.
                if (cursor?.AsTimestamp() is { } after && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(provider =>
                        provider.PublishedAt < after
                        || (provider.PublishedAt == after
                            && string.Compare(provider.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(provider => provider.PublishedAt)
                    .ThenBy(provider => provider.Slug.Value);
            }
        }
    }

    private static ProviderCursor BuildCursor(Provider provider, ProviderSortOrder sort) => sort switch
    {
        ProviderSortOrder.Rating =>
            ProviderCursor.FromNumber(provider.ReviewSummary.Average, provider.Slug.Value),

        ProviderSortOrder.MostReviewed =>
            ProviderCursor.FromNumber(provider.ReviewSummary.TotalReviews, provider.Slug.Value),

        ProviderSortOrder.Name =>
            ProviderCursor.Create(provider.Name.Value, provider.Slug.Value),

        _ => ProviderCursor.FromTimestamp(
            provider.PublishedAt ?? provider.CreatedAt, provider.Slug.Value)
    };
}
