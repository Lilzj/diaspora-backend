using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Currencies;

/// <summary>Retrieval and staging for the currency catalogue.</summary>
public interface ICurrencyRepository
{
    void Add(CurrencyDefinition currency);

    void Update(CurrencyDefinition currency);

    Task<CurrencyDefinition?> FindAsync(
        CurrencyDefinitionId currencyId, CancellationToken cancellationToken = default);

    Task<CurrencyDefinition?> FindByCodeAsync(
        string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrencyDefinition>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CurrencyDefinition>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
