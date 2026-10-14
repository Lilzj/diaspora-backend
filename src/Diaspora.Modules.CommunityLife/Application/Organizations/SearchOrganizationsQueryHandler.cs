using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Extensibility.Search;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// The organization directory.
/// <para>
/// <b>The search engine is a seam, and the shipped one is a database query.</b> A deployment can
/// register something else; when it does, this handler hydrates the aggregates from the identifiers
/// the engine returned rather than trusting it to carry a payload, which is what stops a stale index
/// from serving a body that has since been suspended.
/// </para>
/// <para>
/// <b>The neighbourhood is widened to a path prefix.</b> Somebody searching a neighbourhood wants
/// what is around them, and B4's materialised path turns "in this area or below it" into one indexed
/// predicate rather than a recursive walk.
/// </para>
/// </summary>
public sealed class SearchOrganizationsQueryHandler
    : IQueryHandler<SearchOrganizationsQuery, OrganizationSearchResultResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationSearchEngine _engine;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeGeographicScopeAdapter _geography;

    public SearchOrganizationsQueryHandler(
        IOrganizationRepository organizations,
        IOrganizationSearchEngine engine,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeGeographicScopeAdapter geography)
    {
        _organizations = organizations;
        _engine = engine;
        _neighborhoods = neighborhoods;
        _authorization = authorization;
        _geography = geography;
    }

    public async Task<Result<OrganizationSearchResultResponse>> Handle(
        SearchOrganizationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new OrganizationQueryFilter
        {
            SearchTerm = query.SearchTerm,
            TypeKeys = query.TypeKeys,
            CountryCode = query.CountryCode,
            CityName = query.CityName,
            NeighborhoodAreaId = query.NeighborhoodAreaId,
            AreaPathPrefix = await _neighborhoods.AreaPathAsync(
                query.NeighborhoodAreaId, cancellationToken),
            VerifiedOnly = query.VerifiedOnly,
            FeaturedOnly = query.FeaturedOnly,
            AcceptingMembers = query.AcceptingMembers,
            Languages = query.Languages,
            ViewerUserId = _authorization.CallerId,

            // A directory is a public surface. Drafts and archived pages belong to the "my
            // organizations" read, which asks the same repository with this set.
            IncludeNonPublic = false,
            SortOrder = CommunityLifeSharedMapper.ParseEnum<OrganizationSortOrder>(query.SortOrder)
                ?? OrganizationSortOrder.MostMembers,
            Cursor = query.Cursor,
            Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
        };

        // A viewport supersedes the city and neighbourhood narrowing rather than adding to it.
        //
        // Since 14 Oct 2026 a point and a typed town are narrowed here too, without the place
        // catalogue: a circle by coordinates, a town by its name in either letter case.
        if (!string.IsNullOrWhiteSpace(query.Bounds)
            || (query.Latitude is not null && query.Longitude is not null)
            || !string.IsNullOrWhiteSpace(query.CityName))
        {
            var narrowed = await _geography.NarrowAsync(
                filter,
                query.CityName,
                query.CountryCode,
                query.Latitude,
                query.Longitude,
                query.RadiusKm,
                query.Bounds,
                _authorization.CallerId,
                cancellationToken);

            if (narrowed.IsFailure)
            {
                return Result.Failure<OrganizationSearchResultResponse>(narrowed.Error);
            }

            filter = narrowed.Value;
        }

        var result = await _engine.SearchAsync(filter, cancellationToken);

        var organizations = await HydrateAsync(result, cancellationToken);

        CommunityLifeTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>("subject", "organization"),
            new KeyValuePair<string, object?>("engine", _engine.EngineName));

        return Result.Success(new OrganizationSearchResultResponse
        {
            Items = [.. organizations.Select(OrganizationMapper.ToSummary)],
            NextCursor = result.NextCursor,
            HasMore = result.HasMore,
            TotalCount = result.TotalCount,
            Facets = result.Facets,
            Engine = _engine.EngineName
        });
    }

    /// <summary>
    /// Loads the bodies the engine named, keeping its ordering.
    /// <para>
    /// One batched read. A row the engine knew about but the database no longer has — deleted since
    /// the index was built — simply drops out, which is the behaviour that makes a stale index safe
    /// rather than wrong.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Organization>> HydrateAsync(
        OrganizationSearchResult result, CancellationToken cancellationToken)
    {
        if (result.Hits.Count == 0)
        {
            return [];
        }

        var ids = result.Hits
            .Select(hit => OrganizationId.From(hit.OrganizationId))
            .ToList();

        var loaded = await _organizations.FindManyAsync(ids, cancellationToken);
        var byId = loaded.ToDictionary(organization => organization.Id);

        return [.. ids.Select(byId.GetValueOrDefault).OfType<Organization>()];
    }
}
