using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>Retrieval and staging for the versioned policy catalogue.</summary>
public interface IFinancialPolicyRepository
{
    void Add(FinancialPolicy policy);

    void Update(FinancialPolicy policy);

    Task<FinancialPolicy?> FindAsync(
        FinancialPolicyId policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active version of a key, across scopes. The engine picks the most specific one that
    /// matches — which is a decision the domain makes rather than the query.
    /// </summary>
    Task<IReadOnlyList<FinancialPolicy>> ListActiveByKeyAsync(
        string policyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialPolicy>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialPolicy>> ListByKeyAsync(
        string policyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialPolicy>> ListByKindAsync(
        FinancialPolicyKind kind, CancellationToken cancellationToken = default);

    /// <summary>The highest version number in use for a key and scope, or zero.</summary>
    Task<int> HighestVersionAsync(
        string policyKey,
        PolicyScopeKind scope,
        string? scopeValue,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string policyKey,
        PolicyScopeKind scope,
        string? scopeValue,
        int version,
        CancellationToken cancellationToken = default);
}
