using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Application.Search;

/// <summary>
/// COMMUNITY-LIFE-GAP-021 — the federated Community Life search.
/// <para>
/// <b>The register's complaint was not that search did not work; it was that the client had to do the
/// federating.</b> Two of the seven types were real, five were simulated, and the frontend "searched
/// them separately, merged, grouped by type and labelled by source, with a count of how many results
/// are live and how many are simulated. The merge order is a documented client behaviour to be
/// replaced." This route replaces it: one call, one ordered list, and the order is the server's.
/// </para>
/// <para>
/// <b>Four of the seven types are served here and three are not, and the three are named rather than
/// silently absent.</b> Events, series and organizations are B12's — this module does not
/// re-implement a search over another module's rows, and the client calls B12's routes for them
/// exactly as it does today. Campaigns are B15.1's and do not exist yet. What this route federates is
/// the four types B18 owns: membership programmes, volunteer opportunities, polls and announcements.
/// </para>
/// <para>
/// <b>Relevance is a score the server computes, and the client re-orders nothing.</b> A title match
/// outranks a body match; a recently published row outranks an old one at the same match quality. It
/// is deliberately simple and deliberately explicit — a ranking nobody can explain is a ranking
/// nobody can debug, and the alternative was leaving the order to a concatenation.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record SearchCommunityQuery
    : IQuery<CommunityPageResponse<CommunitySearchResultResponse>>
{
    public string? Query { get; init; }

    /// <summary>
    /// Which types to search. Absent means every type this route owns.
    /// <para>
    /// A type this route does not own — <c>event</c>, <c>series</c>, <c>organization</c>,
    /// <c>campaign</c> — is accepted and contributes nothing, rather than being refused. A client
    /// asking for all seven gets the four that exist here and calls B12 for the rest, which is
    /// exactly what it does today minus the labelling.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? Types { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? Cause { get; init; }

    public string? PriceBand { get; init; }

    public int? Take { get; init; }
}

public sealed class SearchCommunityQueryHandler
    : IQueryHandler<SearchCommunityQuery, CommunityPageResponse<CommunitySearchResultResponse>>
{
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly ICommunityPollRepository _polls;
    private readonly ICommunityAnnouncementRepository _announcements;
    private readonly CommunityProgrammeAccessService _access;

    public SearchCommunityQueryHandler(
        IMembershipProgrammeRepository programmes,
        IVolunteerOpportunityRepository opportunities,
        ICommunityPollRepository polls,
        ICommunityAnnouncementRepository announcements,
        CommunityProgrammeAccessService access)
    {
        _programmes = programmes;
        _opportunities = opportunities;
        _polls = polls;
        _announcements = announcements;
        _access = access;
    }

    public async Task<Result<CommunityPageResponse<CommunitySearchResultResponse>>> Handle(
        SearchCommunityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var term = query.Query?.Trim() ?? string.Empty;
        var take = CommunityProgrammeLimits.PageSize(query.Take);
        var wanted = ParseTypes(query.Types);

        var memberOf = await _access.MemberOfAsync(cancellationToken);
        var results = new List<CommunitySearchResultResponse>();

        // Sequential, on one scoped DbContext. Concurrency here is the Wave 4 defect verbatim.
        if (wanted.Contains(CommunityEntityType.Membership))
        {
            results.AddRange(await SearchProgrammesAsync(query, term, memberOf, cancellationToken));
        }

        if (wanted.Contains(CommunityEntityType.Opportunity))
        {
            results.AddRange(await SearchOpportunitiesAsync(query, term, cancellationToken));
        }

        if (wanted.Contains(CommunityEntityType.Poll))
        {
            results.AddRange(await SearchPollsAsync(query, term, memberOf, cancellationToken));
        }

        if (wanted.Contains(CommunityEntityType.Announcement))
        {
            results.AddRange(await SearchAnnouncementsAsync(query, term, memberOf, cancellationToken));
        }

        var ordered = results
            .OrderByDescending(result => result.Relevance)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();

        return Result.Success(new CommunityPageResponse<CommunitySearchResultResponse>
        {
            Items = ordered,

            // No cursor, deliberately. A federated page over four sources cannot carry one token that
            // resumes all four without either interleaving being lost or one source being served
            // ahead of the others. The register asks for relevance ordering rather than for infinite
            // scroll, and a search screen that shows the best fifty and invites a narrower query is
            // the honest shape.
            NextCursor = null,
            HasMore = results.Count > ordered.Count,
            TotalCount = results.Count
        });
    }

    private async Task<IEnumerable<CommunitySearchResultResponse>> SearchProgrammesAsync(
        SearchCommunityQuery query,
        string term,
        IReadOnlyList<Guid> memberOf,
        CancellationToken cancellationToken)
    {
        var page = await _programmes.SearchAsync(
            new ProgrammeQueryFilter
            {
                VisibleOrganizationIds = memberOf,
                PriceBand = query.PriceBand,
                OpenOnly = true,
                Limit = CommunityProgrammeLimits.MaxPageSize
            },
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return [];
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. page.Items.Select(programme => programme.OrganizationId)], cancellationToken);

        var rows = new List<CommunitySearchResultResponse>();

        foreach (var programme in page.Items)
        {
            if (!organizations.TryGetValue(programme.OrganizationId, out var organization))
            {
                continue;
            }

            if (!Matches(query, organization))
            {
                continue;
            }

            var relevance = Score(term, organization.Name, programme.Summary, null);

            if (relevance <= 0)
            {
                continue;
            }

            rows.Add(new CommunitySearchResultResponse
            {
                EntityType = "membership",
                EntityId = programme.OrganizationId,
                Title = organization.Name,
                Subtitle = programme.Summary,
                ImageMediaId = organization.LogoMediaId,
                LocationLabel = organization.CityName,
                Badge = programme.Tiers.Any(tier => tier.Price is null) ? "Free tier" : null,
                ActionRoute = $"/community-life/membership/{programme.OrganizationId}",
                Relevance = relevance
            });
        }

        return rows;
    }

    private async Task<IEnumerable<CommunitySearchResultResponse>> SearchOpportunitiesAsync(
        SearchCommunityQuery query, string term, CancellationToken cancellationToken)
    {
        var page = await _opportunities.SearchAsync(
            new OpportunityQueryFilter
            {
                SearchTerm = string.IsNullOrWhiteSpace(term) ? null : term,
                Cause = query.Cause,
                OrganizationId = query.OrganizationId,
                CountryCode = query.CountryCode,
                CityName = query.CityName,
                Statuses =
                [
                    VolunteerOpportunityStatus.Published,
                    VolunteerOpportunityStatus.Full
                ],
                Limit = CommunityProgrammeLimits.MaxPageSize
            },
            cancellationToken);

        return page.Items.Select(opportunity => new CommunitySearchResultResponse
        {
            EntityType = "opportunity",
            EntityId = opportunity.Id.Value,
            Title = opportunity.Title,
            Subtitle = opportunity.Cause,
            LocationLabel = opportunity.CityName,
            Badge = opportunity.IsFull ? "Waiting list" : null,
            ActionRoute = $"/community-life/volunteer/{opportunity.Id.Value}",
            Relevance = Score(term, opportunity.Title, opportunity.Description, opportunity.CreatedAt)
        })
        .Where(result => result.Relevance > 0);
    }

    private async Task<IEnumerable<CommunitySearchResultResponse>> SearchPollsAsync(
        SearchCommunityQuery query,
        string term,
        IReadOnlyList<Guid> memberOf,
        CancellationToken cancellationToken)
    {
        var managed = await _access.ManagedForAsync(
            OrganizationCapability.ManagePolls, cancellationToken);

        var page = await _polls.SearchAsync(
            new PollQueryFilter
            {
                OrganizationId = query.OrganizationId,
                Statuses = [PollStatus.Open, PollStatus.Closed, PollStatus.ResultsPublished],
                MemberOfOrganizationIds = memberOf,
                ManagedOrganizationIds = managed,
                ViewerUserId = _access.CallerId,
                SearchTerm = string.IsNullOrWhiteSpace(term) ? null : term,
                Limit = CommunityProgrammeLimits.MaxPageSize
            },
            cancellationToken);

        return page.Items.Select(poll => new CommunitySearchResultResponse
        {
            EntityType = "poll",
            EntityId = poll.Id.Value,
            Title = poll.Question,
            Subtitle = poll.Description,
            Badge = poll.Status == PollStatus.Open ? "Open" : null,
            ActionRoute = $"/community-life/poll/{poll.Id.Value}",
            Relevance = Score(term, poll.Question, poll.Description, poll.CreatedAt)
        })
        .Where(result => result.Relevance > 0);
    }

    private async Task<IEnumerable<CommunitySearchResultResponse>> SearchAnnouncementsAsync(
        SearchCommunityQuery query,
        string term,
        IReadOnlyList<Guid> memberOf,
        CancellationToken cancellationToken)
    {
        var page = await _announcements.SearchAsync(
            new AnnouncementQueryFilter
            {
                CountryCode = query.CountryCode,
                MemberOfOrganizationIds = memberOf,
                SearchTerm = string.IsNullOrWhiteSpace(term) ? null : term,
                Limit = CommunityProgrammeLimits.MaxPageSize
            },
            cancellationToken);

        return page.Items.Select(announcement => new CommunitySearchResultResponse
        {
            EntityType = "announcement",
            EntityId = announcement.Id.Value,
            Title = announcement.Title,
            Subtitle = announcement.Body,
            Badge = announcement.IsPinned ? "Pinned" : null,
            ActionRoute = "/community-life/announcements",
            Relevance = Score(term, announcement.Title, announcement.Body, announcement.PublishedAt)
        })
        .Where(result => result.Relevance > 0);
    }

    /// <summary>
    /// The relevance contract, stated in one place.
    /// <para>
    /// A title match is worth more than a body match; an exact title is worth more than a prefix. A
    /// recent row edges out an older one at the same match quality, which is what makes a poll that
    /// closed last year sit below one that opened yesterday. An empty query matches everything at the
    /// recency score alone, so a search screen with no text still has an order.
    /// </para>
    /// </summary>
    private static double Score(string term, string title, string? body, DateTimeOffset? createdAt)
    {
        var recency = createdAt is { } moment
            ? Math.Max(0, 1 - (DateTimeOffset.UtcNow - moment).TotalDays / 365d) * 0.5
            : 0.25;

        if (string.IsNullOrWhiteSpace(term))
        {
            return 1 + recency;
        }

        if (title.Equals(term, StringComparison.OrdinalIgnoreCase))
        {
            return 10 + recency;
        }

        if (title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return 8 + recency;
        }

        if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return 6 + recency;
        }

        if (body?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
        {
            return 3 + recency;
        }

        return 0;
    }

    private static bool Matches(SearchCommunityQuery query, OrganizationSummary organization)
    {
        if (query.OrganizationId is { } organizationId
            && organization.OrganizationId != organizationId)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode)
            && !string.Equals(
                organization.CountryCode,
                query.CountryCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(query.CityName)
            || string.Equals(
                organization.CityName, query.CityName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Which of this route's four types were asked for.
    /// <para>
    /// An unrecognised or foreign type is dropped rather than refused: a client asking for all seven
    /// gets the four that live here and calls B12 for the rest.
    /// </para>
    /// </summary>
    private static HashSet<CommunityEntityType> ParseTypes(IReadOnlyList<string>? types)
    {
        var owned = new[]
        {
            CommunityEntityType.Membership,
            CommunityEntityType.Opportunity,
            CommunityEntityType.Poll,
            CommunityEntityType.Announcement
        };

        if (types is not { Count: > 0 })
        {
            return [.. owned];
        }

        var wanted = new HashSet<CommunityEntityType>();

        foreach (var type in types)
        {
            if (Enum.TryParse<CommunityEntityType>(type, ignoreCase: true, out var parsed)
                && owned.Contains(parsed))
            {
                wanted.Add(parsed);
            }
        }

        return wanted;
    }
}
