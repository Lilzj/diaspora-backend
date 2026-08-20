using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.OAuth;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Domain.Webhooks;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Integrations.Persistence.Repositories;

/// <summary>
/// Staging that cannot turn an insert into an update.
/// <para>
/// The platform's recorded lesson, applied on this module's first day: calling <c>Update</c> on an
/// aggregate the same unit of work has just <c>Add</c>ed flips it from <c>Added</c> to
/// <c>Modified</c>, EF skips the parent INSERT, and the failure surfaces as a foreign-key violation
/// naming a child table several frames from the cause. The exchange handler is exactly that shape —
/// create-or-find a connection, then immediately apply a grant to it.
/// </para>
/// </summary>
internal static class TrackingSafety
{
    public static void StageUpdate<TEntity>(this DiasporaDbContext dbContext, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entity);

        if (dbContext.Entry(entity).State is EntityState.Added)
        {
            return;
        }

        dbContext.Set<TEntity>().Update(entity);
    }
}

/// <summary>
/// The connection registry.
/// <para>
/// <b>Every read filters on the owner pair, except one.</b> The exception —
/// <see cref="FindByExternalAccountAsync"/> — exists because a webhook has no session, and it is
/// reachable only from the callback handler behind a verified signature.
/// </para>
/// </summary>
public sealed class IntegrationConnectionRepository : IIntegrationConnectionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public IntegrationConnectionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<IntegrationConnection> Connections => _dbContext.Set<IntegrationConnection>();

    public async Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        string? providerDomain = null,
        CancellationToken cancellationToken = default)
    {
        var query = Connections
            .Where(row => row.OwnerKind == ownerKind && row.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(providerDomain))
        {
            query = query.Where(row => row.ProviderDomain == providerDomain);
        }

        return await query
            .OrderBy(row => row.Provider)
            .ToListAsync(cancellationToken);
    }

    public Task<IntegrationConnection?> FindAsync(
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        string provider,
        CancellationToken cancellationToken = default) =>
        Connections.FirstOrDefaultAsync(
            row => row.OwnerKind == ownerKind && row.OwnerId == ownerId && row.Provider == provider,
            cancellationToken);

    public Task<IntegrationConnection?> FindByExternalAccountAsync(
        string provider, string externalAccountId, CancellationToken cancellationToken = default) =>
        Connections.FirstOrDefaultAsync(
            row => row.Provider == provider && row.ExternalAccountId == externalAccountId,
            cancellationToken);

    public async Task<Guid?> FindConflictingOwnerAsync(
        string provider,
        string externalAccountId,
        Guid excludingOwnerId,
        CancellationToken cancellationToken = default)
    {
        // A disconnected row is not a conflict. Somebody who linked an account, disconnected it and
        // then linked it to a different Diaspora account has done nothing wrong, and refusing them
        // would make a disconnection permanent in a way nothing tells them about.
        var owner = await Connections
            .Where(row =>
                row.Provider == provider
                && row.ExternalAccountId == externalAccountId
                && row.OwnerId != excludingOwnerId
                && row.Status != IntegrationConnectionStatus.Disconnected)
            .Select(row => (Guid?)row.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        return owner;
    }

    public async Task<IReadOnlyList<IntegrationConnection>> ListExpiringAsync(
        DateTimeOffset now,
        TimeSpan window,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var horizon = now + window;

        return await Connections
            .Where(row =>
                row.Status == IntegrationConnectionStatus.Connected
                && row.ExpiresAt != null
                && row.ExpiresAt > now
                && row.ExpiresAt <= horizon)
            .OrderBy(row => row.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public void Add(IntegrationConnection connection) => Connections.Add(connection);

    public void Update(IntegrationConnection connection) => _dbContext.StageUpdate(connection);
}

/// <summary>Authorization requests, looked up by the hash of a state value.</summary>
public sealed class OAuthAuthorizationRequestRepository : IOAuthAuthorizationRequestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OAuthAuthorizationRequestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<OAuthAuthorizationRequest> Requests => _dbContext.Set<OAuthAuthorizationRequest>();

    public Task<OAuthAuthorizationRequest?> FindByStateAsync(
        string state, CancellationToken cancellationToken = default)
    {
        // Hashed here so no caller has to remember to. A lookup by a plaintext state would match
        // nothing and read as "invalid state" rather than as the bug it is.
        var hash = OAuthAuthorizationRequest.HashState(state);

        return Requests.FirstOrDefaultAsync(row => row.StateHash == hash, cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default)
    {
        var expired = await Requests
            .Where(row => row.ExpiresAt <= olderThan)
            .OrderBy(row => row.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        Requests.RemoveRange(expired);

        return expired.Count;
    }

    public void Add(OAuthAuthorizationRequest request) => Requests.Add(request);

    public void Update(OAuthAuthorizationRequest request) => _dbContext.StageUpdate(request);
}

/// <summary>The source-to-external mapping.</summary>
public sealed class CalendarEventLinkRepository : ICalendarEventLinkRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CalendarEventLinkRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CalendarEventLink> Links => _dbContext.Set<CalendarEventLink>();

    public Task<CalendarEventLink?> FindAsync(
        IntegrationConnectionId connectionId,
        string calendarId,
        string sourceId,
        CancellationToken cancellationToken = default) =>
        Links.FirstOrDefaultAsync(
            row => row.ConnectionId == connectionId
                && row.CalendarId == calendarId
                && row.SourceId == sourceId,
            cancellationToken);

    public Task<CalendarEventLink?> FindByExternalEventAsync(
        IntegrationConnectionId connectionId,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        Links.FirstOrDefaultAsync(
            row => row.ConnectionId == connectionId && row.ExternalEventId == externalEventId,
            cancellationToken);

    public async Task<IReadOnlyList<CalendarEventLink>> ListForConnectionAsync(
        IntegrationConnectionId connectionId, CancellationToken cancellationToken = default) =>
        await Links
            .Where(row => row.ConnectionId == connectionId)
            .OrderBy(row => row.WrittenAt)
            .ToListAsync(cancellationToken);

    public void Add(CalendarEventLink link) => Links.Add(link);

    public void Update(CalendarEventLink link) => _dbContext.StageUpdate(link);

    public void Remove(CalendarEventLink link) => Links.Remove(link);
}

/// <summary>The member's calendar preferences.</summary>
public sealed class IntegrationCalendarSettingsRepository : IIntegrationCalendarSettingsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public IntegrationCalendarSettingsRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<IntegrationCalendarSettings> Settings =>
        _dbContext.Set<IntegrationCalendarSettings>();

    public Task<IntegrationCalendarSettings?> FindAsync(
        Guid ownerId, CancellationToken cancellationToken = default) =>
        Settings.FirstOrDefaultAsync(row => row.OwnerId == ownerId, cancellationToken);

    public void Add(IntegrationCalendarSettings settings) => Settings.Add(settings);

    public void Update(IntegrationCalendarSettings settings) => _dbContext.StageUpdate(settings);
}

/// <summary>Synchronisation runs.</summary>
public sealed class IntegrationSyncRunRepository : IIntegrationSyncRunRepository
{
    private readonly DiasporaDbContext _dbContext;

    public IntegrationSyncRunRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<IntegrationSyncRun> Runs => _dbContext.Set<IntegrationSyncRun>();

    public async Task<IntegrationSyncRun?> FindLiveAsync(
        Guid ownerId,
        string provider,
        string? calendarId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var floor = now - IntegrationSyncRun.StaleAfter;

        return await Runs
            .Where(row =>
                row.OwnerId == ownerId
                && row.Provider == provider
                && row.CalendarId == calendarId
                && row.Phase == SyncPhase.Syncing
                && row.StartedAt > floor)
            .OrderByDescending(row => row.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<IntegrationSyncRun?> FindLatestAsync(
        Guid ownerId,
        string provider,
        string? calendarId,
        CancellationToken cancellationToken = default) =>
        Runs
            .Where(row =>
                row.OwnerId == ownerId
                && row.Provider == provider
                && (calendarId == null || row.CalendarId == calendarId))
            .OrderByDescending(row => row.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<IntegrationSyncRun>> ListStaleAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default)
    {
        var floor = now - IntegrationSyncRun.StaleAfter;

        return await Runs
            .Where(row => row.Phase == SyncPhase.Syncing && row.StartedAt <= floor)
            .OrderBy(row => row.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteCompletedBeforeAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default)
    {
        var stale = await Runs
            .Where(row => row.CompletedAt != null && row.CompletedAt <= olderThan)
            .OrderBy(row => row.CompletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        Runs.RemoveRange(stale);

        return stale.Count;
    }

    public void Add(IntegrationSyncRun run) => Runs.Add(run);

    public void Update(IntegrationSyncRun run) => _dbContext.StageUpdate(run);
}

/// <summary>Received callbacks. Append-only; there is no Update.</summary>
public sealed class ProcessedIntegrationWebhookRepository : IProcessedIntegrationWebhookRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProcessedIntegrationWebhookRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<ProcessedIntegrationWebhook> Received =>
        _dbContext.Set<ProcessedIntegrationWebhook>();

    public Task<ProcessedIntegrationWebhook?> FindAsync(
        string provider, string providerEventId, CancellationToken cancellationToken = default) =>
        Received.FirstOrDefaultAsync(
            row => row.Provider == provider && row.ProviderEventId == providerEventId,
            cancellationToken);

    public async Task<int> DeleteReceivedBeforeAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default)
    {
        var stale = await Received
            .Where(row => row.ReceivedAt <= olderThan)
            .OrderBy(row => row.ReceivedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        Received.RemoveRange(stale);

        return stale.Count;
    }

    public void Add(ProcessedIntegrationWebhook record) => Received.Add(record);
}
