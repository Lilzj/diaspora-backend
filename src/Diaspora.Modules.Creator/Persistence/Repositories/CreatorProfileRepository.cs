using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>
/// Reads and writes the creator projection, and serves the public directory.
/// <para>
/// <b>The visibility rule lives in <see cref="VisibleTo"/> and every public read goes through
/// it.</b> The register's objection to the alternative is the reason: "A client-side privacy filter
/// is one refactor away from a privacy leak." Making the predicate one private method means the
/// search, the facet counts and the leaderboard hydration cannot drift into applying three
/// different rules.
/// </para>
/// </summary>
public sealed class CreatorProfileRepository : ICreatorProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorProfile> Creators => _dbContext.Set<CreatorProfile>();

    public void Add(CreatorProfile creator) => Creators.Add(creator);

    public void Update(CreatorProfile creator) => _dbContext.StageUpdate(creator);

    public async Task<CreatorProfile?> GetAsync(
        CreatorId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Creators
            .Include(creator => creator.Tags)
            .FirstOrDefaultAsync(creator => creator.Id == id, cancellationToken);
    }

    /// <summary>
    /// A member's creator record, staged or saved.
    /// <para>
    /// The change tracker is consulted first for the reason B15's reward repository gives: a
    /// consumer that opens a record and immediately writes against it does both in one unit of work,
    /// and a lookup that only asked the database would open a second one for the unique index to
    /// refuse at save time.
    /// </para>
    /// </summary>
    public async Task<CreatorProfile?> FindByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorProfile>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(creator => creator.UserId == userId);

        if (staged is not null)
        {
            return staged;
        }

        return await Creators
            .Include(creator => creator.Tags)
            .FirstOrDefaultAsync(creator => creator.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorProfile>> GetManyAsync(
        IReadOnlyCollection<CreatorId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var distinct = ids.Distinct().ToList();

        if (distinct.Count == 0)
        {
            return [];
        }

        return await Creators
            .AsNoTracking()
            .Include(creator => creator.Tags)
            .Where(creator => distinct.Contains(creator.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreatorDirectoryPage> SearchAsync(
        CreatorDirectoryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var take = CreatorLimits.PageSize(filter.Take);
        var query = Filtered(filter);

        var cursor = CreatorCursor.Parse(filter.Cursor);
        var boundaryScore = cursor?.AsScore();

        if (boundaryScore is { } score)
        {
            var exclusion = CursorBoundary.Exclusion(cursor!.BoundaryKey, CreatorId.From);

            // Excluding an empty set is not the same as skipping the group — reading IsExhausted is
            // what stops an absurdly wide tie group being served for ever.
            query = exclusion.IsExhausted
                ? query.Where(creator => creator.WeightedContributionScore < score)
                : query.Where(creator =>
                    creator.WeightedContributionScore < score
                    || (creator.WeightedContributionScore == score
                        && !exclusion.Ids.Contains(creator.Id)));
        }

        var rows = await query
            .OrderByDescending(creator => creator.WeightedContributionScore)
            .ThenByDescending(creator => creator.Id)
            .Take(take + 1)
            .Include(creator => creator.Tags)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > take;
        var page = hasMore ? rows.Take(take).ToList() : rows;

        if (!hasMore || page.Count == 0)
        {
            return new CreatorDirectoryPage(page, null, false);
        }

        var boundary = CursorBoundary.Key(
            page,
            creator => creator.WeightedContributionScore,
            creator => creator.Id.Value,
            cursor?.BoundaryKey,
            boundaryScore);

        return new CreatorDirectoryPage(
            page,
            CreatorCursor.FromScore(page[^1].WeightedContributionScore, boundary).Encode(),
            true);
    }

    /// <summary>
    /// Facet counts over the same predicate the search applies.
    /// <para>
    /// <b>The predicate is <see cref="Filtered"/> minus the facet's own dimension</b> — a category
    /// facet counts what would be returned if the category filter were replaced, which is what makes
    /// "Housing (0)" a useful thing to disable rather than a lie. Visibility is never dropped:
    /// the register requires that a private creator does not affect a public count "in a way that
    /// reveals their existence".
    /// </para>
    /// </summary>
    public async Task<CreatorDirectoryFacets> FacetsAsync(
        CreatorDirectoryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var baseQuery = Filtered(filter);

        var total = await baseQuery.CountAsync(cancellationToken);

        var verified = await baseQuery
            .CountAsync(creator => creator.IsVerifiedCreator, cancellationToken);

        var countries = await Filtered(filter with { CountryCode = null })
            .Where(creator => creator.CountryCode != null)
            .GroupBy(creator => creator.CountryCode!)
            .Select(group => new FacetCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var categories = await TagFacetAsync(
            filter with { Category = null }, CreatorTagKind.Category, cancellationToken);

        var expertise = await TagFacetAsync(
            filter with { Expertise = null }, CreatorTagKind.Expertise, cancellationToken);

        var languages = await TagFacetAsync(
            filter with { Language = null }, CreatorTagKind.Language, cancellationToken);

        return new CreatorDirectoryFacets(
            countries.OrderByDescending(facet => facet.Count).ToList(),
            categories,
            expertise,
            languages,
            verified,
            total);
    }

    private async Task<IReadOnlyList<FacetCount>> TagFacetAsync(
        CreatorDirectoryFilter filter, CreatorTagKind kind, CancellationToken cancellationToken)
    {
        var rows = await Filtered(filter)
            .SelectMany(creator => creator.Tags)
            .Where(tag => tag.Kind == kind)
            .GroupBy(tag => tag.NormalisedValue)
            .Select(group => new FacetCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        return rows.OrderByDescending(facet => facet.Count).ThenBy(facet => facet.Value).ToList();
    }

    private IQueryable<CreatorProfile> Filtered(CreatorDirectoryFilter filter)
    {
        var query = VisibleTo(Creators.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();

            query = query.Where(creator =>
                EF.Functions.Like(creator.DisplayName, $"%{term}%")
                || (creator.Bio != null && EF.Functions.Like(creator.Bio, $"%{term}%")));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(creator => creator.CountryCode == country);
        }

        if (filter.Verified is { } verified)
        {
            query = query.Where(creator => creator.IsVerifiedCreator == verified);
        }

        query = WithTag(query, CreatorTagKind.Category, filter.Category);
        query = WithTag(query, CreatorTagKind.Expertise, filter.Expertise);
        query = WithTag(query, CreatorTagKind.Language, filter.Language);

        return query;
    }

    private static IQueryable<CreatorProfile> WithTag(
        IQueryable<CreatorProfile> query, CreatorTagKind kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return query;
        }

        var normalised = value.Trim().ToUpperInvariant();

        return query.Where(creator =>
            creator.Tags.Any(tag => tag.Kind == kind && tag.NormalisedValue == normalised));
    }

    /// <summary>
    /// The one visibility predicate.
    /// <para>
    /// A creator appears publicly only when they have asked to and the platform has not said
    /// otherwise. Suspended and restricted creators are absent rather than returned-and-hidden, and
    /// so is anybody who has never done anything the programme recognises — a directory of empty
    /// profiles is a directory nobody uses.
    /// </para>
    /// </summary>
    private static IQueryable<CreatorProfile> VisibleTo(IQueryable<CreatorProfile> query) =>
        query.Where(creator =>
            creator.IsDiscoverable
            && !creator.IsSuspended
            && !creator.IsRestricted
            && creator.RecognisedActions > 0);
}
