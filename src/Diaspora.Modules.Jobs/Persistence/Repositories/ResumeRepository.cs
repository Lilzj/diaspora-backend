using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Resumes;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for résumé versions.
/// <para>
/// <see cref="GetForCandidateAsync"/> returns tracked entities deliberately: moving the default
/// flag means clearing it on every other version, and that has to happen inside one unit of work
/// or a candidate ends up with two defaults or none.
/// </para>
/// </summary>
public sealed class ResumeRepository : IResumeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ResumeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Resume> Resumes => _dbContext.Set<Resume>();

    public async Task<Resume?> GetAsync(
        ResumeId id, CancellationToken cancellationToken = default) =>
        await Resumes.FirstOrDefaultAsync(resume => resume.Id == id, cancellationToken);

    public async Task<Resume?> GetOwnedAsync(
        ResumeId id, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Ownership in the predicate, so "not yours" and "does not exist" are one query rather
        // than two — and so a caller cannot forget the second.
        return await Resumes.FirstOrDefaultAsync(
            resume => resume.Id == id && resume.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Resume>> GetForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        // Tracked: moving the default flag touches every row.
        return await Resumes
            .Where(resume => resume.CandidateId == candidateId)
            .OrderByDescending(resume => resume.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Resume?> GetDefaultAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        return await Resumes
            .FirstOrDefaultAsync(
                resume => resume.CandidateId == candidateId && resume.IsDefault,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Resume>> GetManyAsync(
        IReadOnlyCollection<ResumeId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // Whole identifiers: `.Value` has no column of its own, so EF cannot translate it.
        var keys = ids.ToList();

        return await Resumes.AsNoTracking()
            .Where(resume => keys.Contains(resume.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountForCandidateAsync(
        CandidateProfileId candidateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        return await Resumes.CountAsync(
            resume => resume.CandidateId == candidateId, cancellationToken);
    }

    public async Task AddAsync(Resume resume, CancellationToken cancellationToken = default) =>
        await Resumes.AddAsync(resume, cancellationToken);

    public void Remove(Resume resume) => Resumes.Remove(resume);
}
