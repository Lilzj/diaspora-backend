using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Domain.Visas;

/// <summary>
/// How the application layer reaches visa and immigration advisories.
/// <para>
/// <b>The <c>appliesTo</c> argument on the listing is nullable in a way that means three things, and
/// the middle one is the useful one.</b> Null asks for every advisory about the destination. A code
/// asks for the ones that apply to that nationality — which must include the general advisories that
/// apply to everybody, because a Nigerian passport holder is subject to both the Nigeria-specific
/// rules and the ones with no nationality named. A repository that answered only the exact match
/// would tell most members that no rules exist for them.
/// </para>
/// </summary>
public interface IVisaAdvisoryRepository
{
    Task<VisaAdvisory?> GetAsync(
        VisaAdvisoryId id, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.GetIncludingArchivedAsync" />
    Task<VisaAdvisory?> GetIncludingArchivedAsync(
        VisaAdvisoryId id, CancellationToken cancellationToken = default);

    /// <summary>One page of advisories for a destination.</summary>
    /// <param name="countryCode">The destination. Required — an advisory is always about one.</param>
    /// <param name="appliesTo">
    /// A nationality. Matches advisories naming it <em>and</em> those naming nobody, because both
    /// apply. Null asks for all of them regardless of nationality.
    /// </param>
    /// <param name="publishedOnly">Whether drafts are excluded. Readers always set this.</param>
    /// <param name="activeOnly">Whether advisories outside their effective window are excluded.</param>
    Task<CursorPagedResult<VisaAdvisory>> ListAsync(
        string countryCode,
        string? appliesTo,
        bool publishedOnly,
        bool activeOnly,
        DateTimeOffset asOf,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>The first <paramref name="limit"/> advisories for a dashboard band.</summary>
    Task<IReadOnlyList<VisaAdvisory>> TakeAsync(
        string countryCode,
        string? appliesTo,
        DateTimeOffset asOf,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many published advisories a country has, for a dashboard counter.</summary>
    Task<int> CountAsync(
        string countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    Task AddAsync(VisaAdvisory advisory, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IOfficialNoticeRepository.Remove" />
    void Remove(VisaAdvisory advisory);
}
