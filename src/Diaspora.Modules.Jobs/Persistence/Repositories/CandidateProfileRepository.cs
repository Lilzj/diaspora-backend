using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for candidate profiles.
/// <para>
/// <b><see cref="SearchAsync"/> applies the visibility rules itself, and that is the single most
/// important thing in this file.</b> A candidate's chosen visibility is meaningless unless the
/// query enforces it, and enforcing it correctly means resolving the searching employer's actual
/// trust mark rather than believing a flag a caller passed in. Every path that returns more than
/// one candidate goes through here; there is deliberately no unfiltered listing method for
/// something else to call by mistake.
/// </para>
/// </summary>
public sealed class CandidateProfileRepository : ICandidateProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CandidateProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CandidateProfile> Candidates => _dbContext.Set<CandidateProfile>();

    public async Task<CandidateProfile?> GetAsync(
        CandidateProfileId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Candidates)
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<CandidateProfile?> GetForReadAsync(
        CandidateProfileId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Candidates.AsNoTracking())
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<CandidateProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await FullGraph(Candidates)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public async Task<CandidateProfile?> GetByUserForReadAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await FullGraph(Candidates.AsNoTracking())
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateProfile>> GetManyAsync(
        IReadOnlyCollection<CandidateProfileId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Whole identifiers: `.Value` has no column of its own, so EF cannot translate it.
        var keys = ids.ToList();

        // Skills only: an applicant list shows a headline and chips, not a full work history.
        return await Candidates.AsNoTracking()
            .Include(profile => profile.Skills)
            .Where(profile => keys.Contains(profile.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Candidates.AnyAsync(profile => profile.UserId == userId, cancellationToken);
    }

    public async Task<CursorPagedResult<CandidateProfile>> SearchAsync(
        CandidateSearchFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Candidates.AsNoTracking()
            .Include(profile => profile.Skills)
            .Include(profile => profile.WorkAuthorizations)
            .Include(profile => profile.PreferredLocations)
            .Include(profile => profile.Languages);

        var filtered = await ApplyVisibilityAsync(query, filter, cancellationToken);

        filtered = ApplyCriteria(filtered, filter);

        var parsed = JobCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            // Excludes what the previous page already returned at this timestamp rather than
            // everything sharing it. See CursorBoundary.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, CandidateProfileId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            filtered = filtered.Where(profile =>
                profile.CreatedAt < after
                || (profile.CreatedAt == after && !exhausted && !seen.Contains(profile.Id)));
        }

        // Newest profile first, with the creation instant as the cursor. There is no relevance
        // score here and inventing one would be dishonest: candidate search is a filter.
        filtered = filtered.OrderByDescending(profile => profile.CreatedAt)
            .ThenByDescending(profile => profile.Id);

        var effectiveLimit = Math.Clamp(limit, 1, JobLimits.MaxPageSize);
        var rows = await filtered.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? JobCursor.FromTimestamp(
                rows[^1].CreatedAt,
                CursorBoundary.Key(
                    rows,
                    row => row.CreatedAt,
                    row => row.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<CandidateProfile>.Create(rows, next, hasMore);
    }

    public async Task AddAsync(
        CandidateProfile profile, CancellationToken cancellationToken = default) =>
        await Candidates.AddAsync(profile, cancellationToken);

    public void Remove(CandidateProfile profile) => Candidates.Remove(profile);

    private static IQueryable<CandidateProfile> FullGraph(IQueryable<CandidateProfile> query) =>
        query
            .Include(profile => profile.Skills)
            .Include(profile => profile.Experience)
            .Include(profile => profile.Education)
            .Include(profile => profile.Certifications)
            .Include(profile => profile.Languages)
            .Include(profile => profile.WorkAuthorizations)
            .Include(profile => profile.PreferredLocations)
            .Include(profile => profile.Portfolio);

    /// <summary>
    /// Narrows the query to profiles this caller is actually allowed to see.
    /// <para>
    /// The employer's trust mark is resolved from the database rather than taken on trust from
    /// the filter, because <see cref="CandidateVisibility.VerifiedEmployers"/> is a promise the
    /// platform made to the candidate — and a promise enforced against a caller-supplied boolean
    /// is not enforced at all.
    /// </para>
    /// </summary>
    private async Task<IQueryable<CandidateProfile>> ApplyVisibilityAsync(
        IQueryable<CandidateProfile> query,
        CandidateSearchFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.IsStaff)
        {
            return query;
        }

        if (filter.SearchingEmployerId is not { } employerId)
        {
            // No employer context: only what the candidate made outright public.
            return query.Where(profile => profile.Visibility == CandidateVisibility.Public);
        }

        var isVerifiedEmployer = await _dbContext.Set<EmployerProfile>()
            .AnyAsync(
                employer => employer.Id == employerId
                    && employer.Status == EmployerStatus.Active
                    && employer.TrustMark.State == JobVerificationState.Verified,
                cancellationToken);

        if (isVerifiedEmployer)
        {
            return query.Where(profile =>
                profile.Visibility == CandidateVisibility.Public
                || profile.Visibility == CandidateVisibility.Members
                || profile.Visibility == CandidateVisibility.VerifiedEmployers);
        }

        return query.Where(profile =>
            profile.Visibility == CandidateVisibility.Public
            || profile.Visibility == CandidateVisibility.Members);
    }

    private static IQueryable<CandidateProfile> ApplyCriteria(
        IQueryable<CandidateProfile> query, CandidateSearchFilter filter)
    {
        if (filter.OpenToWorkOnly)
        {
            query = query.Where(profile => profile.IsOpenToWork);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.PreferredCountryCode))
        {
            var country = filter.PreferredCountryCode.Trim().ToUpperInvariant();

            query = query.Where(profile =>
                profile.PreferredLocations.Any(location => location.CountryCode == country));
        }

        // Only candidates who can work there without the employer sponsoring.
        if (!string.IsNullOrWhiteSpace(filter.AuthorizedToWorkIn))
        {
            var country = filter.AuthorizedToWorkIn.Trim().ToUpperInvariant();

            query = query.Where(profile =>
                profile.WorkAuthorizations.Any(authorization =>
                    authorization.CountryCode == country
                    && authorization.Kind != WorkAuthorizationKind.RequiresSponsorship));
        }

        if (filter.SkillKeys is { Count: > 0 })
        {
            var skills = filter.SkillKeys.ToList();

            query = query.Where(profile =>
                profile.Skills.Any(skill => skills.Contains(skill.SkillKey)));
        }

        if (filter.LanguageCodes is { Count: > 0 })
        {
            var languages = filter.LanguageCodes.ToList();

            query = query.Where(profile =>
                profile.Languages.Any(language => languages.Contains(language.Code)));
        }

        if (filter.MinimumExperience is { } minimumExperience)
        {
            query = query.Where(profile => profile.ExperienceLevel >= minimumExperience);
        }

        if (filter.MinimumEducation is { } minimumEducation)
        {
            query = query.Where(profile => profile.HighestEducation >= minimumEducation);
        }

        if (filter.RemotePreference is { } remotePreference)
        {
            query = query.Where(profile => profile.RemotePreference == remotePreference);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();

            query = query.Where(profile =>
                EF.Functions.Like(profile.Headline.Headline, $"%{term}%")
                || (profile.Headline.Summary != null
                    && EF.Functions.Like(profile.Headline.Summary, $"%{term}%")));
        }

        return query;
    }
}
