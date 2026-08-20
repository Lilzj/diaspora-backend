using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Residency;

/// <summary>The residency-projection persistence boundary.</summary>
public interface IVerifiedResidencyRepository
{
    Task<VerifiedResidency?> GetAsync(
        UserId userId, CountryCode countryCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VerifiedResidency>> ListByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Batched for the same reason as the experience repository's: one query per screen.</summary>
    Task<IReadOnlyDictionary<UserId, IReadOnlyList<VerifiedResidency>>> ListByUsersAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);

    Task AddAsync(VerifiedResidency residency, CancellationToken cancellationToken = default);
}
