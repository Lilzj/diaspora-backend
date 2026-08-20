using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Emergency;

/// <summary>
/// How the application layer reaches emergency information.
/// <para>
/// <b>There is no <c>GetIncludingArchivedAsync</c> here, and its absence is the aggregate's rule made
/// visible.</b> Emergency directories are the one thing in this phase that is never soft-deleted: a
/// hidden row holding a number that now rings somewhere else is a liability with no upside. Nothing
/// to un-hide means nothing to look past a filter for.
/// </para>
/// </summary>
public interface ICountryEmergencyDirectoryRepository
{
    Task<CountryEmergencyDirectory?> GetAsync(
        CountryEmergencyDirectoryId id, CancellationToken cancellationToken = default);

    /// <summary>One country's emergency information, or null when nobody has written it yet.</summary>
    Task<CountryEmergencyDirectory?> GetForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>Whether a country already has a directory. Guards the create.</summary>
    Task<bool> ExistsForCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>Every country with emergency information, for an administrative index.</summary>
    Task<IReadOnlyList<CountryEmergencyDirectory>> ListAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        CountryEmergencyDirectory directory, CancellationToken cancellationToken = default);
}
