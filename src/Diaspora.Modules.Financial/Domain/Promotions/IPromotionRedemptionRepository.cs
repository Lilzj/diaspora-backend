using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Promotions;

/// <summary>Retrieval and staging for redemptions.</summary>
public interface IPromotionRedemptionRepository
{
    void Add(PromotionRedemption redemption);

    Task<PromotionRedemption?> FindAsync(
        PromotionRedemptionId redemptionId, CancellationToken cancellationToken = default);

    /// <summary>How many times this member has used this promotion. The per-subject limit check.</summary>
    Task<int> CountForSubjectAsync(
        PromotionId promotionId, Guid userId, CancellationToken cancellationToken = default);

    Task<int> CountForPromotionAsync(
        PromotionId promotionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromotionRedemption>> ListForUserAsync(
        Guid userId, int take, CancellationToken cancellationToken = default);
}
