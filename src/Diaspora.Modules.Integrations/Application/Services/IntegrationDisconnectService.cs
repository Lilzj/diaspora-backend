using Diaspora.Contracts.Integrations.V1.Events;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Integrations.Application.Services;

/// <summary>
/// Dropping a grant, in one place.
/// <para>
/// <b>A service rather than one handler calling another.</b> Two paths end a connection — the
/// disconnect route and the identity unlink — and both must revoke at the provider, drop both
/// ciphers, clear the calendar state a dead grant strands, publish the disconnection contract and be
/// idempotent. That is five decisions; a second implementation would be a second set of them, and
/// the one that drifts is the revocation.
/// </para>
/// <para>
/// The obvious alternative, injecting <c>ISender</c> into the unlink handler and sending the
/// disconnect command, is refused by DSP1001 — and correctly: a handler that can reach any handler
/// in the process is a handler whose dependencies cannot be read from its constructor.
/// </para>
/// <para>
/// <b>Nothing here calls <c>SaveChangesAsync</c>.</b> The caller owns the unit of work, so the
/// disconnection and whatever else the caller is doing commit together. A service that saved would
/// make the unlink handler's transaction two transactions.
/// </para>
/// </summary>
public sealed class IntegrationDisconnectService
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationCalendarSettingsRepository _settings;
    private readonly ICalendarEventLinkRepository _links;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IIntegrationEventPublisher _events;
    private readonly IClock _clock;
    private readonly ILogger<IntegrationDisconnectService> _logger;

    public IntegrationDisconnectService(
        IIntegrationConnectionRepository connections,
        IIntegrationCalendarSettingsRepository settings,
        ICalendarEventLinkRepository links,
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IIntegrationEventPublisher events,
        IClock clock,
        ILogger<IntegrationDisconnectService> logger)
    {
        _connections = connections;
        _settings = settings;
        _links = links;
        _registry = registry;
        _protector = protector;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ends a connection, if there is one.
    /// <para>
    /// Returns whether the provider confirmed the revocation. <b>Never throws for a provider
    /// failure</b>: a member who asked to disconnect has disconnected, and refusing to until the
    /// provider answers leaves the platform holding a refresh token it was told to forget.
    /// </para>
    /// </summary>
    public async Task<bool> DisconnectAsync(
        IntegrationCaller caller,
        ProviderKey provider,
        bool revokeAtProvider,
        string initiator,
        CancellationToken cancellationToken)
    {
        var connection = await _connections.FindAsync(
            caller.OwnerKind, caller.OwnerId, provider.Value, cancellationToken);

        if (connection is null)
        {
            // Nothing to do, and not an error. The register's idempotency rule taken to its
            // conclusion: a member who never connected and one who disconnected twice get the same
            // answer.
            return false;
        }

        var revoked = revokeAtProvider && await RevokeAsync(provider, connection, cancellationToken);
        var wasConnected = connection.Status is not IntegrationConnectionStatus.Disconnected;
        var now = _clock.UtcNow;

        connection.Disconnect(revoked, now);
        _connections.Update(connection);

        if (provider.IsCalendar)
        {
            await ForgetCalendarStateAsync(caller.OwnerId, provider, connection, now, cancellationToken);
        }

        if (wasConnected)
        {
            await _events.PublishAsync(
                new IntegrationDisconnectedIntegrationEvent
                {
                    UserId = caller.OwnerId,
                    Provider = provider.Value,
                    ProviderDomain = provider.Domain,
                    RevokedAtProvider = revoked,
                    Initiator = initiator,
                    DisconnectedAt = now
                },
                cancellationToken);

            IntegrationsTelemetry.ConnectionsDisconnected.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("revoked_at_provider", revoked));
        }

        return revoked;
    }

    /// <summary>Asks the provider to drop the grant. Never throws, never blocks the disconnect.</summary>
    private async Task<bool> RevokeAsync(
        ProviderKey provider, IntegrationConnection connection, CancellationToken cancellationToken)
    {
        if (connection.ReadCredential() is not { } credential
            || _registry.FindOAuth(provider.Value) is not { } adapter)
        {
            return false;
        }

        // The refresh token first where there is one: revoking it drops the whole grant, where
        // revoking an access token drops an hour of it.
        var refresh = credential.RefreshTokenCipher is { } cipher
            && _protector.TryUnprotect(cipher, out var refreshToken)
            ? refreshToken
            : null;

        var access = _protector.TryUnprotect(credential.AccessTokenCipher, out var accessToken)
            ? accessToken
            : null;

        if (refresh is null && access is null)
        {
            // Nothing openable to revoke with — a rotated protection key, most likely. Reported as
            // not-revoked rather than as an error: the grant is going regardless, and the false is
            // the honest signal to the member.
            return false;
        }

        var outcome = await adapter.RevokeAsync(
            new OAuthRevocationRequest(access, refresh), cancellationToken);

        if (!outcome.Succeeded)
        {
            _logger.LogWarning(
                "A revocation at {Provider} did not succeed ({Kind}). The local grant is being "
                + "dropped regardless and the member will be told the provider still holds it.",
                provider.Value,
                outcome.FailureKind);

            return false;
        }

        return outcome.Value;
    }

    /// <summary>
    /// Forgets the calendar state a dropped grant leaves stranded.
    /// <para>
    /// Two things, and both are privacy answers as much as tidiness. The default calendar pointer is
    /// cleared because it names a calendar the member can no longer reach. The event links are
    /// deleted because their only purpose is deduplicating against a grant that no longer exists —
    /// §43's "what local integration data must be removed" has an unambiguous answer for both.
    /// </para>
    /// <para>
    /// <b>The entries themselves are left in the member's calendar.</b> Deleting somebody's calendar
    /// entries because they disconnected an integration would be destroying their data on an
    /// inference — and the grant needed to do it is the one being revoked.
    /// </para>
    /// </summary>
    private async Task ForgetCalendarStateAsync(
        Guid ownerId,
        ProviderKey provider,
        IntegrationConnection connection,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await _settings.FindAsync(ownerId, cancellationToken) is { } settings)
        {
            settings.ForgetProvider(provider.Value, now);
            _settings.Update(settings);
        }

        foreach (var link in await _links.ListForConnectionAsync(connection.Id, cancellationToken))
        {
            _links.Remove(link);
        }
    }
}
