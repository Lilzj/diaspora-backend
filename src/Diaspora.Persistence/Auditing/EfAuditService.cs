using Diaspora.Platform.Auditing;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Microsoft.Extensions.Logging;

namespace Diaspora.Persistence.Auditing;

/// <summary>
/// Stores the audit trail in <c>platform.audit_records</c>, in the caller's transaction.
/// <para>
/// <b>It saves, and that is safe here.</b> The pipeline records after the handler and inside the
/// transaction a <c>[Transactional]</c> command opened, so the row commits or rolls back with the
/// action. A command without that attribute has already saved its own work, so this save writes
/// the row alone. A writer called from a domain-event handler is inside the context's dispatch
/// loop, which persists nested saves and keeps going: <see cref="DiasporaDbContext"/> handles that.
/// </para>
/// <para>
/// The log line stays as well, so an operator tailing logs sees the same thing the table holds.
/// </para>
/// </summary>
public sealed class EfAuditService : IAuditService
{
    private readonly DiasporaDbContext _context;
    private readonly IClock _clock;
    private readonly ILogger<EfAuditService> _logger;

    public EfAuditService(DiasporaDbContext context, IClock clock, ILogger<EfAuditService> logger)
    {
        _context = context;
        _clock = clock;
        _logger = logger;
    }

    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _logger.LogInformation(
            "Audit: {Action} on {EntityType}:{EntityId} by {ActorId}",
            entry.Action, entry.EntityType, entry.EntityId, entry.ActorId ?? "System");

        var tracked = _context.Set<AuditRecord>().Add(AuditRecord.From(entry, _clock.UtcNow));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Not left in the change tracker: a row that could not be written would otherwise be
            // retried by the next save the request makes, and fail that one too.
            tracked.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            throw;
        }
    }
}
