using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Experience;

/// <summary>The country-experience persistence boundary.</summary>
public interface ICountryExperienceRepository
{
    Task<CountryExperience?> GetAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default);

    Task<CountryExperience?> GetByIdAsync(
        CountryExperienceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's whole residence history, most recent arrival first.
    /// <para>
    /// Unpaged: nobody has lived in fifty countries, the timeline screen renders all of them,
    /// and paging a list that is almost always under five entries adds a cursor nobody reads.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<CountryExperience>> ListByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The residence histories of several members at once.
    /// <para>
    /// Exists so a contributor list can render everyone's country badges without a query per
    /// row — the N+1 this module would otherwise inflict on every screen that shows a person.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<UserId, IReadOnlyList<CountryExperience>>> ListByUsersAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);

    Task AddAsync(CountryExperience experience, CancellationToken cancellationToken = default);

    void Remove(CountryExperience experience);
}
