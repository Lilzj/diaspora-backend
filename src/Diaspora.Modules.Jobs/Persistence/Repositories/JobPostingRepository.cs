using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Querying;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for job postings.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded posting cannot validate a requirements replacement against lines it never
/// loaded. <see cref="GetForReadAsync"/> loads the same graph untracked, because the detail
/// screen renders all of it. <see cref="QueryAsync"/> loads the <em>card</em> graph: tags for
/// chips and skills for the match hints — a results page of twenty cards that eagerly loaded
/// twenty full adverts would be several thousand rows to render twenty titles.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and
/// owned collections only when asked — exactly the trap that makes a card query fast and a
/// command silently operate on a posting with no requirements in it.
/// </para>
/// <para>
/// <b>Visibility is applied here, from the filter's audience fields, and nowhere else.</b>
/// Drafts and private postings appear only to their own employer and to staff; hidden postings
/// only to staff. Soft-deleted rows never appear at all — the shared context's global filter
/// owns that predicate.
/// </para>
/// </summary>
public sealed class JobPostingRepository : IJobPostingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobPostingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobPosting> Postings => _dbContext.Set<JobPosting>();

    public async Task<JobPosting?> GetAsync(
        JobPostingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Postings)
            .FirstOrDefaultAsync(posting => posting.Id == id, cancellationToken);

    public async Task<JobPosting?> GetForReadAsync(
        JobPostingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Postings.AsNoTracking())
            .FirstOrDefaultAsync(posting => posting.Id == id, cancellationToken);

    public async Task<JobPosting?> GetIncludingDeletedAsync(
        JobPostingId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Postings.IgnoreQueryFilters())
            .FirstOrDefaultAsync(posting => posting.Id == id, cancellationToken);

    public async Task<JobPosting?> GetBySlugAsync(
        JobSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Postings.AsNoTracking())
            .FirstOrDefaultAsync(posting => posting.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<JobPosting>> GetManyAsync(
        IReadOnlyCollection<JobPostingId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Compare whole identifiers, not their inner Guids. `posting.Id.Value` reaches through
        // the strongly-typed id to a member that has no column of its own — the converter maps
        // the id as a unit — so EF cannot translate it and throws at query time rather than
        // compile time. Matching on the id itself lets the converter do the work per element.
        var keys = ids.ToList();

        return await CardGraph(Postings.AsNoTracking())
            .Where(posting => keys.Contains(posting.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<JobPosting?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await FullGraph(Postings)
            .FirstOrDefaultAsync(
                posting => posting.TrustMark.VerificationId == verificationId, cancellationToken);

    public async Task<CursorPagedResult<JobPosting>> QueryAsync(
        JobQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = CardGraph(Apply(Postings.AsNoTracking(), filter));

        query = ApplyOrderAndCursor(query, filter, JobCursor.Parse(cursor));

        var effectiveLimit = Math.Clamp(limit, 1, JobLimits.MaxPageSize);

        // One more than asked for, so "is there a next page" is answered by the same query
        // rather than by a second count over the same predicate.
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0 ? EncodeCursor(rows[^1], filter.Sort) : null;

        return CursorPagedResult<JobPosting>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        JobQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Postings.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        JobSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // Ignores the soft-delete filter: a deleted posting still holds its slug, and handing
        // it to a new posting would break every link ever shared to the old one.
        return await Postings.IgnoreQueryFilters()
            .AnyAsync(posting => posting.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<JobPosting>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default)
    {
        var live = new[]
        {
            JobStatus.Published,
            JobStatus.Open,
            JobStatus.ReceivingApplications,
            JobStatus.Interviewing,
            JobStatus.OfferMade
        };

        return await Postings
            .Where(posting =>
                posting.ClosingAt != null
                && posting.ClosingAt <= asOf
                && live.Contains(posting.Status))
            .OrderBy(posting => posting.ClosingAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobPosting>> GetPublishedSinceAsync(
        DateTimeOffset since, int limit, CancellationToken cancellationToken = default) =>
        await CardGraph(Postings.AsNoTracking())
            .Where(posting =>
                posting.PublishedAt != null
                && posting.PublishedAt > since
                && posting.Visibility == JobVisibility.Public
                && posting.ModerationState != JobModerationState.Hidden
                && (posting.Status == JobStatus.Open
                    || posting.Status == JobStatus.ReceivingApplications))
            .OrderBy(posting => posting.PublishedAt)
            .Take(Math.Clamp(limit, 1, 1_000))
            .ToListAsync(cancellationToken);

    public async Task<int> CountOpenByEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        var open = new[]
        {
            JobStatus.Published,
            JobStatus.Open,
            JobStatus.ReceivingApplications,
            JobStatus.Interviewing,
            JobStatus.OfferMade
        };

        return await Postings
            .CountAsync(
                posting => posting.EmployerId == employerId && open.Contains(posting.Status),
                cancellationToken);
    }

    public async Task AddAsync(JobPosting posting, CancellationToken cancellationToken = default) =>
        await Postings.AddAsync(posting, cancellationToken);

    public void Remove(JobPosting posting) => Postings.Remove(posting);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Graphs
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Everything. For a command that must enforce the aggregate's own invariants.</summary>
    private static IQueryable<JobPosting> FullGraph(IQueryable<JobPosting> query) =>
        query
            .Include(posting => posting.DetailLines)
            .Include(posting => posting.Skills)
            .Include(posting => posting.Tags)
            .Include(posting => posting.MediaReferences)
            .Include(posting => posting.ProviderReferences)
            .Include(posting => posting.GuideReferences)
            .Include(posting => posting.ScreeningQuestions)
            .Include(posting => posting.ModerationNotes);

    /// <summary>
    /// What a card renders: chips and match hints. Deliberately excludes the body, the
    /// bulleted lists, the questions and the moderation trail.
    /// </summary>
    private static IQueryable<JobPosting> CardGraph(IQueryable<JobPosting> query) =>
        query
            .Include(posting => posting.Tags)
            .Include(posting => posting.Skills);

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
            + "case-insensitive on Postgres, where = is not.")]
    private IQueryable<JobPosting> Apply(
        IQueryable<JobPosting> query, JobQueryFilter filter)
    {
        query = ApplyAudience(query, filter);

        if (filter.PostingIds is { Count: > 0 })
        {
            var ids = filter.PostingIds.ToList();
            query = query.Where(posting => ids.Contains(posting.Id));
        }

        if (filter.EmployerId is { } employerId)
        {
            query = query.Where(posting => posting.EmployerId == employerId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(posting => statuses.Contains(posting.Status));
        }

        if (filter.OpenOnly)
        {
            query = query.Where(posting =>
                posting.Status == JobStatus.Open
                || posting.Status == JobStatus.ReceivingApplications
                || posting.Status == JobStatus.Interviewing);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(posting => posting.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides (14 Oct 2026): Postgres compares text case-sensitively and the
            // SQLite tests do not, so "mombasa" missing "Mombasa" could only fail in production.
            var city = filter.CityName.Trim().ToLowerInvariant();
            query = query.Where(posting =>
                posting.Location.CityName != null && posting.Location.CityName.ToLower() == city);
        }

        if (filter.HasBoundingBox)
        {
            var south = filter.MinLatitude!.Value;
            var north = filter.MaxLatitude!.Value;
            var west = filter.MinLongitude!.Value;
            var east = filter.MaxLongitude!.Value;

            // A posting nobody pinned, in the town a circle search named.
            var unpinned = string.IsNullOrWhiteSpace(filter.UnpinnedCityName)
                ? null
                : filter.UnpinnedCityName.Trim().ToLowerInvariant();
            var keepUnpinned = unpinned is not null;

            // Two shapes rather than one predicate, so each stays a plain pair of ranges over the
            // indexed columns; the wrapped one is the ±180° split.
            query = filter.BoundingBoxCrossesAntimeridian
                ? query.Where(posting =>
                    (posting.Latitude != null
                        && posting.Longitude != null
                        && posting.Latitude >= south
                        && posting.Latitude <= north
                        && (posting.Longitude >= west || posting.Longitude <= east))
                    || (keepUnpinned
                        && posting.Latitude == null
                        && posting.Location.CityName != null
                        && posting.Location.CityName.ToLower() == unpinned))
                : query.Where(posting =>
                    (posting.Latitude != null
                        && posting.Longitude != null
                        && posting.Latitude >= south
                        && posting.Latitude <= north
                        && posting.Longitude >= west
                        && posting.Longitude <= east)
                    || (keepUnpinned
                        && posting.Latitude == null
                        && posting.Location.CityName != null
                        && posting.Location.CityName.ToLower() == unpinned));
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            // A plain string prefix match — B4's EF lesson. The materialised path is stored
            // precisely so this is a LIKE 'prefix%' against an indexed column.
            var prefix = filter.AreaPathPrefix;

            if (filter.AreaPathAncestors is { Count: > 0 })
            {
                var ancestors = filter.AreaPathAncestors.ToList();

                query = query.Where(posting =>
                    posting.Location.AreaPath != null
                    && (posting.Location.AreaPath.StartsWith(prefix)
                        || ancestors.Contains(posting.Location.AreaPath)));
            }
            else
            {
                query = query.Where(posting =>
                    posting.Location.AreaPath != null
                    && posting.Location.AreaPath.StartsWith(prefix));
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryKey))
        {
            var category = filter.CategoryKey.Trim().ToLowerInvariant();
            query = query.Where(posting => posting.CategoryKey == category);
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim().ToLowerInvariant();
            query = query.Where(posting => posting.Tags.Any(assignment => assignment.Slug == tag));
        }

        if (filter.SkillKeys is { Count: > 0 })
        {
            // Any rather than all: a candidate searching three skills wants the postings that
            // want one of them, not the vanishingly few that want all three.
            var skills = filter.SkillKeys.ToList();

            query = query.Where(posting =>
                posting.Skills.Any(requirement => skills.Contains(requirement.SkillKey)));
        }

        if (filter.JobTypes is { Count: > 0 })
        {
            var types = filter.JobTypes.ToList();
            query = query.Where(posting => types.Contains(posting.JobType));
        }

        if (filter.WorkplaceTypes is { Count: > 0 })
        {
            var workplaces = filter.WorkplaceTypes.ToList();

            query = query.Where(posting =>
                workplaces.Contains(posting.WorkArrangement.WorkplaceType));
        }

        // Levels are stored by name, so each range is taken over the enum and sent as the levels it
        // admits. `>=` in SQL compared words: "Senior or above" matched Senior alone, because Lead,
        // Principal, Director and Executive all sort before S. See StoredEnum.
        if (filter.MinimumExperience is { } minimumExperience)
        {
            var admitted = StoredEnum.AtLeast(minimumExperience);
            query = query.Where(posting => admitted.Contains(posting.ExperienceLevel));
        }

        if (filter.MaximumExperience is { } maximumExperience)
        {
            var admitted = StoredEnum.AtMost(maximumExperience);
            query = query.Where(posting => admitted.Contains(posting.ExperienceLevel));
        }

        // The education the searcher holds: postings asking for that much or less.
        if (filter.MinimumEducation is { } minimumEducation)
        {
            var admitted = StoredEnum.AtMost(minimumEducation);
            query = query.Where(posting => admitted.Contains(posting.EducationLevel));
        }

        if (filter.VisaSponsorshipOnly)
        {
            query = query.Where(posting => posting.WorkArrangement.OffersVisaSponsorship);
        }

        if (filter.RelocationAssistanceOnly)
        {
            query = query.Where(posting => posting.WorkArrangement.OffersRelocationAssistance);
        }

        if (filter.FeaturedOnly)
        {
            query = query.Where(posting => posting.IsFeatured);
        }

        // Correlated against the employer rather than a denormalised flag on the posting.
        // A badge copied onto the posting at publication would keep saying "verified" after
        // Trust revoked it — which is the platform vouching for exactly the wrong party, and
        // is the single worst thing this filter could get wrong.
        if (filter.VerifiedEmployersOnly)
        {
            var verified = _dbContext.Set<EmployerProfile>()
                .Where(employer =>
                    employer.TrustMark.State == JobVerificationState.Verified
                    && employer.Status == EmployerStatus.Active)
                .Select(employer => employer.Id);

            query = query.Where(posting => verified.Contains(posting.EmployerId));
        }

        if (filter.PublishedAfter is { } publishedAfter)
        {
            query = query.Where(posting => posting.PublishedAt > publishedAfter);
        }

        query = ApplySalary(query, filter);

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // A LIKE-style match over the write model. Honest and adequate until a real engine
            // arrives; IJobSearchEngine is the seam that replaces it without a domain change.
            var term = filter.Text.Trim();

            query = query.Where(posting =>
                EF.Functions.Like(posting.Title.Value, $"%{term}%")
                || EF.Functions.Like(posting.Description.Summary, $"%{term}%")
                || posting.Skills.Any(skill =>
                    EF.Functions.Like(skill.DisplayName, $"%{term}%")));
        }

        return query;
    }

    /// <summary>
    /// The one place drafts, private and hidden postings are let through. Every listing in the
    /// module goes through it, so the rule is written once rather than remembered per caller.
    /// </summary>
    private static IQueryable<JobPosting> ApplyAudience(
        IQueryable<JobPosting> query, JobQueryFilter filter)
    {
        if (filter.IsStaff)
        {
            return query;
        }

        if (filter.IncludeNonPublic && filter.ViewerEmployerId is { } viewerEmployerId)
        {
            // The employer's own dashboard: everything of theirs, nothing of anyone else's
            // that is not already public.
            return query.Where(posting =>
                posting.EmployerId == viewerEmployerId
                || (posting.Visibility == JobVisibility.Public
                    && posting.ModerationState != JobModerationState.Hidden
                    && posting.Status != JobStatus.Draft
                    && posting.Status != JobStatus.Archived));
        }

        return query.Where(posting =>
            posting.Visibility == JobVisibility.Public
            && posting.ModerationState != JobModerationState.Hidden
            && posting.Status != JobStatus.Draft
            && posting.Status != JobStatus.Archived);
    }

    /// <summary>
    /// The salary filter, currency-matched.
    /// <para>
    /// <b>Applied only to postings quoting the same currency, and that is deliberate.</b>
    /// Nothing in this module converts between currencies — a conversion needs a rate, a rate
    /// has a date, and a salary silently restated at yesterday's rate is a number nobody can
    /// act on. A filter that matches nothing is better than one that matches wrongly.
    /// </para>
    /// <para>
    /// Undisclosed salaries are excluded when a minimum is set, because a posting that publishes
    /// no figure cannot be said to meet one.
    /// </para>
    /// </summary>
    private static IQueryable<JobPosting> ApplySalary(
        IQueryable<JobPosting> query, JobQueryFilter filter)
    {
        if (filter.MinimumSalary is not { } minimum
            || string.IsNullOrWhiteSpace(filter.SalaryCurrency))
        {
            return query;
        }

        var currency = filter.SalaryCurrency.Trim().ToUpperInvariant();

        query = query.Where(posting =>
            posting.Salary.IsDisclosed
            && posting.Salary.CurrencyCode == currency
            && ((posting.Salary.Maximum != null && posting.Salary.Maximum >= minimum)
                || (posting.Salary.Maximum == null
                    && posting.Salary.Minimum != null
                    && posting.Salary.Minimum >= minimum)));

        if (filter.SalaryPeriod is { } period)
        {
            query = query.Where(posting => posting.Salary.Period == period);
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
    /// because the primary key is a value-converted identifier EF cannot compare in SQL. Without
    /// a stable tie-break, two postings published in the same millisecond can swap places
    /// between pages and one of them is never shown.
    /// </para>
    /// <para>
    /// <b>CA1309 is suppressed across the whole method, and it has to be.</b> The analyser wants
    /// <c>string.Compare(a, b, StringComparison.Ordinal)</c>, and EF cannot translate that
    /// overload — the query would fall back to client evaluation, which for a cursor predicate
    /// means loading the table to find page two. The two-argument overload translates to the
    /// database's own collation, which is what actually orders the rows, so comparing any other
    /// way would in fact be the bug. B8's repository carries the same suppression for the same
    /// reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<JobPosting> ApplyOrderAndCursor(
        IQueryable<JobPosting> query, JobQueryFilter filter, JobCursor? cursor)
    {
        switch (filter.Sort)
        {
            case JobSortOrder.SalaryHighToLow:
            {
                var after = cursor?.AsNumber();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.Salary.Maximum < (decimal)value
                        || (posting.Salary.Maximum == (decimal)value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(posting => posting.Salary.Maximum)
                    .ThenBy(posting => posting.Slug.Value);
            }

            case JobSortOrder.SalaryLowToHigh:
            {
                var after = cursor?.AsNumber();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.Salary.Minimum > (decimal)value
                        || (posting.Salary.Minimum == (decimal)value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderBy(posting => posting.Salary.Minimum)
                    .ThenBy(posting => posting.Slug.Value);
            }

            case JobSortOrder.ClosingSoon:
            {
                var after = cursor?.AsTimestamp();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.ClosingAt > value
                        || (posting.ClosingAt == value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                // Postings with no closing date sort last: "closing soon" is meaningless for
                // one that never closes, and putting them first would bury the urgent ones.
                return query
                    .OrderBy(posting => posting.ClosingAt == null)
                    .ThenBy(posting => posting.ClosingAt)
                    .ThenBy(posting => posting.Slug.Value);
            }

            case JobSortOrder.MostApplied:
            {
                var after = cursor?.AsNumber();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.Engagement.ApplicationCount < (int)value
                        || (posting.Engagement.ApplicationCount == (int)value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(posting => posting.Engagement.ApplicationCount)
                    .ThenBy(posting => posting.Slug.Value);
            }

            case JobSortOrder.Featured:
            {
                var after = cursor?.AsTimestamp();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.PublishedAt < value
                        || (posting.PublishedAt == value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(posting => posting.IsFeatured)
                    .ThenByDescending(posting => posting.PublishedAt)
                    .ThenBy(posting => posting.Slug.Value);
            }

            // Relevance and Nearest both degrade to newest here, and honestly: the shipped EF
            // engine has no score and no geometry. A real engine implements both properly
            // behind IJobSearchEngine, and nothing above it changes.
            default:
            {
                var after = cursor?.AsTimestamp();

                if (after is { } value)
                {
                    query = query.Where(posting =>
                        posting.PublishedAt < value
                        || (posting.PublishedAt == value
                            && string.Compare(posting.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(posting => posting.PublishedAt)
                    .ThenBy(posting => posting.Slug.Value);
            }
        }
    }
#pragma warning restore CA1309

    private static string EncodeCursor(JobPosting last, JobSortOrder sort) => sort switch
    {
        JobSortOrder.SalaryHighToLow =>
            JobCursor.FromNumber((double)(last.Salary.Maximum ?? 0m), last.Slug.Value).Encode(),
        JobSortOrder.SalaryLowToHigh =>
            JobCursor.FromNumber((double)(last.Salary.Minimum ?? 0m), last.Slug.Value).Encode(),
        JobSortOrder.ClosingSoon =>
            JobCursor.FromTimestamp(
                last.ClosingAt ?? DateTimeOffset.MaxValue, last.Slug.Value).Encode(),
        JobSortOrder.MostApplied =>
            JobCursor.FromNumber(last.Engagement.ApplicationCount, last.Slug.Value).Encode(),
        _ => JobCursor.FromTimestamp(
            last.PublishedAt ?? last.CreatedAt, last.Slug.Value).Encode()
    };
}
