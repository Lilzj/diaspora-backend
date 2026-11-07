using Diaspora.Platform.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Auditing;

public sealed class EfAuditTrailReader : IAuditTrailReader
{
    public const int MaxLimit = 200;

    private readonly DiasporaDbContext _context;

    public EfAuditTrailReader(DiasporaDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditRecord>> ReadAsync(
        AuditTrailQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = _context.Set<AuditRecord>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityId = query.EntityId.Trim();
            records = records.Where(record => record.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            var actor = query.ActorUserId.Trim();
            records = records.Where(record => record.ActorUserId == actor);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            records = records.Where(record => record.Action.StartsWith(action));
        }

        if (query.From is { } from)
        {
            records = records.Where(record => record.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            records = records.Where(record => record.OccurredAt < to);
        }

        var limit = Math.Clamp(query.Limit, 1, MaxLimit);

        // Newest first. Orderable on SQLite too: the context stores DateTimeOffset there in an
        // order-preserving form.
        return await records
            .OrderByDescending(record => record.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
