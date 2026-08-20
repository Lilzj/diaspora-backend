using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Modules.Jobs.Extensibility.Ranking;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Jobs.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Discovery;

/// <summary>
/// Runs a job search.
/// <para>
/// <b>The engine answers with identifiers; this handler hydrates and re-applies visibility.</b>
/// That order is a correctness property rather than an optimisation: a stale index can misrank a
/// result but can never surface a posting whose visibility changed after it was indexed, because
/// the repository filters whatever comes back.
/// </para>
/// <para>
/// The employer chips, the "you applied" flags and the "you saved" flags are each resolved once
/// for the whole page. Doing any of them per card would make a twenty-row page sixty round trips.
/// </para>
/// </summary>
public sealed class SearchJobsQueryHandler
    : IQueryHandler<SearchJobsQuery, JobSearchResultResponse>
{
    private readonly IJobSearchEngine _engine;
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IJobApplicationRepository _applications;
    private readonly ISavedJobRepository _saved;
    private readonly JobRankingStrategyRegistry _ranking;
    private readonly JobAuthorizationService _authorization;
    private readonly JobGeographicScopeAdapter _geography;
    private readonly IClock _clock;

    public SearchJobsQueryHandler(
        IJobSearchEngine engine,
        IJobPostingRepository postings,
        IEmployerProfileRepository employers,
        IJobApplicationRepository applications,
        ISavedJobRepository saved,
        JobRankingStrategyRegistry ranking,
        JobAuthorizationService authorization,
        JobGeographicScopeAdapter geography,
        IClock clock)
    {
        _geography = geography;
        _engine = engine;
        _postings = postings;
        _employers = employers;
        _applications = applications;
        _saved = saved;
        _ranking = ranking;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<JobSearchResultResponse>> Handle(
        SearchJobsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;

        JobTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>(
                "has_term", !string.IsNullOrWhiteSpace(request.Query)));

        var searchRequest = await ApplyGeographyAsync(
            ToSearchRequest(request), request, cancellationToken);

        var outcome = await _engine.SearchAsync(searchRequest, cancellationToken);

        if (outcome.Hits.Count == 0)
        {
            return Result.Success(new JobSearchResultResponse
            {
                Items = [],
                NextCursor = null,
                HasMore = false,
                TotalCount = outcome.TotalCount,
                Facets = query.IncludeFacets ? Empty() : null
            });
        }

        // Re-applied here, against the hits the engine returned. An over-returning engine costs
        // a wasted row; it can never leak a draft.
        var ids = outcome.Hits.Select(hit => JobPostingId.From(hit.JobId)).ToList();

        var postings = await _postings.GetManyAsync(ids, cancellationToken);

        var visible = postings
            .Where(posting => posting.IsDiscoverable)
            .ToList();

        // The engine's order, preserved. It knows why it ranked as it did; this does not.
        var order = outcome.Hits
            .Select((hit, index) => (hit.JobId, index))
            .ToDictionary(entry => entry.JobId, entry => entry.index);

        var ordered = visible
            .OrderBy(posting => order.GetValueOrDefault(posting.Id.Value, int.MaxValue))
            .ToList();

        var ranked = _ranking.Rank(request.Ranking, ordered, BuildRankingContext(request));

        var cards = await BuildCardsAsync(ranked, cancellationToken);

        return Result.Success(new JobSearchResultResponse
        {
            Items = cards,
            NextCursor = outcome.NextCursor,
            HasMore = outcome.HasMore,
            TotalCount = outcome.TotalCount,
            Facets = query.IncludeFacets
                ? ToFacets(await _engine.GetFacetsAsync(searchRequest, cancellationToken))
                : null
        });
    }

    /// <summary>
    /// Narrows the search request to whatever geography the client sent, through B4.
    /// <para>
    /// <b>Only when the client asked something the raw strings cannot answer.</b> A browse by
    /// country or by city name is already served by the columns the posting table indexes, and
    /// sending it through the resolver would add a catalogue round trip to the most common
    /// request on the module for no change in the result. A viewport or an area id is the case
    /// that genuinely needs resolving, and until this phase there was no route by which one could
    /// arrive: <c>JobGeographicScopeAdapter</c> was registered in DI and called from nowhere.
    /// </para>
    /// <para>
    /// No B4 type appears in this method, and that is enforced —
    /// <c>JobsArchitectureTests.Only_one_class_talks_to_location</c> permits the adapter and
    /// nothing else, so the adapter takes raw values and hands back a narrowed request.
    /// </para>
    /// </summary>
    private async Task<JobSearchRequest> ApplyGeographyAsync(
        JobSearchRequest searchRequest,
        Contracts.V1.Requests.SearchJobsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Bounds) && request.AreaId is null)
        {
            return searchRequest;
        }

        return await _geography.NarrowAsync(
            searchRequest,
            request.CityName,
            request.CountryCode,
            request.AreaId,
            request.Bounds,
            _authorization.CallerId,
            cancellationToken);
    }

    private static JobSearchRequest ToSearchRequest(
        Contracts.V1.Requests.SearchJobsRequest request) =>
        new()
        {
            Text = request.Query,
            CountryCode = request.CountryCode,
            CityName = request.CityName,
            CategoryKey = request.CategoryKey,
            Tag = request.Tag,

            // Normalised through the same function the posting side uses, or a candidate
            // searching "React.js" never finds a posting asking for "reactjs".
            SkillKeys = request.Skills is { Count: > 0 }
                ? [.. request.Skills
                    .Select(JobSkillRequirement.NormalizeKey)
                    .Where(key => key is not null)
                    .Select(key => key!)]
                : null,

            EmployerId = request.EmployerId,
            JobTypes = JobWireNames.ParseMany<JobType>(request.JobTypes),
            WorkplaceTypes = JobWireNames.ParseMany<WorkplaceType>(request.WorkplaceTypes),
            MinimumExperience = JobWireNames.ParseExperienceLevel(request.MinimumExperience),
            MinimumEducation = JobWireNames.ParseEducationLevel(request.MinimumEducation),
            VisaSponsorshipOnly = request.VisaSponsorshipOnly,
            RelocationAssistanceOnly = request.RelocationAssistanceOnly,
            VerifiedEmployersOnly = request.VerifiedEmployersOnly,
            FeaturedOnly = request.FeaturedOnly,
            OpenOnly = request.OpenOnly,
            MinimumSalary = request.MinimumSalary,
            SalaryCurrency = request.SalaryCurrency,
            SalaryPeriod = JobWireNames.ParseSalaryPeriod(request.SalaryPeriod),
            PublishedAfter = request.PublishedAfter,
            Sort = JobWireNames.ParseSort(request.Sort) ?? JobSortOrder.Newest,
            Cursor = request.Cursor,
            Limit = Math.Clamp(request.Limit, 1, JobLimits.MaxPageSize)
        };

    private JobRankingContext BuildRankingContext(
        Contracts.V1.Requests.SearchJobsRequest request) =>
        new()
        {
            ViewerCountryCode = request.CountryCode,
            SearchTerm = request.Query,
            NeedsSponsorship = request.VisaSponsorshipOnly,
            Now = _clock.UtcNow
        };

    /// <summary>
    /// Builds the cards, resolving the employer chips and the viewer annotations once each for
    /// the whole page rather than per row.
    /// </summary>
    private async Task<IReadOnlyList<JobCardResponse>> BuildCardsAsync(
        IReadOnlyList<JobPosting> postings, CancellationToken cancellationToken)
    {
        if (postings.Count == 0)
        {
            return [];
        }

        var employers = (await _employers.GetManyAsync(
                [.. postings.Select(posting => posting.EmployerId).Distinct()],
                cancellationToken))
            .ToDictionary(employer => employer.Id.Value);

        IReadOnlySet<Guid>? applied = null;
        IReadOnlySet<Guid>? saved = null;

        if (_authorization.CallerId is { } viewer)
        {
            var ids = postings.Select(posting => posting.Id).ToList();

            applied = await _applications.FindAppliedPostingIdsAsync(
                viewer, ids, cancellationToken);

            saved = await _saved.FindSavedPostingIdsAsync(viewer, ids, cancellationToken);
        }

        return [.. postings.Select(posting => JobMapper.ToCard(
            posting,
            employers.GetValueOrDefault(posting.EmployerId.Value),
            applied,
            saved))];
    }

    private static JobSearchFacetsResponse ToFacets(JobSearchFacets facets) =>
        new()
        {
            WorkplaceTypes = facets.WorkplaceTypes,
            JobTypes = facets.JobTypes,
            Countries = facets.Countries,
            Categories = facets.Categories,
            VisaSponsorshipCount = facets.VisaSponsorshipCount,
            VerifiedEmployerCount = facets.VerifiedEmployerCount
        };

    private static JobSearchFacetsResponse Empty() => ToFacets(JobSearchFacets.None);
}
