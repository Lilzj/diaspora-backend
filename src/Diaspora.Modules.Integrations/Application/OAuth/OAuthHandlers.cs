using System.Security.Cryptography;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Integrations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Connections;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.OAuth;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Integrations.Application.OAuth;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-002 — the authorization-code exchange. The register calls it "the blocker",
// and it blocks six other entries.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What a started authorization gives the client.</summary>
public sealed record AuthorizationStartResponse
{
    /// <summary>Where to send the member.</summary>
    public required string AuthorizationUrl { get; init; }

    /// <summary>
    /// The state to send back at exchange.
    /// <para>
    /// Issued here rather than by the client, because a state the server did not mint is a state the
    /// server cannot validate — and "never trust <c>state</c> without validation" then has nothing to
    /// check against.
    /// </para>
    /// </summary>
    public required string State { get; init; }

    /// <summary>The redirect the server approved. The client must use this one.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>What the adapter will actually ask for, after narrowing.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Opens an authorization.
/// <para>
/// Four controls are established here and every one of them is checked again at exchange: the
/// redirect is validated against the allow-list, the state is minted and stored as a hash, the PKCE
/// challenge is recorded, and the scopes are narrowed by the adapter rather than accepted from the
/// client.
/// </para>
/// </summary>
[Authorize(Policy = IntegrationsPolicies.ConnectionsWrite)]
public sealed record StartAuthorizationCommand : ICommand<AuthorizationStartResponse>
{
    public required string Provider { get; init; }

    public string? RedirectUri { get; init; }

    public string? CodeChallenge { get; init; }

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public string? Intent { get; init; }
}

public sealed class StartAuthorizationCommandHandler
    : ICommandHandler<StartAuthorizationCommand, AuthorizationStartResponse>
{
    private readonly IOAuthAuthorizationRequestRepository _requests;
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<IntegrationsOptions> _options;
    private readonly ICurrentUser _currentUser;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public StartAuthorizationCommandHandler(
        IOAuthAuthorizationRequestRepository requests,
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IUnitOfWork unitOfWork,
        IOptionsMonitor<IntegrationsOptions> options,
        ICurrentUser currentUser,
        IGuidGenerator guids,
        IClock clock)
    {
        _requests = requests;
        _connections = connections;
        _registry = registry;
        _protector = protector;
        _unitOfWork = unitOfWork;
        _options = options;
        _currentUser = currentUser;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<AuthorizationStartResponse>> Handle(
        StartAuthorizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<AuthorizationStartResponse>(caller.Error);
        }

        if (!ProviderKey.TryParse(command.Provider, out var provider))
        {
            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.ProviderUnknown(command.Provider));
        }

        var intent = ParseIntent(command.Intent);

        if (intent is null)
        {
            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.IntentUnsupported(command.Intent ?? "(none)"));
        }

        if (intent is OAuthIntent.SignIn)
        {
            // Refused rather than downgraded. Minting a platform session from an external assertion
            // is Identity's decision, Identity has no external credential model, and silently
            // treating a sign-in as a link would attach an account to whichever session was open.
            IntegrationsTelemetry.AuthorizationFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("reason", "intent_unsupported"));

            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.IntentUnsupported("signIn"));
        }

        if (!_protector.IsConfigured)
        {
            // Refusing before a token exists rather than after. An exchange that fell back to
            // plaintext storage because no key was configured would be the worst defect available
            // to this module, and this is where that path is closed.
            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.ProviderNotConfigured("credential custody"));
        }

        if (_registry.FindOAuth(provider.Value) is not { } adapter)
        {
            IntegrationsTelemetry.NotConfigured.Add(
                1, new KeyValuePair<string, object?>("capability", "oauth"));

            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.ProviderNotConfigured(provider.Value));
        }

        if (ResolveRedirect(command.RedirectUri) is not { } redirectUri)
        {
            IntegrationsTelemetry.AuthorizationFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("reason", "redirect_not_allowed"));

            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.RedirectUriNotAllowed());
        }

        if (string.IsNullOrWhiteSpace(command.CodeChallenge))
        {
            return Result.Failure<AuthorizationStartResponse>(Error.Validation(
                IntegrationsErrorCodes.AuthorizationStateUnknown,
                "A PKCE code challenge is required. This client is public and cannot authenticate "
                + "itself without one."));
        }

        if (intent is OAuthIntent.Reauthorize)
        {
            var existing = await _connections.FindAsync(
                caller.Value.OwnerKind, caller.Value.OwnerId, provider.Value, cancellationToken);

            if (existing is null)
            {
                return Result.Failure<AuthorizationStartResponse>(
                    IntegrationErrors.ConnectionNotFound(provider.Value));
            }
        }

        var scopes = adapter.NarrowScopes(command.Scopes ?? []);
        var state = NewState();

        var request = OAuthAuthorizationRequest.Start(
            OAuthAuthorizationRequestId.From(_guids.NewSequentialGuid()),
            caller.Value.OwnerKind,
            caller.Value.OwnerId,
            provider,
            state,
            command.CodeChallenge,
            redirectUri,
            intent.Value,
            scopes,
            _clock.UtcNow);

        var uri = adapter.BuildAuthorizationUri(
            new OAuthAuthorizationParameters(redirectUri, state, command.CodeChallenge, scopes));

        if (!uri.Succeeded || uri.Value is null)
        {
            return Result.Failure<AuthorizationStartResponse>(
                IntegrationErrors.FromProvider(uri, "starting this authorization"));
        }

        _requests.Add(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.AuthorizationsStarted.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("intent", intent.Value.ToString()));

        return Result.Success(new AuthorizationStartResponse
        {
            AuthorizationUrl = uri.Value.ToString(),
            State = state,
            RedirectUri = redirectUri,
            Scopes = scopes,
            ExpiresAt = request.ExpiresAt
        });
    }

    /// <summary>
    /// The allow-listed redirect, or null.
    /// <para>
    /// <b>Exact match after normalisation, never a prefix.</b> A prefix match on
    /// <c>https://diaspora.app/</c> admits <c>https://diaspora.app.attacker.example/</c>, which is
    /// the whole vulnerability. A client that sends none takes the first entry, so a deployment with
    /// one redirect works without the client naming it — and a deployment with none can start no
    /// authorization at all, which is the correct default.
    /// </para>
    /// </summary>
    private string? ResolveRedirect(string? requested)
    {
        var allowed = _options.CurrentValue.AllowedRedirectUris
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(entry => entry.Trim())
            .ToArray();

        if (allowed.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return allowed[0];
        }

        var candidate = requested.Trim();

        return allowed.FirstOrDefault(entry =>
            string.Equals(entry, candidate, StringComparison.Ordinal));
    }

    /// <summary>
    /// A fresh state value.
    /// <para>
    /// 256 bits from the cryptographic generator, base64url so it survives a query string. Not a
    /// GUID: a GUID is 122 bits of which several are structural, and this value's only job is to be
    /// unguessable.
    /// </para>
    /// </summary>
    private static string NewState() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static OAuthIntent? ParseIntent(string? intent) => intent?.Trim().ToLowerInvariant() switch
    {
        "link" => OAuthIntent.Link,
        "reauthorize" => OAuthIntent.Reauthorize,
        "signin" => OAuthIntent.SignIn,
        _ => null
    };
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Completes an authorization.
/// <para>
/// <b>Seven gates, in this order, and none is skippable.</b> The order matters: every gate that can
/// be answered from this platform's own records runs before the one that spends a call on a
/// provider, so a replayed or forged callback costs nothing and never reaches a vendor.
/// </para>
/// <list type="number">
/// <item>The caller is signed in.</item>
/// <item>The provider key parses and has a configured adapter.</item>
/// <item>The state matches a request this platform issued.</item>
/// <item>The request belongs to <em>this</em> caller.</item>
/// <item>The request has not been redeemed and has not expired.</item>
/// <item>The PKCE verifier hashes to the recorded challenge.</item>
/// <item>The redirect the client claims matches the one recorded at authorization.</item>
/// </list>
/// <para>
/// Only then is the code exchanged. Afterwards one more gate — the external account must not
/// already belong to a different member — which cannot run earlier because the account is not known
/// until the provider says who it is.
/// </para>
/// </summary>
[Transactional(Reason =
    "Consumes the single-use authorization request, writes or updates the connection and enqueues "
    + "the connected contract. A partial commit would either burn a code without storing the grant "
    + "it bought, or store a grant against a request that could be replayed.")]
[Authorize(Policy = IntegrationsPolicies.ConnectionsWrite)]
public sealed record ExchangeAuthorizationCommand : ICommand<IntegrationConnectionResponse>
{
    public required string Provider { get; init; }

    public string? Code { get; init; }

    public string? CodeVerifier { get; init; }

    public string? State { get; init; }

    public string? RedirectUri { get; init; }

    public string? Intent { get; init; }
}

public sealed class ExchangeAuthorizationCommandHandler
    : ICommandHandler<ExchangeAuthorizationCommand, IntegrationConnectionResponse>
{
    private readonly IOAuthAuthorizationRequestRepository _requests;
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IIntegrationEventPublisher _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<ExchangeAuthorizationCommandHandler> _logger;

    public ExchangeAuthorizationCommandHandler(
        IOAuthAuthorizationRequestRepository requests,
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IIntegrationEventPublisher events,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGuidGenerator guids,
        IClock clock,
        ILogger<ExchangeAuthorizationCommandHandler> logger)
    {
        _requests = requests;
        _connections = connections;
        _registry = registry;
        _protector = protector;
        _events = events;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<IntegrationConnectionResponse>> Handle(
        ExchangeAuthorizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IntegrationConnectionResponse>(caller.Error);
        }

        if (!ProviderKey.TryParse(command.Provider, out var provider))
        {
            return Result.Failure<IntegrationConnectionResponse>(
                IntegrationErrors.ProviderUnknown(command.Provider));
        }

        if (StartAuthorizationCommandHandler.ParseIntent(command.Intent) is OAuthIntent.SignIn)
        {
            return Fail(provider, "intent_unsupported", IntegrationErrors.IntentUnsupported("signIn"));
        }

        if (_registry.FindOAuth(provider.Value) is not { } adapter)
        {
            return Result.Failure<IntegrationConnectionResponse>(
                IntegrationErrors.ProviderNotConfigured(provider.Value));
        }

        if (!_protector.IsConfigured)
        {
            return Result.Failure<IntegrationConnectionResponse>(
                IntegrationErrors.ProviderNotConfigured("credential custody"));
        }

        if (string.IsNullOrWhiteSpace(command.State)
            || await _requests.FindByStateAsync(command.State, cancellationToken) is not { } request)
        {
            return Fail(provider, "state_unknown", IntegrationErrors.AuthorizationStateUnknown());
        }

        // The request belongs to this caller. A state value leaked to a second member must not be
        // redeemable by them — that would attach the first member's external account to the second
        // member's Diaspora account, which is the account takeover the 409 further down also guards.
        if (request.OwnerId != caller.Value.OwnerId
            || !string.Equals(request.Provider, provider.Value, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "An authorization state for {Provider} was presented by a caller it was not issued "
                + "to. The exchange was refused.",
                provider.Value);

            return Fail(provider, "state_owner_mismatch", IntegrationErrors.AuthorizationStateUnknown());
        }

        if (request.ConsumedAt is not null)
        {
            // A 409, because the register requires it: "A replayed code MUST return 409, not a
            // second connection." The status is what tells a client the first attempt worked.
            return Fail(provider, "code_replayed", IntegrationErrors.AuthorizationCodeReplayed());
        }

        if (request.IsExpired(_clock.UtcNow))
        {
            return Fail(provider, "state_expired", IntegrationErrors.AuthorizationStateExpired());
        }

        if (!request.MatchesVerifier(command.CodeVerifier))
        {
            return Fail(provider, "verifier_mismatch", IntegrationErrors.AuthorizationStateUnknown());
        }

        // Compared, not used. The URI presented to the provider below is the recorded one.
        if (!string.IsNullOrWhiteSpace(command.RedirectUri)
            && !string.Equals(command.RedirectUri.Trim(), request.RedirectUri, StringComparison.Ordinal))
        {
            return Fail(provider, "redirect_mismatch", IntegrationErrors.RedirectUriNotAllowed());
        }

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            return Fail(provider, "code_missing", IntegrationErrors.AuthorizationStateUnknown());
        }

        // Consumed before the call, not after. A provider call that times out after the code was
        // spent must not leave a request a second attempt could redeem — the code is burnt at the
        // provider either way, and a replay would produce a provider error that reads like a bug.
        request.TryConsume(_clock.UtcNow);
        _requests.Update(request);

        var outcome = await adapter.ExchangeAsync(
            new OAuthExchangeRequest(command.Code.Trim(), command.CodeVerifier!.Trim(), request.RedirectUri),
            cancellationToken);

        if (!outcome.Succeeded || outcome.Value is not { } grant)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Fail(
                provider,
                "provider",
                IntegrationErrors.FromProvider(outcome, "completing this authorization"));
        }

        var conflicting = await _connections.FindConflictingOwnerAsync(
            provider.Value, grant.Account.ExternalId, caller.Value.OwnerId, cancellationToken);

        if (conflicting is not null)
        {
            // "The 409 is a security control, not a niceness — without it, linking is an account
            // merge anybody can trigger." The request stays consumed, so the burnt code cannot be
            // retried against a different member either.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "An attempt to link a {Provider} account already attached to a different member was "
                + "refused.",
                provider.Value);

            return Fail(
                provider, "account_conflict", IntegrationErrors.ExternalAccountAlreadyLinked());
        }

        var now = _clock.UtcNow;
        var connection = await _connections.FindAsync(
            caller.Value.OwnerKind, caller.Value.OwnerId, provider.Value, cancellationToken);

        var isFirstConnection = connection?.ConnectedAt is null;

        if (connection is null)
        {
            connection = IntegrationConnection.Start(
                IntegrationConnectionId.From(_guids.NewSequentialGuid()),
                caller.Value.OwnerKind,
                caller.Value.OwnerId,
                provider,
                now);

            _connections.Add(connection);
        }

        connection.ApplyGrant(
            grant.Account,
            grant.GrantedScopes,
            _protector.Protect(grant.AccessToken),
            grant.RefreshToken is { } refresh ? _protector.Protect(refresh) : null,
            _protector.KeyId,
            grant.ExpiresAt,
            now);

        _connections.Update(connection);

        await _events.PublishAsync(
            new IntegrationConnectedIntegrationEvent
            {
                UserId = caller.Value.OwnerId,
                Provider = provider.Value,
                ProviderDomain = provider.Domain,
                IsFirstConnection = isFirstConnection,
                Scopes = grant.GrantedScopes,
                ConnectedAt = now
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.ConnectionsEstablished.Add(
            1, new KeyValuePair<string, object?>("provider", provider.Value));

        return Result.Success(
            ListConnectionsQueryHandler.Project(connection, latestSync: null, _registry));
    }

    private static Result<IntegrationConnectionResponse> Fail(
        ProviderKey provider, string reason, Error error)
    {
        IntegrationsTelemetry.AuthorizationFailures.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("reason", reason));

        return Result.Failure<IntegrationConnectionResponse>(error);
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Revokes a grant at the provider.
/// <para>
/// Separate from disconnecting: a member may want the provider-side grant dropped while keeping the
/// connection record, and a reauthorization flow needs exactly that. Idempotent — "revoking twice is
/// a 200 no-op" — because a retry must not become an error.
/// </para>
/// </summary>
[Authorize(Policy = IntegrationsPolicies.ConnectionsWrite)]
public sealed record RevokeAuthorizationCommand : ICommand<IntegrationDisconnectResponse>
{
    public required string Provider { get; init; }
}

public sealed class RevokeAuthorizationCommandHandler
    : ICommandHandler<RevokeAuthorizationCommand, IntegrationDisconnectResponse>
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationCredentialProtector _protector;
    private readonly IIntegrationEventPublisher _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public RevokeAuthorizationCommandHandler(
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        IIntegrationCredentialProtector protector,
        IIntegrationEventPublisher events,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock)
    {
        _connections = connections;
        _registry = registry;
        _protector = protector;
        _events = events;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<IntegrationDisconnectResponse>> Handle(
        RevokeAuthorizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IntegrationDisconnectResponse>(caller.Error);
        }

        if (!ProviderKey.TryParse(command.Provider, out var provider))
        {
            return Result.Failure<IntegrationDisconnectResponse>(
                IntegrationErrors.ProviderUnknown(command.Provider));
        }

        var connection = await _connections.FindAsync(
            caller.Value.OwnerKind, caller.Value.OwnerId, provider.Value, cancellationToken);

        if (connection is null || connection.ReadCredential() is not { } credential)
        {
            // Nothing held, so nothing to revoke. A 200 rather than a 404, per the register.
            return Result.Success(new IntegrationDisconnectResponse
            {
                Provider = provider.Value,
                Status = Mapping.IntegrationMapper.StatusDisconnected,
                RevokedAtProvider = false
            });
        }

        var revoked = false;

        if (_registry.FindOAuth(provider.Value) is { } adapter)
        {
            var refresh = credential.RefreshTokenCipher is { } cipher
                && _protector.TryUnprotect(cipher, out var refreshToken)
                ? refreshToken
                : null;

            var access = _protector.TryUnprotect(credential.AccessTokenCipher, out var accessToken)
                ? accessToken
                : null;

            var outcome = await adapter.RevokeAsync(
                new OAuthRevocationRequest(access, refresh), cancellationToken);

            revoked = outcome is { Succeeded: true, Value: true };
        }

        var now = _clock.UtcNow;

        connection.Disconnect(revoked, now);
        _connections.Update(connection);

        await _events.PublishAsync(
            new IntegrationDisconnectedIntegrationEvent
            {
                UserId = caller.Value.OwnerId,
                Provider = provider.Value,
                ProviderDomain = provider.Domain,
                RevokedAtProvider = revoked,
                Initiator = "member",
                DisconnectedAt = now
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.ConnectionsDisconnected.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("revoked_at_provider", revoked));

        return Result.Success(new IntegrationDisconnectResponse
        {
            Provider = provider.Value,
            Status = Mapping.IntegrationMapper.StatusDisconnected,
            RevokedAtProvider = revoked
        });
    }
}
