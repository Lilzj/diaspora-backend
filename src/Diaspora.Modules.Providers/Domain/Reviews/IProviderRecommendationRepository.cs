using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Providers.Domain.Reviews;

/// <summary>Reads and writes provider recommendations, endorsements and testimonials.</summary>
public interface IProviderRecommendationRepository
{
    Task<ProviderRecommendation?> GetAsync(
        ProviderRecommendationId id, CancellationToken cancellationToken = default);

    /// <summary>A member's existing recommendation of a provider, if any. One each.</summary>
    Task<ProviderRecommendation?> FindByRecommenderAsync(
        ProviderId providerId, UserId recommenderId, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<ProviderRecommendation>> ListForProviderAsync(
        ProviderId providerId,
        string? cursor,
        int limit,
        bool featuredOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>Active recommendation counts for several providers, for the statistics rollup.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountForProvidersAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default);

    Task AddAsync(ProviderRecommendation recommendation, CancellationToken cancellationToken = default);

    void Remove(ProviderRecommendation recommendation);
}
