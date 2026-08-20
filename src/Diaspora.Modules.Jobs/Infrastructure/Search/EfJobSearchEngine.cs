using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;

namespace Diaspora.Modules.Jobs.Infrastructure.Search;

/// <summary>
/// The search engine the module ships with: a filter over the write model.
/// <para>
/// <b>There is no index.</b> <see cref="IndexAsync"/> and <see cref="RemoveAsync"/> do nothing at
/// all, and that is the honest implementation rather than a stub — the write model <em>is</em>
/// the index, so it is never stale and there is nothing to reconcile. A deployment measured in
/// thousands of postings is well served by this; one measured in millions registers a real engine
/// and this class is never constructed again.
/// </para>
/// <para>
/// <b>It does not pretend to rank.</b> <c>Relevance</c> and <c>Nearest</c> degrade to
/// <c>Newest</c>, because there is no score and no geometry here, and a fabricated ranking is
/// worse than an admitted absence — it looks like it is working. A real engine implements both
/// properly behind the same port.
/// </para>
/// <para>
/// <b>Facets are computed, not skipped.</b> <see cref="JobSearchFacets"/> permits an empty answer
/// precisely so an engine can decline; this one can produce them from the same predicate at the
/// cost of a few grouped counts, and a results page with counts beside its filters is
/// meaningfully better than one without.
/// </para>
/// </summary>
public sealed class EfJobSearchEngine : IJobSearchEngine
{
    private readonly IJobPostingRepository _postings;

    public EfJobSearchEngine(IJobPostingRepository postings) => _postings = postings;

    /// <summary>
    /// A no-op. The write model is the index — see the class summary. Deliberately not throwing:
    /// callers index after every publish and edit, and a port whose shipped implementation threw
    /// would make every one of those a swallowed error nobody reads.
    /// </summary>
    public Task IndexAsync(
        JobSearchDocument document, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc cref="IndexAsync" />
    public Task IndexManyAsync(
        IReadOnlyCollection<JobSearchDocument> documents,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc cref="IndexAsync" />
    public Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<JobSearchOutcome> SearchAsync(
        JobSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _postings.QueryAsync(
            ToFilter(request), request.Cursor, request.Limit, cancellationToken);

        // Score is deliberately zero throughout: this engine has none, and emitting a fabricated
        // one would let a caller sort by it and believe the order meant something.
        var hits = page.Items
            .Select(posting => new JobSearchHit { JobId = posting.Id.Value, Score = 0d })
            .ToList();

        return new JobSearchOutcome
        {
            Hits = hits,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Null rather than a count: producing one means running the predicate a second time
            // over the whole table on every search, and "many" is a better answer than a slow
            // page. A real engine returns its own estimate.
            TotalCount = null
        };
    }

    /// <summary>
    /// Counts the matching set along each facet axis.
    /// <para>
    /// Each axis re-runs the predicate with that axis cleared, which is what a facet actually
    /// means: "how many would match if you also picked this". Six bounded counts, and they are
    /// only requested by a screen that renders them.
    /// </para>
    /// </summary>
    public async Task<JobSearchFacets> GetFacetsAsync(
        JobSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseFilter = ToFilter(request);

        var visaCount = await _postings.CountAsync(
            baseFilter with { VisaSponsorshipOnly = true }, cancellationToken);

        var verifiedCount = await _postings.CountAsync(
            baseFilter with { VerifiedEmployersOnly = true }, cancellationToken);

        var workplaceCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var workplace in Enum.GetValues<WorkplaceType>())
        {
            var count = await _postings.CountAsync(
                baseFilter with { WorkplaceTypes = [workplace] }, cancellationToken);

            if (count > 0)
            {
                workplaceCounts[workplace.ToString()] = count;
            }
        }

        var jobTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var jobType in Enum.GetValues<JobType>())
        {
            var count = await _postings.CountAsync(
                baseFilter with { JobTypes = [jobType] }, cancellationToken);

            if (count > 0)
            {
                jobTypeCounts[jobType.ToString()] = count;
            }
        }

        return new JobSearchFacets
        {
            WorkplaceTypes = workplaceCounts,
            JobTypes = jobTypeCounts,
            VisaSponsorshipCount = visaCount,
            VerifiedEmployerCount = verifiedCount,

            // Country and category facets are deliberately absent. Producing them means a
            // grouped count over the whole matching set, which the repository's filter-shaped
            // API cannot express without a scan — and JobSearchFacets exists to let an engine
            // say "not cheaply" rather than answer expensively.
            Countries = new Dictionary<string, int>(StringComparer.Ordinal),
            Categories = new Dictionary<string, int>(StringComparer.Ordinal),
            ExperienceLevels = new Dictionary<string, int>(StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// Turns the engine-independent request into the repository's filter.
    /// <para>
    /// The audience fields are left unset on purpose. This engine answers as the anonymous
    /// public would see the world; the caller re-applies its own visibility to the hits, which
    /// is the property that makes an over-returning engine harmless.
    /// </para>
    /// </summary>
    private static JobQueryFilter ToFilter(JobSearchRequest request) =>
        new()
        {
            Text = request.Text,
            CountryCode = request.CountryCode,
            CityName = request.CityName,
            AreaPathPrefix = request.AreaPathPrefix,
            AreaPathAncestors = request.AreaPathAncestors,
            CategoryKey = request.CategoryKey,
            Tag = request.Tag,
            SkillKeys = request.SkillKeys,
            EmployerId = request.EmployerId is { } employerId
                ? Domain.Identifiers.EmployerProfileId.From(employerId)
                : null,
            JobTypes = request.JobTypes,
            WorkplaceTypes = request.WorkplaceTypes,
            MinimumExperience = request.MinimumExperience,
            MinimumEducation = request.MinimumEducation,
            VisaSponsorshipOnly = request.VisaSponsorshipOnly,
            RelocationAssistanceOnly = request.RelocationAssistanceOnly,
            VerifiedEmployersOnly = request.VerifiedEmployersOnly,
            FeaturedOnly = request.FeaturedOnly,
            OpenOnly = request.OpenOnly,
            MinimumSalary = request.MinimumSalary,
            SalaryCurrency = request.SalaryCurrency,
            SalaryPeriod = request.SalaryPeriod,
            PublishedAfter = request.PublishedAfter,

            // Relevance and Nearest have no meaning here. Newest is the honest fallback.
            Sort = request.Sort is JobSortOrder.Relevance or JobSortOrder.Nearest
                ? JobSortOrder.Newest
                : request.Sort
        };
}
