using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.OAuth;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Domain.Webhooks;

namespace Diaspora.Modules.Integrations.Domain.Repositories;

/// <summary>
/// Connections, always by owner.
/// <para>
/// <b>Every read method takes the owner pair, and there is no overload that does not.</b> §11 and
/// §44 both require that one owner can never reach another's credential, and the strongest available
/// enforcement is a repository with no method that could return the wrong one. A
/// <c>FindAsync(connectionId)</c> would be the natural convenience and is exactly how an
/// authorization check comes to be a thing a handler remembers rather than a thing the shape
/// requires.
/// </para>
/// </summary>
public interface IIntegrationConnectionRepository
{
    /// <summary>Every connection this owner has, including disconnected ones.</summary>
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        string? providerDomain = null,
        CancellationToken cancellationToken = default);

    /// <summary>One owner's connection to one provider, or null.</summary>
    Task<IntegrationConnection?> FindAsync(
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The connection a provider's callback names, without an owner.
    /// <para>
    /// The one method that crosses owners, and it exists because a webhook has no session — the
    /// provider names an external account and the platform must resolve it. It matches on
    /// (provider, external account id), which is the pair a provider actually sends, and it is
    /// reachable only from the webhook handler behind a verified signature.
    /// </para>
    /// </summary>
    Task<IntegrationConnection?> FindByExternalAccountAsync(
        string provider,
        string externalAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether an external account is attached to somebody other than this owner.
    /// <para>
    /// The 409 the register calls "a security control, not a niceness" — "without it, linking is an
    /// account merge anybody can trigger". Returns the conflicting owner rather than a boolean so
    /// the handler can log the collision without a second query.
    /// </para>
    /// </summary>
    Task<Guid?> FindConflictingOwnerAsync(
        string provider,
        string externalAccountId,
        Guid excludingOwnerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connections whose grant lapses inside <paramref name="window"/> and that nobody has been told
    /// about. For the expiry sweep.
    /// </summary>
    Task<IReadOnlyList<IntegrationConnection>> ListExpiringAsync(
        DateTimeOffset now,
        TimeSpan window,
        int limit,
        CancellationToken cancellationToken = default);

    void Add(IntegrationConnection connection);

    void Update(IntegrationConnection connection);
}

/// <summary>Authorization requests. Written at redirect, read once at exchange, swept after expiry.</summary>
public interface IOAuthAuthorizationRequestRepository
{
    /// <summary>
    /// By the hashed state.
    /// <para>
    /// Takes the raw state and hashes it here, so no caller has to remember to — a lookup by a
    /// plaintext state would silently match nothing and read as "invalid state" rather than as the
    /// bug it is.
    /// </para>
    /// </summary>
    Task<OAuthAuthorizationRequest?> FindByStateAsync(
        string state, CancellationToken cancellationToken = default);

    /// <summary>Requests past their expiry, for the retention sweep.</summary>
    Task<int> DeleteExpiredAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default);

    void Add(OAuthAuthorizationRequest request);

    void Update(OAuthAuthorizationRequest request);
}

/// <summary>The source-to-external mapping.</summary>
public interface ICalendarEventLinkRepository
{
    /// <summary>The link for one source in one calendar, or null. The duplicate-suppression lookup.</summary>
    Task<CalendarEventLink?> FindAsync(
        IntegrationConnectionId connectionId,
        string calendarId,
        string sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>By the provider's identifier, for a callback or a delete by external id.</summary>
    Task<CalendarEventLink?> FindByExternalEventAsync(
        IntegrationConnectionId connectionId,
        string externalEventId,
        CancellationToken cancellationToken = default);

    /// <summary>Everything written through one connection. For a disconnect and for erasure.</summary>
    Task<IReadOnlyList<CalendarEventLink>> ListForConnectionAsync(
        IntegrationConnectionId connectionId, CancellationToken cancellationToken = default);

    void Add(CalendarEventLink link);

    void Update(CalendarEventLink link);

    void Remove(CalendarEventLink link);
}

/// <summary>One member's integration preferences.</summary>
public interface IIntegrationCalendarSettingsRepository
{
    Task<IntegrationCalendarSettings?> FindAsync(
        Guid ownerId, CancellationToken cancellationToken = default);

    void Add(IntegrationCalendarSettings settings);

    void Update(IntegrationCalendarSettings settings);
}

/// <summary>Synchronisation runs.</summary>
public interface IIntegrationSyncRunRepository
{
    /// <summary>
    /// The live run for this key, or null.
    /// <para>
    /// The half of the concurrency guard that makes the common case correct and cheap. The other
    /// half is a distributed lease over the same key, taken by the handler.
    /// </para>
    /// </summary>
    Task<IntegrationSyncRun?> FindLiveAsync(
        Guid ownerId,
        string provider,
        string? calendarId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent run for this key, live or not. What the status route reads.</summary>
    Task<IntegrationSyncRun?> FindLatestAsync(
        Guid ownerId,
        string provider,
        string? calendarId,
        CancellationToken cancellationToken = default);

    /// <summary>Runs that claim to be in progress and are past <see cref="IntegrationSyncRun.StaleAfter"/>.</summary>
    Task<IReadOnlyList<IntegrationSyncRun>> ListStaleAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    /// <summary>Terminal runs older than the retention horizon.</summary>
    Task<int> DeleteCompletedBeforeAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default);

    void Add(IntegrationSyncRun run);

    void Update(IntegrationSyncRun run);
}

/// <summary>Received callbacks.</summary>
public interface IProcessedIntegrationWebhookRepository
{
    Task<ProcessedIntegrationWebhook?> FindAsync(
        string provider, string providerEventId, CancellationToken cancellationToken = default);

    /// <summary>Records older than the retention horizon. §42 has no exception for webhook logs.</summary>
    Task<int> DeleteReceivedBeforeAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default);

    void Add(ProcessedIntegrationWebhook record);
}
