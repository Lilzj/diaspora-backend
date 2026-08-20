namespace Diaspora.Modules.Guides.Domain.CountryContext;

/// <summary>Reads and writes the cross-vertical counter projection.</summary>
public interface ICountryContextSummaryRepository
{
    /// <summary>The counters for one country, tracked. The upsert path on every consumed event.</summary>
    Task<CountryContextSummary?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>The counters for one country, untracked. For the dashboard.</summary>
    Task<CountryContextSummary?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counters for several countries at once, untracked.
    /// <para>
    /// The N+1 guard for the country grid, which renders a badge per country and would otherwise
    /// be one query per tile.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, CountryContextSummary>> GetManyForReadAsync(
        IReadOnlyCollection<string> countryCodes, CancellationToken cancellationToken = default);

    Task AddAsync(CountryContextSummary summary, CancellationToken cancellationToken = default);
}
