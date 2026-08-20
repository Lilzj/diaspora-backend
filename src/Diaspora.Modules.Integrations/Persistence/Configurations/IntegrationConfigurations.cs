using Diaspora.Modules.Integrations.Domain.Calendar;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.OAuth;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Domain.Webhooks;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Integrations.Persistence.Configurations;

/// <summary>The module's table names and column widths, in one place.</summary>
internal static class IntegrationsPersistenceConstants
{
    public const string ConnectionsTable = "integration_connections";
    public const string AuthorizationRequestsTable = "integration_oauth_requests";
    public const string CalendarLinksTable = "integration_calendar_links";
    public const string CalendarSettingsTable = "integration_calendar_settings";
    public const string SyncRunsTable = "integration_sync_runs";
    public const string WebhooksTable = "integration_webhook_events";

    /// <summary>Enums are stored as strings, so a member added later reads correctly on old rows.</summary>
    public const int EnumLength = 32;

    /// <summary>A dotted provider key. Capped and validated on the way in — see <c>ProviderKey</c>.</summary>
    public const int ProviderLength = 64;

    /// <summary>The domain half. Short by construction.</summary>
    public const int DomainLength = 32;

    public const int CorrelationLength = 128;
}

/// <summary>The module's identifier converters. The platform's generic one, never a second encoding.</summary>
internal static class IntegrationValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}

/// <summary>
/// Maps the connection registry.
/// <para>
/// <b>Five indexes, and each answers a query this module actually makes.</b> The unique one is the
/// registry's whole integrity rule: one owner has at most one connection per provider. Without it,
/// a race between two exchanges produces two grants for one account and the disconnect path drops
/// one of them.
/// </para>
/// <para>
/// <b>The scope list and both ciphers are field-only properties.</b> <c>Scopes</c> is a computed
/// projection with no setter, so EF cannot see it; the backing fields are mapped by name, which is
/// the same shape B4's <c>_spokenLanguages</c> already uses. Mapping the computed property instead
/// would silently persist nothing.
/// </para>
/// <para>
/// <b>Not <c>ISoftDeletable</c>, and that is a decision.</b> A disconnection is a state on the row —
/// it has to be, because the register requires disconnecting twice to answer the same thing, and a
/// soft-deleted row disappears behind the global query filter and would make the second call a
/// create. This platform has paid for that exact interaction once already.
/// </para>
/// </summary>
internal sealed class IntegrationConnectionConfiguration
    : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(IntegrationsPersistenceConstants.ConnectionsTable, IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<IntegrationConnectionId>())
            .ValueGeneratedNever();

        builder.Property(row => row.OwnerKind)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.Provider)
            .HasMaxLength(IntegrationsPersistenceConstants.ProviderLength)
            .IsRequired();

        builder.Property(row => row.ProviderDomain)
            .HasMaxLength(IntegrationsPersistenceConstants.DomainLength)
            .IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ExternalAccountId)
            .HasMaxLength(IntegrationConnection.ExternalAccountIdLength);

        builder.Property(row => row.AccountLabel)
            .HasMaxLength(IntegrationConnection.AccountLabelLength);

        builder.Property(row => row.AvatarUrl)
            .HasMaxLength(IntegrationConnection.AccountLabelLength);

        builder.Property(row => row.IsPrivateRelay).IsRequired();

        builder.Property<string?>("_scopes")
            .HasColumnName("scopes")
            .HasMaxLength(IntegrationConnection.ScopesLength);

        // The credential. Two columns, both nullable because a disconnected row holds neither, and
        // both unreadable from anywhere but the aggregate's own ReadCredential.
        builder.Property<string?>("_accessTokenCipher")
            .HasColumnName("access_token_cipher")
            .HasMaxLength(IntegrationConnection.CipherLength);

        builder.Property<string?>("_refreshTokenCipher")
            .HasColumnName("refresh_token_cipher")
            .HasMaxLength(IntegrationConnection.CipherLength);

        builder.Property(row => row.CredentialKeyId)
            .HasMaxLength(IntegrationsPersistenceConstants.CorrelationLength);

        builder.Property(row => row.LastFailureKind)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength);

        builder.Property(row => row.LastFailureMessage)
            .HasMaxLength(IntegrationConnection.FailureMessageLength);

        builder.Property(row => row.LastFailureCorrelationId)
            .HasMaxLength(IntegrationsPersistenceConstants.CorrelationLength);

        // Computed at read time from the four columns above. Not a column.
        builder.Ignore(row => row.LastFailure);
        builder.Ignore(row => row.Scopes);
        builder.Ignore(row => row.HasRefreshToken);
        builder.Ignore(row => row.IsUsable);

        // The registry's integrity rule. See the class summary.
        builder.HasIndex(row => new { row.OwnerKind, row.OwnerId, row.Provider })
            .IsUnique()
            .HasDatabaseName("ux_integration_connections_owner_provider");

        // The connection centre's list, and its domain filter.
        builder.HasIndex(row => new { row.OwnerKind, row.OwnerId, row.ProviderDomain })
            .HasDatabaseName("ix_integration_connections_owner_domain");

        // The webhook's resolution: a provider names an account and has no session to scope by.
        builder.HasIndex(row => new { row.Provider, row.ExternalAccountId })
            .HasDatabaseName("ix_integration_connections_external_account");

        // The expiry sweep, which walks by expiry across every owner.
        builder.HasIndex(row => new { row.Status, row.ExpiresAt })
            .HasDatabaseName("ix_integration_connections_expiry");
    }
}

/// <summary>
/// Maps the authorization request.
/// <para>
/// <b>The unique index on the state hash is a security control.</b> It is what makes a callback's
/// state resolvable in one probe and what makes two requests sharing a state impossible — a
/// collision would be a state value redeemable twice.
/// </para>
/// </summary>
internal sealed class OAuthAuthorizationRequestConfiguration
    : IEntityTypeConfiguration<OAuthAuthorizationRequest>
{
    public void Configure(EntityTypeBuilder<OAuthAuthorizationRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntegrationsPersistenceConstants.AuthorizationRequestsTable,
            IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<OAuthAuthorizationRequestId>())
            .ValueGeneratedNever();

        builder.Property(row => row.OwnerKind)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Provider)
            .HasMaxLength(IntegrationsPersistenceConstants.ProviderLength)
            .IsRequired();

        builder.Property(row => row.StateHash)
            .HasMaxLength(OAuthAuthorizationRequest.HashLength)
            .IsRequired();

        builder.Property(row => row.CodeChallenge)
            .HasMaxLength(OAuthAuthorizationRequest.HashLength)
            .IsRequired();

        builder.Property(row => row.CodeChallengeMethod)
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.RedirectUri)
            .HasMaxLength(OAuthAuthorizationRequest.RedirectUriLength)
            .IsRequired();

        builder.Property(row => row.Intent)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property<string?>("_scopes")
            .HasColumnName("requested_scopes")
            .HasMaxLength(OAuthAuthorizationRequest.ScopesLength);

        builder.Ignore(row => row.RequestedScopes);

        builder.HasIndex(row => row.StateHash)
            .IsUnique()
            .HasDatabaseName("ux_integration_oauth_requests_state");

        // The retention sweep. Expired requests are deleted rather than kept: a consumed state hash
        // has no evidential value and an unconsumed one is an abandoned attempt.
        builder.HasIndex(row => row.ExpiresAt)
            .HasDatabaseName("ix_integration_oauth_requests_expiry");
    }
}

/// <summary>
/// Maps the source-to-external mapping.
/// <para>
/// <b>The unique index is the duplicate barrier.</b> (connection, calendar, source) — not
/// (source) alone, which would forbid exporting one event to two calendars, and not (source,
/// calendar) alone, which would let one member's export suppress another's.
/// </para>
/// </summary>
internal sealed class CalendarEventLinkConfiguration : IEntityTypeConfiguration<CalendarEventLink>
{
    public void Configure(EntityTypeBuilder<CalendarEventLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntegrationsPersistenceConstants.CalendarLinksTable, IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<CalendarEventLinkId>())
            .ValueGeneratedNever();

        builder.Property(row => row.ConnectionId)
            .HasConversion(IntegrationValueConverters.Id<IntegrationConnectionId>())
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.CalendarId)
            .HasMaxLength(CalendarEventLink.CalendarIdLength)
            .IsRequired();

        builder.Property(row => row.SourceKind)
            .HasMaxLength(CalendarEventLink.SourceKindLength)
            .IsRequired();

        builder.Property(row => row.SourceId)
            .HasMaxLength(CalendarEventLink.SourceIdLength)
            .IsRequired();

        builder.Property(row => row.ExternalEventId)
            .HasMaxLength(CalendarEventLink.ExternalEventIdLength)
            .IsRequired();

        builder.Property(row => row.ViewUrl)
            .HasMaxLength(CalendarEventLink.ViewUrlLength);

        builder.Property(row => row.TimeZone)
            .HasMaxLength(CalendarEventLink.TimeZoneLength)
            .IsRequired();

        builder.Property(row => row.ContentHash)
            .HasMaxLength(CalendarEventLink.ContentHashLength)
            .IsRequired();

        builder.Property(row => row.IdempotencyKey)
            .HasMaxLength(CalendarEventLink.IdempotencyKeyLength);

        builder.Property(row => row.State)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(row => new { row.ConnectionId, row.CalendarId, row.SourceId })
            .IsUnique()
            .HasDatabaseName("ux_integration_calendar_links_source");

        // Deleting by the provider's identifier, and the callback path.
        builder.HasIndex(row => new { row.ConnectionId, row.ExternalEventId })
            .HasDatabaseName("ix_integration_calendar_links_external");

        // Erasure and disconnect both delete by owner.
        builder.HasIndex(row => row.OwnerId)
            .HasDatabaseName("ix_integration_calendar_links_owner");
    }
}

/// <summary>Maps the member's calendar preferences. One row per member.</summary>
internal sealed class IntegrationCalendarSettingsConfiguration
    : IEntityTypeConfiguration<IntegrationCalendarSettings>
{
    public void Configure(EntityTypeBuilder<IntegrationCalendarSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntegrationsPersistenceConstants.CalendarSettingsTable,
            IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<IntegrationSettingsId>())
            .ValueGeneratedNever();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.DefaultCalendarId)
            .HasMaxLength(IntegrationCalendarSettings.CalendarIdLength);

        builder.Property(row => row.DefaultProvider)
            .HasMaxLength(IntegrationCalendarSettings.ProviderLength);

        builder.Property(row => row.SyncFrequency)
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.TimeZoneBehaviour)
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ConflictBehaviour)
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property<string?>("_defaultReminderMinutes")
            .HasColumnName("default_reminder_minutes")
            .HasMaxLength(IntegrationCalendarSettings.RemindersLength);

        builder.Ignore(row => row.DefaultReminderMinutes);

        // One row per member, enforced rather than assumed: two settings rows would make "which
        // calendar do new entries go into" a question with two answers.
        builder.HasIndex(row => row.OwnerId)
            .IsUnique()
            .HasDatabaseName("ux_integration_calendar_settings_owner");
    }
}

/// <summary>
/// Maps the synchronisation run.
/// <para>
/// <b>No unique index, deliberately.</b> The concurrency rule is "at most one <em>live</em> run per
/// key", which is a partial-index predicate and is not portable across this platform's providers.
/// The barrier is therefore the repository lookup plus a distributed lease — see
/// <c>StartCalendarSyncCommandHandler</c> — and the index below is what makes the lookup a probe
/// rather than a scan.
/// </para>
/// </summary>
internal sealed class IntegrationSyncRunConfiguration : IEntityTypeConfiguration<IntegrationSyncRun>
{
    public void Configure(EntityTypeBuilder<IntegrationSyncRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntegrationsPersistenceConstants.SyncRunsTable, IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<IntegrationSyncRunId>())
            .ValueGeneratedNever();

        builder.Property(row => row.ConnectionId)
            .HasConversion(IntegrationValueConverters.Id<IntegrationConnectionId>())
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.Provider)
            .HasMaxLength(IntegrationsPersistenceConstants.ProviderLength)
            .IsRequired();

        builder.Property(row => row.CalendarId)
            .HasMaxLength(IntegrationSyncRun.CalendarIdLength);

        builder.Property(row => row.Direction)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Phase)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property<string?>("_failureReasons")
            .HasColumnName("failure_reasons")
            .HasMaxLength(IntegrationSyncRun.FailureReasonsLength);

        builder.Ignore(row => row.FailureReasons);

        // The live-run lookup and the status route, in one index. Descending on StartedAt is not
        // expressed here because the platform's providers differ on descending-index support; the
        // repository orders and takes one, which the composite still serves.
        builder.HasIndex(row => new { row.OwnerId, row.Provider, row.CalendarId, row.StartedAt })
            .HasDatabaseName("ix_integration_sync_runs_key");

        // The reaper: rows claiming to be in progress.
        builder.HasIndex(row => new { row.Phase, row.StartedAt })
            .HasDatabaseName("ix_integration_sync_runs_phase");

        // Retention deletes by a predicate rather than by key, so it needs one.
        builder.HasIndex(row => row.CompletedAt)
            .HasDatabaseName("ix_integration_sync_runs_completed");
    }
}

/// <summary>
/// Maps received callbacks.
/// <para>
/// <b>The unique index on (provider, event id) is the replay barrier and not a hint.</b> A pre-read
/// tells the common case cheaply; this is what stops two workers racing one redelivery from both
/// applying it.
/// </para>
/// </summary>
internal sealed class ProcessedIntegrationWebhookConfiguration
    : IEntityTypeConfiguration<ProcessedIntegrationWebhook>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationWebhook> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntegrationsPersistenceConstants.WebhooksTable, IntegrationsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(IntegrationValueConverters.Id<IntegrationWebhookEventId>())
            .ValueGeneratedNever();

        builder.Property(row => row.Provider)
            .HasMaxLength(ProcessedIntegrationWebhook.ProviderLength)
            .IsRequired();

        builder.Property(row => row.ProviderEventId)
            .HasMaxLength(ProcessedIntegrationWebhook.ProviderEventIdLength)
            .IsRequired();

        builder.Property(row => row.Outcome)
            .HasConversion<string>()
            .HasMaxLength(IntegrationsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.EventKind)
            .HasMaxLength(ProcessedIntegrationWebhook.EventKindLength);

        builder.Property(row => row.ExternalReference)
            .HasMaxLength(ProcessedIntegrationWebhook.ExternalReferenceLength);

        builder.Property(row => row.ConnectionId)
            .HasConversion(IntegrationValueConverters.Id<IntegrationConnectionId>());

        builder.Property(row => row.Detail)
            .HasMaxLength(ProcessedIntegrationWebhook.DetailLength);

        builder.HasIndex(row => new { row.Provider, row.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("ux_integration_webhook_events_dedup");

        // Retention. §42 has no exception for webhook logs.
        builder.HasIndex(row => row.ReceivedAt)
            .HasDatabaseName("ix_integration_webhook_events_received");
    }
}
