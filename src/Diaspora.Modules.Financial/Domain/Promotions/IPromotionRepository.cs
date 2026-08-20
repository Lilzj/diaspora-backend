using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Promotions;

/// <summary>Retrieval and staging for promotions.</summary>
public interface IPromotionRepository
{
    void Add(Promotion promotion);

    void Update(Promotion promotion);

    Task<Promotion?> FindAsync(PromotionId promotionId, CancellationToken cancellationToken = default);

    Task<Promotion?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Promotion>> ListActiveAsync(
        DateTimeOffset asOf, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Promotion>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
