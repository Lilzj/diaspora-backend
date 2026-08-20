using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Application.Services;

/// <summary>
/// Turns "this member wants to call Google" into a usable access token, or a specific refusal.
/// <para>
/// <b>Every provider call on this surface goes through here, and that is the whole design.</b> The
/// sequence — find the connection for <em>this</em> owner, check it is usable, refresh it if it is
/// about to lapse, open the cipher, and record the outcome — has five places to get wrong and would
/// otherwise be written once per capability. It is written once.
/// </para>
/// <para>
/// <b>The refresh happens under a distributed lease.</b> Two concurrent calls on one connection
/// would otherwise both see an expiring token, both refresh, and — with a provider that rotates
/// refresh tokens on use, which Google does — the second would invalidate the first, leaving a
/// connection whose stored refresh token the provider has already retired. §45 names simultaneous
/// token refresh explicitly, and this is where it is prevented.
/// </para>
/// <para>
/// <b>A credential that will not open is <c>REAUTH_REQUIRED</c>, not a 500.</b> A restored backup or
/// a key ring that was not persisted produces exactly that, and the honest answer to a member is
/// "connect it again" rather than an error page.
/// </para>
/// </summary>
public sealed class IntegrationAccessService
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IDistributedLock _leases;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<IntegrationsOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<IntegrationAccessService> _logger;

    public IntegrationAccessService(
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IDistributedLock leases,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<IntegrationsOptions> options,
        IClock clock,
        ILogger<IntegrationAccessService> logger)
    {
        _connections = connections;
        _registry = registry;
        _protector = protector;
        _leases = leases;
        _unitOfWork = unitOfWork;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// A usable access token for this owner's connection, or the reason there is not one.
    /// </summary>
    public async Task<Result<IntegrationAccess>> ResolveAsync(
        IntegrationOwnerKind ownerKind,
        Guid ownerId,
        ProviderKey provider,
        CancellationToken cancellationToken)
    {
        var connection = await _connections.FindAsync(
            ownerKind, ownerId, provider.Value, cancellationToken);

        if (connection is null)
        {
            return Result.Failure<IntegrationAccess>(
                IntegrationErrors.ConnectionNotFound(provider.Value));
        }

        if (connection.Status is IntegrationConnectionStatus.ReauthorizationRequired)
        {
            return Result.Failure<IntegrationAccess>(
                IntegrationErrors.ReauthorizationRequired(provider.Value));
        }

        if (!connection.IsUsable)
        {
            return Result.Failure<IntegrationAccess>(
                connection.Status is IntegrationConnectionStatus.Disconnected
                    ? IntegrationErrors.ConnectionNotFound(provider.Value)
                    : IntegrationErrors.ConnectionNotUsable(provider.Value));
        }

        var now = _clock.UtcNow;
        var skew = TimeSpan.FromSeconds(_options.CurrentValue.TokenRefreshSkewSeconds);

        if (connection.NeedsRefresh(now, skew))
        {
            var refreshed = await RefreshAsync(connection, provider, cancellationToken);

            if (refreshed.IsFailure)
            {
                return Result.Failure<IntegrationAccess>(refreshed.Error);
            }
        }

        if (connection.ReadCredential() is not { } sealedCredential)
        {
            return Result.Failure<IntegrationAccess>(
                IntegrationErrors.ConnectionNotUsable(provider.Value));
        }

        if (!_protector.TryUnprotect(sealedCredential.AccessTokenCipher, out var accessToken))
        {
            await FailAsync(
                connection,
                IntegrationFailureKind.Unauthorized,
                "This connection's stored authorization could not be read. Connect it again.",
                cancellationToken);

            return Result.Failure<IntegrationAccess>(
                IntegrationErrors.ReauthorizationRequired(provider.Value));
        }

        return Result.Success(new IntegrationAccess(connection, accessToken));
    }

    /// <summary>
    /// Applies a provider outcome to the connection: a success clears the failure, a failure records
    /// it and — for an authorization failure — ends the grant.
    /// <para>
    /// Called by every capability handler with whatever the adapter returned, so that a member
    /// reading the connection centre sees the state the last real call actually produced rather than
    /// the state of the last successful one.
    /// </para>
    /// </summary>
    public async Task ObserveAsync<T>(
        IntegrationConnection connection,
        IntegrationOutcome<T> outcome,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (outcome.Succeeded)
        {
            connection.RecordSuccess(_clock.UtcNow);
            _connections.Update(connection);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return;
        }

        await FailAsync(
            connection,
            outcome.FailureKind ?? IntegrationFailureKind.ProviderError,
            outcome.Message ?? "The provider refused the request.",
            cancellationToken,
            correlationId);
    }

    private async Task FailAsync(
        IntegrationConnection connection,
        IntegrationFailureKind kind,
        string message,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        var wasConnected = connection.Status is IntegrationConnectionStatus.Connected;

        connection.RecordFailure(kind, message, _clock.UtcNow, correlationId);
        _connections.Update(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (kind is IntegrationFailureKind.Unauthorized && wasConnected)
        {
            IntegrationsTelemetry.ReauthorizationsRequired.Add(
                1,
                new KeyValuePair<string, object?>("provider", connection.Provider),
                new KeyValuePair<string, object?>("cause", "provider_rejected"));
        }
    }

    /// <summary>
    /// Refreshes an access token under a lease. See the class summary for why the lease is not
    /// optional.
    /// </summary>
    private async Task<Result> RefreshAsync(
        IntegrationConnection connection, ProviderKey provider, CancellationToken cancellationToken)
    {
        if (!connection.HasRefreshToken)
        {
            // Nothing to refresh with. The grant is not revoked — it has simply run out — and the
            // member has to authorize again, which is the same recovery as a revocation.
            connection.RequireReauthorization(
                "This connection has expired. Connect it again to carry on.", _clock.UtcNow);
            _connections.Update(connection);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            IntegrationsTelemetry.ReauthorizationsRequired.Add(
                1,
                new KeyValuePair<string, object?>("provider", connection.Provider),
                new KeyValuePair<string, object?>("cause", "expired_no_refresh_token"));

            return Result.Failure(IntegrationErrors.ReauthorizationRequired(provider.Value));
        }

        await using var lease = await _leases.TryAcquireAsync(
            $"integrations:refresh:{connection.Id.Value:N}",
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (lease is null)
        {
            // Another request is refreshing this connection right now. Its result will be committed
            // in a moment; failing here rather than waiting means the caller retries against a
            // connection that has a fresh token, which is faster than blocking and cannot deadlock.
            return Result.Failure(Error.Conflict(
                IntegrationsErrorCodes.ConnectionNotUsable,
                "This connection is being renewed. Try again in a moment."));
        }

        var adapter = _registry.FindOAuth(provider.Value);

        if (adapter is null)
        {
            return Result.Failure(IntegrationErrors.ProviderNotConfigured(provider.Value));
        }

        if (connection.ReadCredential() is not { RefreshTokenCipher: { } refreshCipher }
            || !_protector.TryUnprotect(refreshCipher, out var refreshToken))
        {
            connection.RequireReauthorization(
                "This connection's stored authorization could not be read. Connect it again.",
                _clock.UtcNow);
            _connections.Update(connection);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure(IntegrationErrors.ReauthorizationRequired(provider.Value));
        }

        var outcome = await adapter.RefreshAsync(refreshToken, cancellationToken);

        IntegrationsTelemetry.TokenRefreshes.Add(
            1,
            new KeyValuePair<string, object?>("provider", connection.Provider),
            new KeyValuePair<string, object?>("outcome", outcome.Succeeded ? "ok" : "failed"));

        if (!outcome.Succeeded || outcome.Value is not { } grant)
        {
            // A refused refresh ends the grant; anything else leaves it alone so a provider blip
            // does not make every member reconnect.
            if (outcome.FailureKind is IntegrationFailureKind.Unauthorized)
            {
                connection.RequireReauthorization(
                    outcome.Message ?? "The provider ended this authorization.", _clock.UtcNow);
                _connections.Update(connection);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                IntegrationsTelemetry.ReauthorizationsRequired.Add(
                    1,
                    new KeyValuePair<string, object?>("provider", connection.Provider),
                    new KeyValuePair<string, object?>("cause", "refresh_rejected"));

                return Result.Failure(IntegrationErrors.ReauthorizationRequired(provider.Value));
            }

            _logger.LogWarning(
                "A token refresh for {Provider} did not succeed: {Kind}.",
                connection.Provider,
                outcome.FailureKind);

            return Result.Failure(IntegrationErrors.FromProvider(outcome, "renewing this connection"));
        }

        connection.ApplyRefreshedAccessToken(
            _protector.Protect(grant.AccessToken),
            grant.RefreshToken is { } rotated ? _protector.Protect(rotated) : null,
            _protector.KeyId,
            grant.ExpiresAt,
            _clock.UtcNow);

        _connections.Update(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

/// <summary>
/// A resolved grant: the connection, and a token that works right now.
/// <para>
/// The token is a plain string on a record that is never serialised, never logged and never returned
/// from a route. It lives for the length of one handler call.
/// </para>
/// </summary>
public sealed record IntegrationAccess(IntegrationConnection Connection, string AccessToken);
