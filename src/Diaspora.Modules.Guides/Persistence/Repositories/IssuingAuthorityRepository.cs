using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the register of issuing authorities.
/// <para>
/// The register owns no collections, so there is no include to forget — which is why this is the
/// shortest repository in the phase and why it is worth saying so: every other one here has a
/// <c>FullGraph</c> helper precisely because EF loads owned collections only when asked.
/// </para>
/// </summary>
public sealed class IssuingAuthorityRepository : IIssuingAuthorityRepository
{
    private readonly DiasporaDbContext _dbContext;

    public IssuingAuthorityRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<IssuingAuthority> Authorities => _dbContext.Set<IssuingAuthority>();

    public async Task<IssuingAuthority?> GetAsync(
        IssuingAuthorityId id, CancellationToken cancellationToken = default) =>
        await Authorities.FirstOrDefaultAsync(
            authority => authority.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IssuingAuthority?> GetIncludingArchivedAsync(
        IssuingAuthorityId id, CancellationToken cancellationToken = default) =>
        await Authorities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(authority => authority.Id == id, cancellationToken);

    public async Task<IReadOnlyList<IssuingAuthority>> ListForCountryAsync(
        string countryCode,
        AuthorityType? type = null,
        bool verifiedOnly = false,
        CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        if (country is null)
        {
            return [];
        }

        var query = Authorities
            .AsNoTracking()
            .Where(authority => authority.CountryCode == country);

        if (type is { } authorityType)
        {
            query = query.Where(authority => authority.AuthorityType == authorityType);
        }

        if (verifiedOnly)
        {
            query = query.Where(authority => authority.IsVerified);
        }

        return await query
            .OrderBy(authority => authority.Name)
            .ThenBy(authority => authority.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<IssuingAuthorityId, IssuingAuthority>> GetByIdsAsync(
        IReadOnlyCollection<IssuingAuthorityId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var distinct = ids.Distinct().ToList();

        if (distinct.Count == 0)
        {
            return new Dictionary<IssuingAuthorityId, IssuingAuthority>();
        }

        // IgnoreQueryFilters: a notice published under an authority that has since been archived
        // still has to render the name of who issued it. Hiding the row would show a live notice
        // with a blank source, which is worse than showing an archived one.
        var found = await Authorities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(authority => distinct.Contains(authority.Id))
            .ToListAsync(cancellationToken);

        return found.ToDictionary(authority => authority.Id);
    }

    /// <inheritdoc />
    public async Task<IssuingAuthority?> FindByVerificationCaseAsync(
        Guid verificationCaseId, CancellationToken cancellationToken = default) =>
        verificationCaseId == Guid.Empty
            ? null
            : await Authorities
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    authority => authority.VerificationCaseId == verificationCaseId,
                    cancellationToken);

    public async Task AddAsync(
        IssuingAuthority authority, CancellationToken cancellationToken = default) =>
        await Authorities.AddAsync(authority, cancellationToken);

    /// <inheritdoc cref="Domain.Intelligence.IOfficialNoticeRepository.Remove" />
    public void Remove(IssuingAuthority authority) => Authorities.Remove(authority);
}
