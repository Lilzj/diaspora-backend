using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Catalog;

/// <summary>Retrieval and staging for the monetization catalogue.</summary>
public interface IMonetizationProductRepository
{
    void Add(MonetizationProduct product);

    void Update(MonetizationProduct product);

    Task<MonetizationProduct?> FindAsync(
        MonetizationProductId productId, CancellationToken cancellationToken = default);

    Task<MonetizationProduct?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default);

    /// <summary>Everything on sale, optionally narrowed to what a subject can be bought for.</summary>
    Task<IReadOnlyList<MonetizationProduct>> ListAvailableAsync(
        MonetizationSubjectType? subjectType,
        MonetizationProductCategory? category,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonetizationProduct>> ListAllAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
