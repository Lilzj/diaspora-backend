using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>Retrieval and staging for community funds.</summary>
public interface IContributionFundRepository
{
    void Add(ContributionFund fund);

    void Update(ContributionFund fund);

    Task<ContributionFund?> FindAsync(
        ContributionFundId fundId, CancellationToken cancellationToken = default);

    Task<ContributionFund?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContributionFund>> ListActiveAsync(
        string? countryCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContributionFund>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
