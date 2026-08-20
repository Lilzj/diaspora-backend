using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Extensibility.Providers;

namespace Diaspora.Modules.Integrations.Application.Mapping;

/// <summary>
/// Composes what the client renders from what this module stores.
/// <para>
/// <b>Static, because none of it depends on anything.</b> Every method is a pure projection from a
/// persisted row plus the adapter's declared capabilities, which is what makes the status vocabulary
/// testable without a host.
/// </para>
/// <para>
/// <b>The interesting work is <see cref="ToStatus"/>.</b> The client has thirteen statuses and this
/// module stores four, and the gap is deliberate: <c>SYNCING</c> and <c>SYNCED</c> are facts about a
/// sync run, <c>CONNECTING</c> is a fact about a browser, and <c>DEVELOPMENT</c> and
/// <c>BACKEND_GAP</c> are facts about a build. Composing them here is what stops the persisted enum
/// growing members it has no business owning.
/// </para>
/// </summary>
public static class IntegrationMapper
{
    // ── Status, as the client's vocabulary ──────────────────────────────────────────────────

    public const string StatusNotConnected = "NOT_CONNECTED";
    public const string StatusConnected = "CONNECTED";
    public const string StatusSyncing = "SYNCING";
    public const string StatusSynced = "SYNCED";
    public const string StatusReauthRequired = "REAUTH_REQUIRED";
    public const string StatusDisconnected = "DISCONNECTED";
    public const string StatusError = "ERROR";
    public const string StatusUnavailable = "UNAVAILABLE";

    // ── Mode ────────────────────────────────────────────────────────────────────────────────

    /// <summary>A certified Diaspora route answered. What this module always claims when it can.</summary>
    public const string ModeBackendConnected = "BACKEND_CONNECTED";

    /// <summary>Nothing answered because this build has no credentials.</summary>
    public const string ModeNotConfigured = "NOT_CONFIGURED";

    /// <summary>
    /// Projects a connection.
    /// <para>
    /// <paramref name="latestSync"/> is the live or most recent run for the same provider, which is
    /// what turns a stored <c>Connected</c> into the client's <c>SYNCING</c> or <c>SYNCED</c>.
    /// Passing null is legitimate and yields <c>CONNECTED</c> — the weaker, true claim, which is the
    /// same degradation the client's own <c>withSync()</c> applies.
    /// </para>
    /// </summary>
    public static IntegrationConnectionResponse ToResponse(
        IntegrationConnection connection,
        IntegrationSyncRun? latestSync,
        bool providerConfigured,
        IReadOnlyCollection<ExternalCalendarCapability> calendarCapabilities)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return new IntegrationConnectionResponse
        {
            Provider = connection.Provider,
            Domain = connection.ProviderDomain,
            Status = ToStatus(connection, latestSync, providerConfigured),
            Mode = providerConfigured ? ModeBackendConnected : ModeNotConfigured,
            Account = ToAccount(connection),
            GrantedCapabilities = ToCapabilities(connection, providerConfigured, calendarCapabilities),
            Scopes = connection.Scopes,
            ExpiresAt = connection.ExpiresAt,

            // The last time a call through this grant *worked*, not the last time one was attempted.
            // The client's own contract: "null when it has never run — distinct from ran and found
            // nothing".
            LastSyncedAt = latestSync?.CompletedAt ?? connection.LastSuccessAt,
            LastError = ToError(connection.LastFailure),
            RevokedAtProvider = connection.RevokedAtProvider,
            ObservedAt = connection.ObservedAt
        };
    }

    /// <summary>
    /// The client's status for a stored connection.
    /// <para>
    /// The order of the checks is the whole of the logic and it is deliberate:
    /// </para>
    /// <list type="number">
    /// <item><b>Reauthorization first.</b> A grant that needs the member needs the member whatever a
    /// sync run says, and a <c>SYNCING</c> badge over a dead grant is the most misleading pair
    /// available here.</item>
    /// <item><b>Disconnected before not-configured.</b> A member who disconnected should see that
    /// they disconnected, not a deployment-level message about a provider.</item>
    /// <item><b>Not-configured before connected.</b> A stored grant whose adapter has since been
    /// removed cannot be used, and claiming <c>CONNECTED</c> for it would offer a button that
    /// answers 400.</item>
    /// <item><b>Sync last</b>, because it only refines a connection that is otherwise healthy.</item>
    /// </list>
    /// </summary>
    public static string ToStatus(
        IntegrationConnection connection, IntegrationSyncRun? latestSync, bool providerConfigured)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.Status is IntegrationConnectionStatus.ReauthorizationRequired)
        {
            return StatusReauthRequired;
        }

        if (connection.Status is IntegrationConnectionStatus.Disconnected)
        {
            // A row that has never held a grant reads as never-connected rather than disconnected:
            // the two look identical in the database and mean different things to a member.
            return connection.ConnectedAt is null ? StatusNotConnected : StatusDisconnected;
        }

        if (!providerConfigured)
        {
            return StatusUnavailable;
        }

        if (connection.Status is IntegrationConnectionStatus.Error)
        {
            return StatusError;
        }

        return latestSync?.Phase switch
        {
            SyncPhase.Syncing => StatusSyncing,

            // `SYNCED` requires a confirmed completion, and the guard is the same one the client
            // applies to itself: "phase: 'synced' with a null lastSyncedAt produces CONNECTED, so an
            // adapter that forgot the timestamp degrades to the weaker, true claim."
            SyncPhase.Synced when latestSync.CompletedAt is not null => StatusSynced,
            _ => StatusConnected
        };
    }

    public static IntegrationAccountResponse? ToAccount(IntegrationConnection connection) =>
        connection.ExternalAccountId is { } externalId && connection.AccountLabel is { } label
            ? new IntegrationAccountResponse
            {
                ExternalId = externalId,
                AccountLabel = label,
                IsPrivateRelay = connection.IsPrivateRelay,
                AvatarUrl = connection.AvatarUrl
            }
            : null;

    public static IntegrationErrorSummaryResponse? ToError(ConnectionFailure? failure) =>
        failure is null
            ? null
            : new IntegrationErrorSummaryResponse
            {
                Kind = ToErrorKind(failure.Kind),
                Message = failure.Message,
                OccurredAt = failure.OccurredAt,
                CorrelationId = failure.CorrelationId
            };

    /// <summary>
    /// A failure kind, as one of the client's thirteen <c>IntegrationErrorKind</c> values.
    /// <para>
    /// The client switches on these and nothing else, which is what stops a screen ever having to
    /// understand a provider's own error code.
    /// </para>
    /// </summary>
    public static string ToErrorKind(IntegrationFailureKind kind) => kind switch
    {
        IntegrationFailureKind.NotConfigured => "ConfigurationMissing",
        IntegrationFailureKind.Unsupported => "UnsupportedCapability",
        IntegrationFailureKind.Unauthorized => "ExpiredCredential",
        IntegrationFailureKind.InvalidRequest => "InvalidRequest",
        IntegrationFailureKind.RateLimited => "RateLimited",
        IntegrationFailureKind.Unavailable => "ProviderUnavailable",
        _ => "UnknownError"
    };

    /// <summary>
    /// What is usable right now.
    /// <para>
    /// <b>Intersected, never assumed.</b> A connection reports a capability only when three things
    /// hold: the grant is usable, this deployment has an adapter, and the adapter declares the
    /// operation. A member who declined a write scope on a Google consent screen does not see a
    /// write capability, because the adapter's <c>Capabilities</c> is what the adapter can do and the
    /// grant is what it may do with it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ToCapabilities(
        IntegrationConnection connection,
        bool providerConfigured,
        IReadOnlyCollection<ExternalCalendarCapability> calendarCapabilities)
    {
        if (!providerConfigured || !connection.IsUsable)
        {
            return [];
        }

        if (!ProviderKey.TryParse(connection.Provider, out var key))
        {
            return [];
        }

        if (key.IsIdentity)
        {
            // Identity's capabilities do not depend on a provider scope: a linked external account
            // can always be unlinked, and can always be re-authorized. Sign-in is deliberately
            // absent — see the OAuth handler's refusal of `signIn`.
            return ["identity.link", "identity.unlink", "identity.reauthenticate"];
        }

        if (!key.IsCalendar)
        {
            return [];
        }

        var capabilities = new List<string>();

        foreach (var capability in calendarCapabilities)
        {
            switch (capability)
            {
                case ExternalCalendarCapability.List:
                    capabilities.Add("calendar.list");
                    break;
                case ExternalCalendarCapability.CreateEvent:
                    capabilities.Add("calendar.event.create");

                    // One-way export follows from being able to create, and nothing else. Two-way
                    // is reported only from `ImportEvents` + `WatchChannel`, below.
                    capabilities.Add("calendar.sync.oneWay");
                    break;
                case ExternalCalendarCapability.UpdateEvent:
                    capabilities.Add("calendar.event.update");
                    break;
                case ExternalCalendarCapability.DeleteEvent:
                    capabilities.Add("calendar.event.delete");
                    break;
                case ExternalCalendarCapability.ImportEvents:
                    capabilities.Add("calendar.event.import");
                    break;
                case ExternalCalendarCapability.ReadEvent:
                case ExternalCalendarCapability.WatchChannel:
                default:
                    break;
            }
        }

        // Two-way needs both halves, and the register's rule is that until the watch channel exists
        // nothing may claim it. Requiring both here is that rule in the one place a client reads.
        if (calendarCapabilities.Contains(ExternalCalendarCapability.ImportEvents)
            && calendarCapabilities.Contains(ExternalCalendarCapability.WatchChannel))
        {
            capabilities.Add("calendar.sync.twoWay");
        }

        return [.. capabilities.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Projects a sync run, or the never-run state.
    /// <para>
    /// Null yields <c>never</c> with a null timestamp rather than a zeroed <c>synced</c>. "A
    /// synchronisation that has never happened" and "one that happened and moved nothing" render
    /// identically if this returns the wrong one, and they are opposite claims.
    /// </para>
    /// </summary>
    public static IntegrationSyncStateResponse ToSyncState(IntegrationSyncRun? run) =>
        run is null
            ? new IntegrationSyncStateResponse
            {
                Phase = "never",
                LastSyncedAt = null,
                ItemsSynced = 0,
                ItemsFailed = 0,
                FailureReasons = [],
                NeedsAttention = false
            }
            : new IntegrationSyncStateResponse
            {
                Phase = run.Phase switch
                {
                    SyncPhase.Syncing => "syncing",
                    SyncPhase.Synced => "synced",
                    SyncPhase.Partial => "partial",
                    SyncPhase.Failed => "failed",
                    _ => "never"
                },

                // Only a run that actually completed produces a `lastSyncedAt`. A failed run has a
                // `completedAt` and must not present it as the moment a sync succeeded.
                LastSyncedAt = run.Phase is SyncPhase.Synced or SyncPhase.Partial
                    ? run.CompletedAt
                    : null,
                StartedAt = run.Phase is SyncPhase.Syncing ? run.StartedAt : null,
                ItemsSynced = run.ItemsSynced,
                ItemsFailed = run.ItemsFailed,
                FailureReasons = run.FailureReasons,
                NeedsAttention = run.NeedsAttention,
                SyncId = run.Id.Value
            };

    public static CalendarSyncSettingsResponse ToSettings(IntegrationCalendarSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new CalendarSyncSettingsResponse
        {
            Enabled = settings.Enabled,
            DefaultCalendarId = settings.DefaultCalendarId,
            DefaultProvider = settings.DefaultProvider,
            AutoExportRsvps = settings.AutoExportRsvps,
            TwoWaySyncEnabled = settings.TwoWaySyncEnabled,
            SyncFrequency = settings.SyncFrequency,
            DefaultReminderMinutes = settings.DefaultReminderMinutes,
            TimeZoneBehaviour = settings.TimeZoneBehaviour,
            ConflictBehaviour = settings.ConflictBehaviour
        };
    }

    public static CalendarRefResponse ToCalendarRef(ExternalCalendar calendar, string provider) =>
        new()
        {
            Id = calendar.Id,
            Title = calendar.Title,
            Color = calendar.Color,
            AllowsModifications = calendar.AllowsModifications,
            IsPrimary = calendar.IsPrimary,
            Provider = provider,
            TimeZone = calendar.TimeZone
        };

    public static CalendarEventRefResponse ToEventRef(
        CalendarEventLink link, string provider, bool wasDeduplicated) =>
        new()
        {
            ExternalEventId = link.ExternalEventId,
            CalendarId = link.CalendarId,
            Provider = provider,
            WrittenAt = link.WrittenAt,
            ViewUrl = link.ViewUrl,
            WasDeduplicated = wasDeduplicated
        };
}
