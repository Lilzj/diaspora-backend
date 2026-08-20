using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for organizations.
/// <para>
/// <b>Every read that renders a page is <c>AsNoTracking</c> and every read that precedes a write is
/// tracked.</b> The distinction is not a micro-optimisation: a directory page tracking two hundred
/// aggregates and their owned collections is a change-tracker snapshot nobody will ever save, and it
/// is the difference between a browse screen that scales and one that does not.
/// </para>
/// <para>
/// The owned collections are loaded only where they are rendered. A card needs the name, the place
/// and the counts; the leaders, the opening hours and the languages belong to the detail read, and
/// including them in the page query would multiply every row by the size of its largest collection.
/// </para>
/// </summary>
public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OrganizationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Organization> Organizations => _dbContext.Set<Organization>();

    public async Task<Organization?> GetAsync(
        OrganizationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Organizations.FirstOrDefaultAsync(
            organization => organization.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetIncludingDeletedAsync(
        OrganizationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(organization => organization.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return null;
        }

        // IgnoreQueryFilters, deliberately. A link shared in a family group chat years ago should
        // resolve to "this page has gone" rather than to a 404 that looks like a broken app, and the
        // handler decides which of those the caller is shown.
        return await Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                organization => organization.Slug.Value == normalized, cancellationToken);
    }

    public async Task<CursorPagedResult<Organization>> SearchAsync(
        OrganizationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Organizations.AsNoTracking(), filter);
        var parsed = CommunityLifeCursor.Parse(filter.Cursor);

        query = filter.SortOrder switch
        {
            OrganizationSortOrder.Newest => ApplyPublishedCursor(query, parsed),
            OrganizationSortOrder.MostActive => ApplyActivityCursor(query, parsed),
            OrganizationSortOrder.Name => ApplySlugCursor(query, parsed),
            _ => ApplyCountCursor(query, parsed)
        };

        query = filter.SortOrder switch
        {
            OrganizationSortOrder.Newest => query
                .OrderByDescending(organization => organization.PublishedAt)
                .ThenBy(organization => organization.Slug.Value),
            OrganizationSortOrder.MostActive => query
                .OrderByDescending(organization => organization.LastActivityAt)
                .ThenBy(organization => organization.Slug.Value),
            OrganizationSortOrder.Name => query
                .OrderBy(organization => organization.Name.Value)
                .ThenBy(organization => organization.Slug.Value),

            // Nearest is resolved above this layer — the geographic scope narrows the query and the
            // handler orders what comes back — so it falls through to the size ordering here.
            _ => query
                .OrderByDescending(organization => organization.Statistics.MemberCount)
                .ThenBy(organization => organization.Slug.Value)
        };

        var limit = Math.Clamp(filter.Limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? NextCursor(rows[^1], filter.SortOrder)
            : null;

        return CursorPagedResult<Organization>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<Organization>> FindManyAsync(
        IReadOnlyCollection<OrganizationId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        var wanted = ids.Distinct().ToList();

        return await Organizations
            .AsNoTracking()
            .Where(organization => wanted.Contains(organization.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDuplicateAsync(
        UserId createdByUserId,
        string? name,
        OrganizationId? excluding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createdByUserId);

        var normalized = OrganizationName.Normalize(name);

        if (normalized is null)
        {
            return false;
        }

        return await Organizations
            .AsNoTracking()
            .AnyAsync(
                organization => organization.CreatedByUserId == createdByUserId
                    && organization.Name.Value == normalized
                    && (excluding == null || organization.Id != excluding),
                cancellationToken);
    }

    public async Task<int> CountActiveByCreatorAsync(
        UserId createdByUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createdByUserId);

        return await Organizations
            .AsNoTracking()
            .CountAsync(
                organization => organization.CreatedByUserId == createdByUserId
                    && organization.Status != OrganizationStatus.Archived,
                cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return false;
        }

        // Deleted rows count. A slug is a permanent link, and reusing one would silently redirect
        // every message that carries it.
        return await Organizations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(organization => organization.Slug.Value == normalized, cancellationToken);
    }

    public async Task<int> CountInAreaAsync(
        string areaPathPrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(areaPathPrefix))
        {
            return 0;
        }

        return await Organizations
            .AsNoTracking()
            .CountAsync(
                organization => organization.Location.AreaPath != null
                    && organization.Location.AreaPath.StartsWith(areaPathPrefix)
                    && (organization.Status == OrganizationStatus.Published
                        || organization.Status == OrganizationStatus.Active)
                    && organization.Visibility == OrganizationVisibility.Public,
                cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var normalized = countryCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            return 0;
        }

        return await Organizations
            .AsNoTracking()
            .CountAsync(
                organization => organization.Location.CountryCode == normalized
                    && (organization.Status == OrganizationStatus.Published
                        || organization.Status == OrganizationStatus.Active)
                    && organization.Visibility == OrganizationVisibility.Public,
                cancellationToken);
    }

    public async Task<Organization?> FindByVerificationAsync(
        Guid verificationId, CancellationToken cancellationToken = default)
    {
        if (verificationId == Guid.Empty)
        {
            return null;
        }

        return await Organizations.FirstOrDefaultAsync(
            organization => organization.TrustMark.VerificationId == verificationId,
            cancellationToken);
    }

    public async Task AddAsync(
        Organization organization, CancellationToken cancellationToken = default) =>
        await Organizations.AddAsync(organization, cancellationToken);

    public void Update(Organization organization) => Organizations.Update(organization);

    // ── Query composition ───────────────────────────────────────────────────────────────────

    private static IQueryable<Organization> Apply(
        IQueryable<Organization> query, OrganizationQueryFilter filter)
    {
        if (!filter.IncludeNonPublic)
        {
            query = query.Where(organization =>
                (organization.Status == OrganizationStatus.Published
                    || organization.Status == OrganizationStatus.Active)
                && organization.Visibility != OrganizationVisibility.Private
                && organization.Visibility != OrganizationVisibility.Unlisted
                && organization.ModerationState != CommunityLifeModerationState.Hidden
                && organization.ModerationState != CommunityLifeModerationState.Suspended);
        }
        else if (filter.ViewerUserId is { } viewer)
        {
            // A staff or owner listing still refuses somebody else's drafts. "Include non-public"
            // means "include mine", not "include everybody's".
            query = query.Where(organization => organization.CreatedByUserId == viewer);
        }

        if (filter.TypeKeys is { Count: > 0 })
        {
            var keys = filter.TypeKeys
                .Select(Organization.NormalizeKey)
                .Where(key => key is not null)
                .ToList();

            query = query.Where(organization => keys.Contains(organization.OrganizationTypeKey));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(organization => organization.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(organization => organization.Location.CityName == city);
        }

        if (filter.NeighborhoodAreaId is { } areaId && areaId != Guid.Empty)
        {
            query = query.Where(
                organization => organization.Location.NeighborhoodAreaId == areaId);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            // B4's materialised path stored as text precisely so a subtree is an indexed prefix
            // scan rather than a recursive descent.
            var prefix = filter.AreaPathPrefix;

            if (filter.AreaPathAncestors is { Count: > 0 })
            {
                var ancestors = filter.AreaPathAncestors.ToList();

                query = query.Where(organization =>
                    organization.Location.AreaPath != null
                    && (organization.Location.AreaPath.StartsWith(prefix)
                        || ancestors.Contains(organization.Location.AreaPath)));
            }
            else
            {
                query = query.Where(organization =>
                    organization.Location.AreaPath != null
                    && organization.Location.AreaPath.StartsWith(prefix));
            }
        }

        if (filter.VerifiedOnly == true)
        {
            query = query.Where(organization =>
                organization.TrustMark.State == OrganizationVerificationState.Verified);
        }

        if (filter.FeaturedOnly == true)
        {
            query = query.Where(organization => organization.IsFeatured);
        }

        if (filter.AcceptingMembers == true)
        {
            query = query.Where(organization =>
                organization.MembershipPolicy.Kind != MembershipPolicyKind.Closed
                && organization.MembershipPolicy.Kind != MembershipPolicyKind.InviteOnly);
        }

        if (filter.Languages is { Count: > 0 })
        {
            // "Which churches here hold a service in Twi" — answered from the owned language table
            // rather than from a CSV column, which is why the languages are rows.
            var languages = filter.Languages
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Select(language => language.Trim())
                .ToList();

            query = query.Where(organization =>
                organization.Languages.Any(language => languages.Contains(language.Name)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            // A contains over the name and summary. Deliberately unsophisticated: real relevance is
            // the search engine's job, and this repository is the fallback the EF engine reads.
            var term = filter.SearchTerm.Trim();

            query = query.Where(organization =>
                organization.Name.Value.Contains(term)
                || (organization.Name.ShortName != null
                    && organization.Name.ShortName.Contains(term))
                || organization.Description.Summary.Contains(term));
        }

        return query;
    }

    /// <summary>
    /// The keyset predicate for a publication-ordered page.
    /// <para>
    /// <b>The slug is the tie-break rather than the identifier, and that is not a style choice.</b>
    /// A value-converted <c>StronglyTypedId</c> cannot be compared for ordering in a predicate on
    /// any provider — <c>Guid.CompareTo</c> fails outright on SQLite. The slug is unique, indexed
    /// and a string, so the textbook keyset comparison translates. B7 found this first and B8, B9,
    /// B10 and B11 each repeated it verbatim.
    /// </para>
    /// <para>
    /// Written out per sort rather than built from an expression tree. Two near-identical methods
    /// are duller and readable; a hand-built tree over a nullable projection is where the
    /// strongly-typed-id translation defect keeps reappearing.
    /// </para>
    /// <para>
    /// <b>CA1309 is suppressed across these four methods, and it has to be.</b> The analyser wants
    /// <c>string.Compare(a, b, StringComparison.Ordinal)</c>, and EF cannot translate that overload
    /// — the query would fall back to client evaluation, which for a cursor predicate means loading
    /// the table to find page two. The two-argument overload translates to the database's own
    /// collation, which is what actually orders the rows, so comparing any other way would in fact
    /// be the bug. B8 through B11 carry the same suppression for the same reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<Organization> ApplyPublishedCursor(
        IQueryable<Organization> query, CommunityLifeCursor? cursor)
    {
        if (cursor?.AsTimestamp() is not { } boundary)
        {
            return query;
        }

        var lastSlug = cursor.LastKey;

        return query.Where(organization =>
            organization.PublishedAt < boundary
            || (organization.PublishedAt == boundary
                && string.Compare(organization.Slug.Value, lastSlug) > 0));
    }

    /// <summary>The same keyset over the activity timestamp. See <see cref="ApplyPublishedCursor"/>.</summary>
    private static IQueryable<Organization> ApplyActivityCursor(
        IQueryable<Organization> query, CommunityLifeCursor? cursor)
    {
        if (cursor?.AsTimestamp() is not { } boundary)
        {
            return query;
        }

        var lastSlug = cursor.LastKey;

        return query.Where(organization =>
            organization.LastActivityAt < boundary
            || (organization.LastActivityAt == boundary
                && string.Compare(organization.Slug.Value, lastSlug) > 0));
    }

    private static IQueryable<Organization> ApplySlugCursor(
        IQueryable<Organization> query, CommunityLifeCursor? cursor) =>
        cursor is null
            ? query
            : query.Where(organization =>
                string.Compare(organization.Slug.Value, cursor.LastKey) > 0);

    private static IQueryable<Organization> ApplyCountCursor(
        IQueryable<Organization> query, CommunityLifeCursor? cursor)
    {
        if (cursor?.AsNumber() is not { } boundary)
        {
            return query;
        }

        var count = (int)boundary;
        var lastSlug = cursor.LastKey;

        return query.Where(organization =>
            organization.Statistics.MemberCount < count
            || (organization.Statistics.MemberCount == count
                && string.Compare(organization.Slug.Value, lastSlug) > 0));
    }
#pragma warning restore CA1309

    private static string NextCursor(Organization last, OrganizationSortOrder sortOrder) =>
        sortOrder switch
        {
            OrganizationSortOrder.Newest => CommunityLifeCursor
                .FromTimestamp(last.PublishedAt ?? last.CreatedAt, last.Slug.Value)
                .Encode(),
            OrganizationSortOrder.MostActive => CommunityLifeCursor
                .FromTimestamp(last.LastActivityAt ?? last.CreatedAt, last.Slug.Value)
                .Encode(),
            OrganizationSortOrder.Name => CommunityLifeCursor
                .Create(last.Name.Value, last.Slug.Value)
                .Encode(),
            _ => CommunityLifeCursor
                .FromNumber(last.Statistics.MemberCount, last.Slug.Value)
                .Encode()
        };
}
