using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Extensibility.Search;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Search;

/// <summary>
/// The shipped organization search: the database, through the repository.
/// See <see cref="EfCommunityEventSearchEngine"/> for why the seam exists and what it returns.
/// </summary>
public sealed class EfOrganizationSearchEngine : IOrganizationSearchEngine
{
    private readonly IOrganizationRepository _organizations;

    public EfOrganizationSearchEngine(IOrganizationRepository organizations) =>
        _organizations = organizations;

    public string EngineName => "database";

    public async Task<OrganizationSearchResult> SearchAsync(
        OrganizationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var page = await _organizations.SearchAsync(filter, cancellationToken);

        return new OrganizationSearchResult
        {
            Hits =
                [.. page.Items.Select(item => new OrganizationSearchHit(item.Id.Value, 0d))],
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = page.TotalCount
        };
    }
}
