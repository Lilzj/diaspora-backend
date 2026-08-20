using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Domain.Countries;

/// <summary>The country catalogue's persistence boundary.</summary>
public interface ICountryRepository
{
    Task<Country?> GetByCodeAsync(CountryCode code, CancellationToken cancellationToken = default);

    Task<Country?> GetByIdAsync(CountryId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every country in the catalogue, ordered featured-first then alphabetically.
    /// <para>
    /// Unpaged on purpose. There are fewer than 250 rows, every country-selection screen in
    /// the app needs all of them at once to offer a searchable list, and paging a set that
    /// small buys nothing but round trips. Cached at the application layer.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Country>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The paged, filtered projection the catalogue endpoint serves.</summary>
    Task<PagedResult<Country>> SearchAsync(
        string? term,
        bool? supportedOnly,
        string? continent,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CountryCode code, CancellationToken cancellationToken = default);

    Task AddAsync(Country country, CancellationToken cancellationToken = default);
}
