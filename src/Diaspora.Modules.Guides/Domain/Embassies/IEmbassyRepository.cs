using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Domain.Embassies;

/// <summary>
/// How the application layer reaches the embassy directory.
/// <para>
/// <b>The two country codes are separate parameters on every listing, and neither defaults to the
/// other.</b> "Missions in Mauritius" and "missions serving Mauritians" are different lists with
/// almost no overlap, and a single <c>countryCode</c> parameter would silently answer whichever
/// question the last caller had in mind. Passing both as null lists everything, which is what a
/// global administrative screen wants.
/// </para>
/// </summary>
public interface IEmbassyRepository
{
    Task<Embassy?> GetAsync(EmbassyId id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.GetIncludingArchivedAsync" />
    Task<Embassy?> GetIncludingArchivedAsync(
        EmbassyId id, CancellationToken cancellationToken = default);

    /// <summary>One page of the directory.</summary>
    /// <param name="locatedIn">Missions physically in this country. Null means anywhere.</param>
    /// <param name="serving">Missions serving this country's nationals. Null means anybody.</param>
    /// <param name="verifiedOnly">Only missions Trust has confirmed.</param>
    /// <param name="text">Free text over the mission's name and city.</param>
    Task<CursorPagedResult<Embassy>> ListAsync(
        string? locatedIn,
        string? serving,
        bool verifiedOnly,
        string? text,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>The first <paramref name="limit"/> missions for a dashboard band, without a cursor.</summary>
    Task<IReadOnlyList<Embassy>> TakeAsync(
        string? locatedIn,
        string? serving,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many missions match, for a dashboard counter.</summary>
    Task<int> CountAsync(
        string? locatedIn, string? serving, CancellationToken cancellationToken = default);

    /// <summary>Several missions by identifier, in one round trip. The N+1 guard.</summary>
    Task<IReadOnlyList<Embassy>> GetByIdsAsync(
        IReadOnlyCollection<EmbassyId> ids, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIssuingAuthorityRepository.FindByVerificationCaseAsync" />
    Task<Embassy?> FindByVerificationCaseAsync(
        Guid verificationCaseId, CancellationToken cancellationToken = default);

    Task AddAsync(Embassy embassy, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.Remove" />
    void Remove(Embassy embassy);
}
