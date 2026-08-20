using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// Everything a client needs to render the pickers and the filter bar, in one call.
/// <para>
/// <b>Retired entries are included only when asked for.</b> A page filed under a retired category
/// still has to render its name, so an editor screen asks for them; a create form must not offer
/// them, so it does not.
/// </para>
/// <para>
/// The facets are derived from the live categories rather than stored separately, so a facet stops
/// appearing the moment the last category carrying it is retired.
/// </para>
/// </summary>
public sealed class GetCommunityLifeCatalogueQueryHandler
    : IQueryHandler<GetCommunityLifeCatalogueQuery, CommunityLifeCatalogueResponse>
{
    private readonly IOrganizationTypeRepository _types;
    private readonly IEventCategoryRepository _categories;
    private readonly CommunityLifeCatalogResolver _catalog;

    public GetCommunityLifeCatalogueQueryHandler(
        IOrganizationTypeRepository types,
        IEventCategoryRepository categories,
        CommunityLifeCatalogResolver catalog)
    {
        _types = types;
        _categories = categories;
        _catalog = catalog;
    }

    public async Task<Result<CommunityLifeCatalogueResponse>> Handle(
        GetCommunityLifeCatalogueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var types = await _types.ListAsync(query.IncludeRetired, cancellationToken);
        var categories = await _categories.ListAsync(query.IncludeRetired, cancellationToken);

        return Result.Success(new CommunityLifeCatalogueResponse
        {
            OrganizationTypes =
                [.. types.Select(CommunityLifeCatalogueMapper.ToResponse)],
            EventCategories =
                [.. categories.Select(CommunityLifeCatalogueMapper.ToResponse)],
            Facets = await _catalog.FacetsAsync(cancellationToken)
        });
    }
}
