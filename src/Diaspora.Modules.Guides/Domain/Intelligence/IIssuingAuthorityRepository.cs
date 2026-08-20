using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Domain.Intelligence;

/// <summary>
/// How the application layer reaches the register of issuing authorities.
/// <para>
/// <b><see cref="FindByVerificationCaseAsync"/> is the member that makes Trust integration work.</b>
/// A decision arrives from Trust carrying its own case identifier and nothing this module chose, so
/// the only way to find the authority it belongs to is to have stored that identifier and to be able
/// to look it up. B8 shipped without the equivalent and every approval arrived with nowhere to land.
/// </para>
/// </summary>
public interface IIssuingAuthorityRepository
{
    Task<IssuingAuthority?> GetAsync(
        IssuingAuthorityId id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.GetIncludingArchivedAsync" />
    Task<IssuingAuthority?> GetIncludingArchivedAsync(
        IssuingAuthorityId id, CancellationToken cancellationToken = default);

    /// <summary>Every authority issuing for one country, ordered by name.</summary>
    Task<IReadOnlyList<IssuingAuthority>> ListForCountryAsync(
        string countryCode,
        AuthorityType? type = null,
        bool verifiedOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>Several authorities by identifier, in one round trip. The N+1 guard.</summary>
    Task<IReadOnlyDictionary<IssuingAuthorityId, IssuingAuthority>> GetByIdsAsync(
        IReadOnlyCollection<IssuingAuthorityId> ids, CancellationToken cancellationToken = default);

    /// <summary>The authority a Trust decision belongs to, found by the case it opened.</summary>
    Task<IssuingAuthority?> FindByVerificationCaseAsync(
        Guid verificationCaseId, CancellationToken cancellationToken = default);

    Task AddAsync(IssuingAuthority authority, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.Remove" />
    void Remove(IssuingAuthority authority);
}
