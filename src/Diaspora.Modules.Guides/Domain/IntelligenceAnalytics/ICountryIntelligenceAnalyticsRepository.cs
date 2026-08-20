namespace Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;

/// <summary>
/// How the application layer reaches per-country intelligence usage counters.
/// <para>
/// <see cref="GetOrStartAsync"/> exists because every caller is a recorder on a read path, and a
/// recorder that had to check for existence and then create would be two round trips and a race on
/// the first view of a country nobody has opened before.
/// </para>
/// </summary>
public interface ICountryIntelligenceAnalyticsRepository
{
    /// <summary>One country's counters, or null when nothing has ever been recorded for it.</summary>
    Task<CountryIntelligenceAnalytics?> GetForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// One country's counters, creating the row on first sight.
    /// <para>
    /// The created row is added to the change tracker and not saved — the caller's unit of work does
    /// that, so recording several signals in one request costs one insert rather than several.
    /// </para>
    /// </summary>
    Task<CountryIntelligenceAnalytics> GetOrStartAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>Every country with recorded activity, for a platform-wide editorial view.</summary>
    Task<IReadOnlyList<CountryIntelligenceAnalytics>> ListAsync(
        CancellationToken cancellationToken = default);
}
