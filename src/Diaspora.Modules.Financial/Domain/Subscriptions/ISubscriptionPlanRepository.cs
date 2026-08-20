using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Subscriptions;

/// <summary>Retrieval and staging for the plan catalogue.</summary>
public interface ISubscriptionPlanRepository
{
    void Add(SubscriptionPlan plan);

    void Update(SubscriptionPlan plan);

    Task<SubscriptionPlan?> FindAsync(
        SubscriptionPlanId planId, CancellationToken cancellationToken = default);

    Task<SubscriptionPlan?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlan>> ListActiveAsync(
        FinancialAccountType? audienceType,
        string? countryCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlan>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
