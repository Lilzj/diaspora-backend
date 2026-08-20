using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Accounts;

/// <summary>Retrieval and staging for financial identities.</summary>
public interface IFinancialAccountRepository
{
    void Add(FinancialAccount account);

    void Update(FinancialAccount account);

    Task<FinancialAccount?> FindAsync(
        FinancialAccountId accountId, CancellationToken cancellationToken = default);

    /// <summary>The account belonging to an owner of a kind. One per owner per kind.</summary>
    Task<FinancialAccount?> FindByOwnerAsync(
        Guid ownerId, FinancialAccountType accountType, CancellationToken cancellationToken = default);

    /// <summary>The platform's own account. Exactly one exists; the seeder guarantees it.</summary>
    Task<FinancialAccount?> FindPlatformAccountAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsForOwnerAsync(
        Guid ownerId, FinancialAccountType accountType, CancellationToken cancellationToken = default);
}
