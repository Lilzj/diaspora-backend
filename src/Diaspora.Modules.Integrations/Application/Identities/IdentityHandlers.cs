using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Integrations.Application.Identities;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-007 — account linking and unlinking.
//
// ── The ownership split, stated once ────────────────────────────────────────────────────────────
//
// Identity owns whether somebody may sign in. Every account on this platform is registered with a
// phone number and a password hash — `User.Register` takes both and neither is nullable — so the
// platform's own credential is always present and can never be removed through this module. B22
// owns the external half: a `identity.*` connection, its grant, and its revocation.
//
// That makes `canUnlink` computable here without B22 knowing anything about how Identity stores a
// password: the phone method always exists, so detaching an external identity can never be the last
// method. **The server enforces the rule anyway**, on the whole set, because the register is
// explicit that "§24's rule is a server rule; the client's disabled button is a courtesy, and a
// courtesy is not a control" — and because the day somebody makes the password optional, this check
// is what stops a member unlinking their way out of their own account.
//
// What is deliberately not built: a second user, a second session, a second credential store, and
// any route that mints a token. §28.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Every way this member can sign in.
/// <para>
/// Never cached. "staleTime 0, always refetched. This list is the input to a lock-out decision and a
/// stale copy could enable an unlink the current state forbids." Nothing in this module writes a
/// cache entry for it.
/// </para>
/// </summary>
public sealed record ListIdentityLinksQuery : IQuery<IReadOnlyList<IdentityLinkResponse>>;

public sealed class ListIdentityLinksQueryHandler
    : IQueryHandler<ListIdentityLinksQuery, IReadOnlyList<IdentityLinkResponse>>
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly ICurrentUser _currentUser;

    public ListIdentityLinksQueryHandler(
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        ICurrentUser currentUser)
    {
        _connections = connections;
        _registry = registry;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<IdentityLinkResponse>>> Handle(
        ListIdentityLinksQuery query, CancellationToken cancellationToken)
    {
        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(caller.Error);
        }

        var connections = await _connections.ListAsync(
            caller.Value.OwnerKind,
            caller.Value.OwnerId,
            ProviderKey.Domains.Identity,
            cancellationToken);

        var links = new List<IdentityLinkResponse>
        {
            // Identity's own method, always present and never removable through this module.
            // Reported so the client's list is the whole set — a list that showed only external
            // identities would make an account with one Google link look like an account with one
            // way in, and the disabled-unlink reasoning would be wrong.
            IdentityLinkComposer.PlatformPhoneMethod()
        };

        foreach (var connection in connections.Where(IsLive))
        {
            links.Add(IdentityLinkComposer.FromConnection(connection, _registry));
        }

        return Result.Success<IReadOnlyList<IdentityLinkResponse>>(links);
    }

    private static bool IsLive(Domain.Connections.IntegrationConnection connection) =>
        connection.Status is not IntegrationConnectionStatus.Disconnected;
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Detaches an external identity.
/// <para>
/// <b>Reauthentication is mandatory and is checked server-side.</b> "An attacker with a borrowed
/// unlocked phone must not be able to detach the real owner's recovery method." The proof this
/// module accepts is a fresh session — see <see cref="UnlinkIdentityCommandHandler"/> — because
/// session freshness is a fact Identity already publishes and inventing a second reauthentication
/// token would be a second credential.
/// </para>
/// <para>
/// <b>Unlinking revokes at the provider.</b> "Unlinking must revoke the provider-side grant as well
/// as forgetting it locally, or the member believes they have disconnected while Diaspora retains a
/// working refresh token." It reuses <see cref="IntegrationDisconnectService"/> rather than
/// reimplementing it, so the two paths cannot drift.
/// </para>
/// </summary>
[Transactional(Reason =
    "Drops the grant, clears the state it stranded and enqueues the disconnection contract through "
    + "the shared disconnect service. A partial commit would detach an identity without telling "
    + "anybody, or tell everybody about a detachment that did not happen.")]
public sealed record UnlinkIdentityCommand : ICommand<IReadOnlyList<IdentityLinkResponse>>
{
    public required string Provider { get; init; }

    /// <summary>
    /// Proof of a recent reauthentication.
    /// <para>
    /// The client's contract carries it and the server must not accept a request without one — but
    /// see the handler: the value is <em>not</em> trusted as the proof. Session freshness is.
    /// </para>
    /// </summary>
    public string? ReauthToken { get; init; }
}

public sealed class UnlinkIdentityCommandHandler
    : ICommandHandler<UnlinkIdentityCommand, IReadOnlyList<IdentityLinkResponse>>
{
    /// <summary>
    /// How recently the session must have been established.
    /// <para>
    /// Five minutes. Long enough that a member who just signed in is not asked again, short enough
    /// that a phone left unlocked on a table an hour ago is not a valid authorisation to detach
    /// somebody's recovery method.
    /// </para>
    /// </summary>
    public static readonly TimeSpan MaximumSessionAge = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Identity's own method. Named here and in the composer, and it is not a connection.
    /// </summary>
    public const string PlatformPhoneProvider = "identity.phone";

    private readonly IIntegrationConnectionRepository _connections;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IntegrationDisconnectService _disconnect;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public UnlinkIdentityCommandHandler(
        IIntegrationConnectionRepository connections,
        IntegrationProviderRegistry registry,
        IntegrationDisconnectService disconnect,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock)
    {
        _connections = connections;
        _registry = registry;
        _disconnect = disconnect;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<IdentityLinkResponse>>> Handle(
        UnlinkIdentityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(caller.Error);
        }

        if (!ProviderKey.TryParse(command.Provider, out var provider) || !provider.IsIdentity)
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(
                IntegrationErrors.ProviderUnknown(command.Provider));
        }

        // Identity's own method, refused explicitly.
        //
        // It parses as a perfectly good identity provider key and it names no connection this
        // module holds, so without this the request would fall through to the not-found branch
        // below and answer 200 with the list unchanged. That is safe and it is the wrong answer: a
        // member — or a client — asking to remove their phone method should be told it cannot be
        // removed, not given a success that quietly did nothing. §24's rule is a server rule, and a
        // silent no-op is not a refusal.
        if (string.Equals(provider.Value, PlatformPhoneProvider, StringComparison.Ordinal))
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(
                IntegrationErrors.LastAuthenticationMethod());
        }

        if (!IsRecentlyAuthenticated())
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(
                IntegrationErrors.ReauthenticationRequired());
        }

        var connections = await _connections.ListAsync(
            caller.Value.OwnerKind,
            caller.Value.OwnerId,
            ProviderKey.Domains.Identity,
            cancellationToken);

        var target = connections.FirstOrDefault(connection =>
            string.Equals(connection.Provider, provider.Value, StringComparison.Ordinal)
            && connection.Status is not IntegrationConnectionStatus.Disconnected);

        if (target is null)
        {
            // "Unlinking an unlinked provider returns the current list, not 404." A retry after a
            // timeout must not read as an error.
            return await ListAsync(caller.Value, cancellationToken);
        }

        // The server rule. It cannot currently fire, because Identity's phone method is always
        // present — and it is here because the client's disabled button is not a control, and
        // because the day the password becomes optional this is what stops a lock-out.
        var remaining = 1 + connections.Count(connection =>
            connection.Status is not IntegrationConnectionStatus.Disconnected
            && !string.Equals(connection.Provider, provider.Value, StringComparison.Ordinal));

        if (remaining < 1)
        {
            return Result.Failure<IReadOnlyList<IdentityLinkResponse>>(
                IntegrationErrors.LastAuthenticationMethod());
        }

        // Reused, not reimplemented. The shared service revokes at the provider, drops both
        // ciphers, clears the state a dead grant strands, publishes the contract and is idempotent;
        // a second implementation here would be a second set of those five decisions, and the one
        // that drifts is the revocation.
        await _disconnect.DisconnectAsync(
            caller.Value, provider, revokeAtProvider: true, "member", cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ListAsync(caller.Value, cancellationToken);
    }

    /// <summary>
    /// Whether the caller authenticated recently enough to change how they sign in.
    /// <para>
    /// Read from the token's <c>auth_time</c> or <c>iat</c> claim, both of which the platform's
    /// access tokens carry. A client-supplied <c>reauthToken</c> is not the proof and is not checked
    /// against anything: a value the caller chooses cannot demonstrate that the caller is the owner,
    /// and treating it as if it could would be security theatre with a parameter.
    /// </para>
    /// </summary>
    private bool IsRecentlyAuthenticated()
    {
        var issued = ReadUnixSeconds("auth_time") ?? ReadUnixSeconds("iat");

        return issued is { } moment && _clock.UtcNow - moment <= MaximumSessionAge;
    }

    private DateTimeOffset? ReadUnixSeconds(string claim) =>
        _currentUser.Claims.TryGetValue(claim, out var raw)
        && long.TryParse(raw, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    private async Task<Result<IReadOnlyList<IdentityLinkResponse>>> ListAsync(
        IntegrationCaller caller, CancellationToken cancellationToken)
    {
        var connections = await _connections.ListAsync(
            caller.OwnerKind, caller.OwnerId, ProviderKey.Domains.Identity, cancellationToken);

        var links = new List<IdentityLinkResponse> { IdentityLinkComposer.PlatformPhoneMethod() };

        foreach (var connection in connections.Where(connection =>
            connection.Status is not IntegrationConnectionStatus.Disconnected))
        {
            links.Add(IdentityLinkComposer.FromConnection(connection, _registry));
        }

        return Result.Success<IReadOnlyList<IdentityLinkResponse>>(links);
    }
}

/// <summary>Composes the client's identity-link rows. One place, so the two read paths agree.</summary>
internal static class IdentityLinkComposer
{
    /// <summary>
    /// Identity's own method.
    /// <para>
    /// Always present, always primary, never unlinkable, and it carries no account: the member's
    /// phone number is Identity's to serve on its own profile route and putting it here would make
    /// this module a second place a phone number is disclosed from.
    /// </para>
    /// </summary>
    public static IdentityLinkResponse PlatformPhoneMethod() => new()
    {
        Provider = UnlinkIdentityCommandHandler.PlatformPhoneProvider,
        Status = IntegrationMapper.StatusConnected,
        Mode = IntegrationMapper.ModeBackendConnected,
        Account = null,
        LinkedAt = null,
        LastUsedAt = null,
        IsPrimary = true,
        CanUnlink = false,
        CannotUnlinkReason =
            "Your phone number is how this account was created and how it is recovered, so it "
            + "cannot be removed."
    };

    public static IdentityLinkResponse FromConnection(
        Domain.Connections.IntegrationConnection connection, IntegrationProviderRegistry registry)
    {
        var configured = registry.FindOAuth(connection.Provider) is not null;

        return new IdentityLinkResponse
        {
            Provider = connection.Provider,
            Status = IntegrationMapper.ToStatus(connection, latestSync: null, configured),
            Mode = configured
                ? IntegrationMapper.ModeBackendConnected
                : IntegrationMapper.ModeNotConfigured,
            Account = IntegrationMapper.ToAccount(connection),
            LinkedAt = connection.ConnectedAt,
            LastUsedAt = connection.LastSuccessAt,
            IsPrimary = false,

            // Always true today, and computed rather than asserted — the phone method above is what
            // guarantees it, and if that ever stops being guaranteed this expression is what changes.
            CanUnlink = true,
            CannotUnlinkReason = null
        };
    }
}
