using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence;

/// <summary>
/// Answers "who did this" from the recognised-activity ledger.
/// <para>
/// <b>A narrow read that exists so this module does not reference another one for a Guid.</b> B8's
/// featured contract names the guide but not its author; the author is a fact this module already
/// recorded when the guide was published, so it is read from here rather than fetched from B8.
/// </para>
/// <para>
/// Its own type rather than a method on <c>ICreatorActivityRepository</c>, because it is not a
/// repository operation on the aggregate — it is a lookup across the ledger, and putting it on the
/// repository would invite the next one.
/// </para>
/// </summary>
public sealed class CreatorActivityLookup
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorActivityLookup(DiasporaDbContext dbContext) => _dbContext = dbContext;

    /// <summary>
    /// The member who owns a subject, from the first entry that named it.
    /// <para>
    /// Null when nothing recorded it — a guide published before this module existed, for instance —
    /// and the caller does nothing rather than guessing.
    /// </para>
    /// </summary>
    public async Task<Guid?> UserForSubjectAsync(
        string subjectType, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Set<CreatorActivityEntry>()
            .AsNoTracking()
            .Where(entry => entry.SubjectType == subjectType && entry.SubjectId == subjectId)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => entry.UserId)
            .Take(1)
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : rows[0];
    }
}
