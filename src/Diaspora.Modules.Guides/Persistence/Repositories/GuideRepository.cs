using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for guides.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded guide cannot validate a structure replacement against sections it never
/// loaded. <see cref="GetForReadAsync"/> and <see cref="GetBySlugAsync"/> load the same graph
/// untracked, because the detail screen renders all of it. <see cref="QueryAsync"/> loads the
/// <em>card</em> graph: tags for chips and media references for the hero image — a page of
/// twenty cards that eagerly loaded twenty full documents would be several thousand rows to
/// render twenty titles.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and
/// owned collections only when asked — exactly the trap that makes a card query fast and a
/// command silently operate on a guide with no sections in it.
/// </para>
/// <para>
/// <b>Visibility is applied here, from the filter's audience fields, and nowhere else.</b>
/// Drafts appear only to their author and staff; unlisted and private guides likewise; hidden
/// guides only to staff. Soft-deleted rows never appear at all — the shared context's global
/// filter owns that predicate.
/// </para>
/// </summary>
public sealed class GuideRepository : IGuideRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Guide> Guides => _dbContext.Set<Guide>();

    public async Task<Guide?> GetAsync(
        GuideId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Guides)
            .FirstOrDefaultAsync(guide => guide.Id == id, cancellationToken);

    public async Task<Guide?> GetForReadAsync(
        GuideId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Guides.AsNoTracking())
            .FirstOrDefaultAsync(guide => guide.Id == id, cancellationToken);

    public async Task<Guide?> GetIncludingDeletedAsync(
        GuideId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Guides.IgnoreQueryFilters())
            .FirstOrDefaultAsync(guide => guide.Id == id, cancellationToken);

    public async Task<Guide?> GetBySlugAsync(
        GuideSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Guides.AsNoTracking())
            .FirstOrDefaultAsync(guide => guide.Slug.Value == value, cancellationToken);
    }

    public async Task<CursorPagedResult<Guide>> QueryAsync(
        GuideQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = CardGraph(Apply(Guides.AsNoTracking(), filter));

        query = ApplyOrderAndCursor(query, filter, GuideCursor.Parse(cursor));

        var effectiveLimit = Math.Clamp(limit, 1, GuideLimits.MaxPageSize);

        // One more than asked for, so "is there a next page" is answered by the same query
        // rather than by a second count.
        var items = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > effectiveLimit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore && items.Count > 0
            ? BuildCursor(items[^1], filter.Sort).Encode()
            : null;

        return CursorPagedResult<Guide>.Create(items, nextCursor, hasMore);
    }

    /// <summary>
    /// Counts through exactly the predicates the listing applies, and nothing else.
    /// <para>
    /// No ordering, no cursor, no graph. A count that included the card graph would join the tag
    /// and media tables to produce a number that does not depend on either — which is how a
    /// headline figure on the most-read screen becomes the most expensive query on the page.
    /// </para>
    /// </summary>
    public async Task<int> CountAsync(
        GuideQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Guides.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        GuideSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // IgnoreQueryFilters, because a soft-deleted guide still holds its slug: reusing it
        // would break every link to the deleted one if it is ever restored, and would make the
        // unique index refuse the insert anyway.
        return await Guides
            .IgnoreQueryFilters()
            .AnyAsync(guide => guide.Slug.Value == value, cancellationToken);
    }

    public async Task<int> CountByAuthorAsync(
        UserId authorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return await Guides
            .AsNoTracking()
            .CountAsync(guide => guide.AuthorId == authorId, cancellationToken);
    }

    public async Task<Guide?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        // The full graph and tracked: the caller is about to apply a decision to the aggregate.
        // The trust mark is an owned reference, so its columns sit on the guide row and this is
        // an ordinary predicate rather than a join.
        await FullGraph(Guides)
            .FirstOrDefaultAsync(
                guide => guide.TrustMark.VerificationId == verificationId, cancellationToken);

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        var normalized = countryCode.Trim().ToUpperInvariant();

        // Published only. A country page counting drafts would advertise knowledge nobody can
        // read, and the number would move when an author saved a file.
        return await Guides
            .AsNoTracking()
            .CountAsync(
                guide => guide.Status == GuideStatus.Published
                    && guide.Location.CountryCode == normalized,
                cancellationToken);
    }

    public async Task<int> CountInAreaAsync(
        string areaPathPrefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaPathPrefix);

        return await Guides
            .AsNoTracking()
            .CountAsync(
                guide => guide.Status == GuideStatus.Published
                    && guide.Location.AreaPath != null
                    && guide.Location.AreaPath.StartsWith(areaPathPrefix),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Guide>> GetScheduledDueAsync(
        DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        // Tracked and full, because the scheduled publisher is about to call Publish on each.
        // Batch-bounded so a backlog after downtime is worked through rather than swallowed.
        await FullGraph(Guides)
            .Where(guide =>
                guide.Status == GuideStatus.Draft
                && guide.ScheduledPublishAt != null
                && guide.ScheduledPublishAt <= now)
            .OrderBy(guide => guide.ScheduledPublishAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Guide guide, CancellationToken cancellationToken = default) =>
        await Guides.AddAsync(guide, cancellationToken);

    public void Remove(Guide guide) => Guides.Remove(guide);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Graph shapes
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Everything the guide owns. For commands, which need an aggregate that can check its
    /// invariants, and for the detail screen, which renders all of it.
    /// </summary>
    private static IQueryable<Guide> FullGraph(IQueryable<Guide> query) =>
        query
            .Include(guide => guide.Sections)
                .ThenInclude(section => section.Blocks)
            .Include(guide => guide.Tags)
            .Include(guide => guide.MediaReferences)
            .Include(guide => guide.ProviderReferences)
            .Include(guide => guide.ModerationNotes);

    /// <summary>
    /// What a discovery card needs: tags for chips, media references for the hero image. Not
    /// the sections — twenty cards' worth of full documents is several thousand rows nobody
    /// renders.
    /// </summary>
    private static IQueryable<Guide> CardGraph(IQueryable<Guide> query) =>
        query
            .Include(guide => guide.Tags)
            .Include(guide => guide.MediaReferences);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Filtering
    // ════════════════════════════════════════════════════════════════════════════════════════

    private static IQueryable<Guide> Apply(IQueryable<Guide> query, GuideQueryFilter filter)
    {
        query = ApplyAudience(query, filter);
        query = ApplyLifecycle(query, filter);
        query = ApplyClassification(query, filter);

        return query;
    }

    /// <summary>
    /// Turns the pre-computed audience into predicates. Drafts, unlisted and private guides
    /// belong to their author; hidden guides belong to staff; nobody else composes their own
    /// visibility — the filter documents that promise and this method keeps it.
    /// </summary>
    private static IQueryable<Guide> ApplyAudience(
        IQueryable<Guide> query, GuideQueryFilter filter)
    {
        if (filter.ViewerIsStaff)
        {
            return query;
        }

        query = query.Where(guide => guide.ModerationState != GuideModerationState.Hidden);

        if (filter.ViewerId is { } viewer)
        {
            return query.Where(guide =>
                guide.AuthorId == viewer
                || (guide.Status != GuideStatus.Draft
                    && guide.Visibility == GuideVisibility.Public));
        }

        return query.Where(guide =>
            guide.Status != GuideStatus.Draft && guide.Visibility == GuideVisibility.Public);
    }

    private static IQueryable<Guide> ApplyLifecycle(
        IQueryable<Guide> query, GuideQueryFilter filter)
    {
        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(guide => statuses.Contains(guide.Status));
        }

        if (filter.FeaturedOnly)
        {
            query = query.Where(guide => guide.IsFeatured);
        }

        if (filter.VerifiedOnly)
        {
            query = query.Where(guide =>
                guide.TrustMark.State == GuideVerificationState.Verified);
        }

        // B8.1's "official" shelf. Narrower than verified: the badge is what Trust grants when the
        // source is an authority rather than merely a checked contributor. Both columns are owned
        // and sit on the guide row, so this is an ordinary predicate rather than a join.
        if (filter.OfficialOnly)
        {
            query = query.Where(guide =>
                guide.TrustMark.State == GuideVerificationState.Verified
                && guide.TrustMark.Badge != null);
        }

        if (filter.Difficulty is { } difficulty)
        {
            query = query.Where(guide => guide.Reading.Difficulty == difficulty);
        }

        if (filter.MaxReadingTimeMinutes is { } maxReadingTime)
        {
            query = query.Where(guide => guide.Reading.ReadingTimeMinutes <= maxReadingTime);
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
            + "case-insensitive on Postgres, where = is not.")]
    private static IQueryable<Guide> ApplyClassification(
        IQueryable<Guide> query, GuideQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // Naive substring matching, and deliberately so — the search seam owns real
            // search. Matching the summary rather than the body keeps the scan off the
            // section tables on every row.
            var term = filter.Text.Trim();

            query = query.Where(guide =>
                guide.Title.Value.Contains(term)
                || guide.Summary!.Summary!.Contains(term)
                || guide.Tags.Any(tag => tag.Slug.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(guide => guide.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides (14 Oct 2026): Postgres compares text case-sensitively and the
            // SQLite tests do not.
            var city = filter.CityName.Trim().ToLowerInvariant();
            query = query.Where(guide =>
                guide.Location.CityName != null && guide.Location.CityName.ToLower() == city);
        }

        if (filter.HasBoundingBox)
        {
            var south = filter.MinLatitude!.Value;
            var north = filter.MaxLatitude!.Value;
            var west = filter.MinLongitude!.Value;
            var east = filter.MaxLongitude!.Value;

            var unpinned = string.IsNullOrWhiteSpace(filter.UnpinnedCityName)
                ? null
                : filter.UnpinnedCityName.Trim().ToLowerInvariant();
            var keepUnpinned = unpinned is not null;

            // Every unpinned guide of the point's country: national content has no pin.
            var unpinnedCountry = string.IsNullOrWhiteSpace(filter.UnpinnedCountryCode)
                ? null
                : filter.UnpinnedCountryCode.Trim().ToUpperInvariant();
            var keepCountry = unpinnedCountry is not null;

            query = filter.BoundingBoxCrossesAntimeridian
                ? query.Where(guide =>
                    (guide.Latitude != null
                        && guide.Longitude != null
                        && guide.Latitude >= south
                        && guide.Latitude <= north
                        && (guide.Longitude >= west || guide.Longitude <= east))
                    || (keepUnpinned
                        && guide.Latitude == null
                        && guide.Location.CityName != null
                        && guide.Location.CityName.ToLower() == unpinned)
                    || (keepCountry
                        && guide.Latitude == null
                        && guide.Location.CountryCode == unpinnedCountry))
                : query.Where(guide =>
                    (guide.Latitude != null
                        && guide.Longitude != null
                        && guide.Latitude >= south
                        && guide.Latitude <= north
                        && guide.Longitude >= west
                        && guide.Longitude <= east)
                    || (keepUnpinned
                        && guide.Latitude == null
                        && guide.Location.CityName != null
                        && guide.Location.CityName.ToLower() == unpinned)
                    || (keepCountry
                        && guide.Latitude == null
                        && guide.Location.CountryCode == unpinnedCountry));
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            var categoryKey = filter.CategoryKey;
            query = query.Where(guide => guide.CategoryKey == categoryKey);
        }

        // B8.1's directory rows draw from a set of filing categories rather than one. Contains over
        // a materialised list translates to an IN clause on every provider.
        if (filter.CategoryKeys.Count > 0)
        {
            var categoryKeys = filter.CategoryKeys.ToList();
            query = query.Where(guide => categoryKeys.Contains(guide.CategoryKey));
        }

        // Restricting to a known set — a curated shelf, or a search engine's hits. Compared as
        // GuideId rather than Guid: Contains over a value-converted property translates, and
        // reaching through to .Value would make the whole query untranslatable at run time.
        if (filter.GuideIds.Count > 0)
        {
            var guideIds = filter.GuideIds.Select(GuideId.From).ToList();
            query = query.Where(guide => guideIds.Contains(guide.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag;
            query = query.Where(guide => guide.Tags.Any(assigned => assigned.Slug == tag));
        }

        if (!string.IsNullOrWhiteSpace(filter.Language))
        {
            var language = filter.Language;
            query = query.Where(guide => guide.Language.Code == language);
        }

        if (filter.AuthorId is { } authorId)
        {
            // Compared as UserId, not as Guid: EF translates equality over a value-converted
            // property, and reaching through to .Value makes the whole query untranslatable —
            // a run-time failure the compiler cannot see. B4's rule.
            query = query.Where(guide => guide.AuthorId == authorId);
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
    /// <c>GuideId</c>: EF cannot translate a comparison on it. The slug is unique, indexed and
    /// a string, so it compares in SQL on every guide. Without the tie-break, two guides
    /// published in the same tick order arbitrarily between pages and a member sees one twice
    /// and misses the other — the exact bug cursor paging exists to avoid.
    /// </para>
    /// <para>
    /// <see cref="GuideSortOrder.Trending"/> is ordered here only as a stable base: the
    /// registered ranking strategy reorders the page in memory afterwards. Sorting it in SQL
    /// would require the repository to compute a decayed score in the database, which is the
    /// one thing the ranking seam exists to keep out of here.
    /// </para>
    /// </summary>
    private static IQueryable<Guide> ApplyOrderAndCursor(
        IQueryable<Guide> query, GuideQueryFilter filter, GuideCursor? cursor)
    {
        var lastSlug = cursor?.LastKey;

        switch (filter.Sort)
        {
            case GuideSortOrder.MostHelpful:
            {
                if (cursor?.AsNumber() is { } afterCount && lastSlug is not null)
                {
#pragma warning disable CA1309 // The ordinal overload is untranslatable. See the class summary.
                    query = query.Where(guide =>
                        guide.Engagement.HelpfulCount < afterCount
                        || (guide.Engagement.HelpfulCount == afterCount
                            && string.Compare(guide.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(guide => guide.Engagement.HelpfulCount)
                    .ThenBy(guide => guide.Slug.Value);
            }

            case GuideSortOrder.Popular:
            {
                if (cursor?.AsNumber() is { } afterViews && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(guide =>
                        guide.Engagement.Views < afterViews
                        || (guide.Engagement.Views == afterViews
                            && string.Compare(guide.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(guide => guide.Engagement.Views)
                    .ThenBy(guide => guide.Slug.Value);
            }

            case GuideSortOrder.RecentlyUpdated:
            {
                // Coalesced to creation, because a guide edited never is still "updated" at
                // some instant — and a nullable order key would sort NULLs differently on
                // SQLite and PostgreSQL.
                if (cursor?.AsTimestamp() is { } afterEdit && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(guide =>
                        (guide.ModifiedAt ?? guide.CreatedAt) < afterEdit
                        || ((guide.ModifiedAt ?? guide.CreatedAt) == afterEdit
                            && string.Compare(guide.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(guide => guide.ModifiedAt ?? guide.CreatedAt)
                    .ThenBy(guide => guide.Slug.Value);
            }

            default:
            {
                // Newest and Trending both page on publication: the first because that is the
                // sort; the second because it needs a stable, indexed base order that the
                // in-memory ranker then rearranges.
                if (cursor?.AsTimestamp() is { } after && lastSlug is not null)
                {
#pragma warning disable CA1309
                    query = query.Where(guide =>
                        guide.PublishedAt < after
                        || (guide.PublishedAt == after
                            && string.Compare(guide.Slug.Value, lastSlug) > 0));
#pragma warning restore CA1309
                }

                return query
                    .OrderByDescending(guide => guide.PublishedAt)
                    .ThenBy(guide => guide.Slug.Value);
            }
        }
    }

    private static GuideCursor BuildCursor(Guide guide, GuideSortOrder sort) => sort switch
    {
        GuideSortOrder.MostHelpful =>
            GuideCursor.FromNumber(guide.Engagement.HelpfulCount, guide.Slug.Value),

        GuideSortOrder.Popular =>
            GuideCursor.FromNumber(guide.Engagement.Views, guide.Slug.Value),

        GuideSortOrder.RecentlyUpdated =>
            GuideCursor.FromTimestamp(guide.ModifiedAt ?? guide.CreatedAt, guide.Slug.Value),

        _ => GuideCursor.FromTimestamp(
            guide.PublishedAt ?? guide.CreatedAt, guide.Slug.Value)
    };
}
