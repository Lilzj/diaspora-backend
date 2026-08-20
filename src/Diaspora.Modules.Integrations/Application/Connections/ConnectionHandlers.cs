using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Integrations.Application.Connections;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-001 — the integration connection registry.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Everything this member has connected.
/// <para>
/// Takes no subject. The register: "Self only. There is no read-another-member's-connections
/// variant."
/// </para>
/// </summary>
public sealed record ListConnectionsQuery : IQuery<IReadOnlyList<IntegrationConnectionResponse>>
{
    /// <summary>Optional filter on the domain half of the key — <c>calendar</c>, <c>identity</c>.</summary>
    public string? Domain { get; init; }
}

public sealed class ListConnectionsQueryHandler
    : IQueryHandler<ListConnectionsQuery, IReadOnlyList<IntegrationConnectionResponse>>
{
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationSyncRunRepository _syncs;
    private readonly IntegrationProviderRegistry _registry;
    private readonly ICurrentUser _currentUser;

    public ListConnectionsQueryHandler(
        IIntegrationConnectionRepository connections,
        IIntegrationSyncRunRepository syncs,
        IntegrationProviderRegistry registry,
        ICurrentUser currentUser)
    {
        _connections = connections;
        _syncs = syncs;
        _registry = registry;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<IntegrationConnectionResponse>>> Handle(
        ListConnectionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IntegrationConnectionResponse>>(caller.Error);
        }

        var rows = await _connections.ListAsync(
            caller.Value.OwnerKind,
            caller.Value.OwnerId,
            NormalizeDomain(query.Domain),
            cancellationToken);

        var responses = new List<IntegrationConnectionResponse>(rows.Count);

        foreach (var connection in rows)
        {
            // Per connection rather than one query for all of them. The list is a member's own
            // connections — single digits in every realistic case — and the alternative is a
            // repository method returning latest-run-per-provider, which is a window function this
            // platform would then owe a provider-portable implementation of.
            var latest = await _syncs.FindLatestAsync(
                caller.Value.OwnerId, connection.Provider, calendarId: null, cancellationToken);

            responses.Add(Project(connection, latest, _registry));
        }

        return Result.Success<IReadOnlyList<IntegrationConnectionResponse>>(responses);
    }

    /// <summary>
    /// Projects one row, resolving whether the deployment can still serve it.
    /// <para>
    /// Shared with the exchange path so a connection cannot be described one way in the list and
    /// another in the response to an action on it.
    /// </para>
    /// </summary>
    internal static IntegrationConnectionResponse Project(
        Domain.Connections.IntegrationConnection connection,
        Domain.Sync.IntegrationSyncRun? latestSync,
        IntegrationProviderRegistry registry)
    {
        var configured =
            registry.FindOAuth(connection.Provider) is not null
            || registry.FindCalendar(connection.Provider) is not null;

        return IntegrationMapper.ToResponse(
            connection,
            latestSync,
            configured,
            registry.CalendarCapabilities(connection.Provider));
    }

    private static string? NormalizeDomain(string? domain) =>
        string.IsNullOrWhiteSpace(domain) ? null : domain.Trim().ToLowerInvariant();
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Disconnects a provider.
/// <para>
/// <b>Idempotent, and the register explains why in one sentence:</b> "Disconnecting an
/// already-disconnected integration returns 200 with the same body, never 404. §41 requires the UI
/// never be left saying 'Connected' after a disconnect, and a 404 on the second call is exactly how
/// a retry leaves it there."
/// </para>
/// <para>
/// The work is <see cref="IntegrationDisconnectService"/>'s, because the identity unlink path must
/// do exactly the same five things and a second implementation would be a second set of them.
/// </para>
/// </summary>
[Transactional(Reason =
    "Drops the grant, forgets the default calendar that pointed at it, deletes the calendar links "
    + "it stranded and enqueues the disconnection contract. A partial commit would leave a settings "
    + "row naming a calendar the member can no longer reach, or a disconnection nobody was told "
    + "about.")]
public sealed record DisconnectIntegrationCommand : ICommand<IntegrationDisconnectResponse>
{
    public required string Provider { get; init; }

    /// <summary>Whether to ask the provider to drop the grant as well as forgetting it locally.</summary>
    public bool RevokeAtProvider { get; init; } = true;
}

public sealed class DisconnectIntegrationCommandHandler
    : ICommandHandler<DisconnectIntegrationCommand, IntegrationDisconnectResponse>
{
    private readonly IntegrationDisconnectService _disconnect;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DisconnectIntegrationCommandHandler(
        IntegrationDisconnectService disconnect,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _disconnect = disconnect;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result<IntegrationDisconnectResponse>> Handle(
        DisconnectIntegrationCommand command, CancellationToken cancellationToken)
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

        var revoked = await _disconnect.DisconnectAsync(
            caller.Value, provider, command.RevokeAtProvider, "member", cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new IntegrationDisconnectResponse
        {
            Provider = provider.Value,
            Status = IntegrationMapper.StatusDisconnected,
            RevokedAtProvider = revoked
        });
    }
}
