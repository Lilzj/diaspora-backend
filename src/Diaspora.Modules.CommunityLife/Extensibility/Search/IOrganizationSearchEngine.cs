using Diaspora.Modules.CommunityLife.Domain.Organizations;

namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>
/// How organizations are found.
/// <para>
/// A second port rather than a generic one, because the two searches are genuinely different
/// queries: an event search is dominated by a time window and an organization search by a place and
/// a size, and an engine optimised for one is not automatically good at the other. Keeping them
/// separate also means a deployment can replace one and keep the shipped implementation of the
/// other.
/// </para>
/// </summary>
public interface IOrganizationSearchEngine
{
    string EngineName { get; }

    Task<OrganizationSearchResult> SearchAsync(
        OrganizationQueryFilter filter, CancellationToken cancellationToken = default);
}
