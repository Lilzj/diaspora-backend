using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Media.Persistence.Repositories;

/// <summary>
/// EF Core repository for upload sessions.
/// <para>
/// Always with the parts, because every operation reads them: receiving one checks for a
/// duplicate, resuming reports which are missing, completing reassembles them in order.
/// </para>
/// </summary>
public sealed class UploadSessionRepository : IUploadSessionRepository
{
    /// <summary>
    /// The states a session can still be worked on from. Kept as a field rather than inlined
    /// because three queries share it and a fourth that disagreed would show a member an
    /// upload the quota check did not count.
    /// </summary>
    private static readonly UploadSessionStatus[] OpenStates =
    [
        UploadSessionStatus.Queued,
        UploadSessionStatus.Uploading,
        UploadSessionStatus.Paused
    ];

    private readonly DiasporaDbContext _dbContext;

    public UploadSessionRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<UploadSession> Sessions => _dbContext.Set<UploadSession>();

    public async Task<UploadSession?> GetAsync(
        UploadSessionId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Sessions
            .Include(session => session.Chunks)
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<UploadSession>> ListOpenForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Sessions
            .Include(session => session.Chunks)
            .Where(session =>
                session.StartedBy == userId && OpenStates.Contains(session.Status))
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountOpenForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Sessions.CountAsync(
            session => session.StartedBy == userId && OpenStates.Contains(session.Status),
            cancellationToken);
    }

    public async Task<IReadOnlyList<UploadSession>> FindExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Sessions
            .Include(session => session.Chunks)
            .Where(session => OpenStates.Contains(session.Status) && session.ExpiresAt < asOf)
            .OrderBy(session => session.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(UploadSession session, CancellationToken cancellationToken = default) =>
        await Sessions.AddAsync(session, cancellationToken);

    public void Remove(UploadSession session) => Sessions.Remove(session);
}
