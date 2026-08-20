using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Integrations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Application.Mapping;
using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Contracts.V1.Responses;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Domain.Sync;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Integrations.Application.Sync;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE16-GAP-005 — synchronisation and its observable state.
//
// What ships here and what does not, stated plainly because the difference is the whole of §49:
//
//   SHIPS      The run record, the concurrency guard, the accepted-not-completed contract, the
//              status route, and reconciliation of what this platform already wrote — a link whose
//              entry the member has since deleted at the provider is detected and marked, which is
//              the export direction being kept honest.
//
//   DOES NOT   Import. Reading a member's whole calendar into Diaspora's storage needs an adapter
//              that declares ImportEvents and a consent flow that says so in those words; no adapter
//              declares it, so `direction: import` is refused with Unsupported rather than accepted
//              and quietly performed as an export.
//
//   DOES NOT   The watch channel. It needs a publicly reachable HTTPS endpoint on a verified domain
//              and renewable channels. The intake route exists and is signature-verified; the
//              registration half is an external dependency and the capability report says so.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Starts a synchronisation.
/// <para>
/// <b>Accepted, not completed.</b> The register's contract returns
/// <c>{ syncId, phase: "syncing", startedAt }</c>, and the client reads the outcome back.
/// </para>
/// <para>
/// <b>A run already going for the same key is returned rather than duplicated.</b> "Two concurrent
/// syncs over one calendar is how duplicates are produced." The guard is two things: a lookup for the
/// live run, which makes the common case correct and cheap, and a distributed lease over the same
/// key, which makes two instances racing correct as well.
/// </para>
/// </summary>
[Transactional(Reason =
    "Opens the run, performs the reconciliation, closes the run and — when it did not end cleanly — "
    + "enqueues the contract that tells the member. A partial commit would leave a run stuck in "
    + "`syncing`, which is also the concurrency barrier, so that calendar could never sync again.")]
public sealed record StartCalendarSyncCommand : ICommand<IntegrationSyncStateResponse>
{
    public required string Provider { get; init; }

    public string? CalendarId { get; init; }

    /// <summary><c>export</c>, <c>import</c> or <c>both</c>.</summary>
    public string? Direction { get; init; }
}

public sealed class StartCalendarSyncCommandHandler
    : ICommandHandler<StartCalendarSyncCommand, IntegrationSyncStateResponse>
{
    private readonly IntegrationAccessService _access;
    private readonly IntegrationProviderRegistry _registry;
    private readonly IIntegrationSyncRunRepository _runs;
    private readonly ICalendarEventLinkRepository _links;
    private readonly IIntegrationEventPublisher _events;
    private readonly IDistributedLock _leases;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICorrelationContext _correlation;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<StartCalendarSyncCommandHandler> _logger;

    public StartCalendarSyncCommandHandler(
        IntegrationAccessService access,
        IntegrationProviderRegistry registry,
        IIntegrationSyncRunRepository runs,
        ICalendarEventLinkRepository links,
        IIntegrationEventPublisher events,
        IDistributedLock leases,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICorrelationContext correlation,
        IGuidGenerator guids,
        IClock clock,
        ILogger<StartCalendarSyncCommandHandler> logger)
    {
        _access = access;
        _registry = registry;
        _runs = runs;
        _links = links;
        _events = events;
        _leases = leases;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _correlation = correlation;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<IntegrationSyncStateResponse>> Handle(
        StartCalendarSyncCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IntegrationSyncStateResponse>(caller.Error);
        }

        if (!ProviderKey.TryParse(command.Provider, out var provider) || !provider.IsCalendar)
        {
            return Result.Failure<IntegrationSyncStateResponse>(
                IntegrationErrors.ProviderUnknown(command.Provider));
        }

        var direction = ParseDirection(command.Direction);

        if (direction is null)
        {
            return Result.Failure<IntegrationSyncStateResponse>(
                IntegrationErrors.SyncDirectionUnsupported(command.Direction ?? "(none)"));
        }

        if (_registry.FindCalendar(provider.Value) is not { } adapter)
        {
            IntegrationsTelemetry.NotConfigured.Add(
                1, new KeyValuePair<string, object?>("capability", "calendar.sync"));

            return Result.Failure<IntegrationSyncStateResponse>(
                IntegrationErrors.ProviderNotConfigured(provider.Value));
        }

        // Import is refused rather than silently downgraded to export. A member who asked for
        // "both" and received an export would believe their calendar had been read into Diaspora,
        // which is a consent claim rather than a feature claim.
        if (direction is SyncDirection.Import or SyncDirection.Both
            && !adapter.Capabilities.Contains(ExternalCalendarCapability.ImportEvents))
        {
            return Result.Failure<IntegrationSyncStateResponse>(
                IntegrationErrors.SyncDirectionUnsupported(direction.Value.ToString().ToLowerInvariant()));
        }

        var calendarId = string.IsNullOrWhiteSpace(command.CalendarId)
            ? null
            : command.CalendarId.Trim();

        var now = _clock.UtcNow;

        // Barrier one: an existing live run is returned, not duplicated.
        if (await _runs.FindLiveAsync(caller.Value.OwnerId, provider.Value, calendarId, now, cancellationToken)
            is { } live)
        {
            return Result.Success(IntegrationMapper.ToSyncState(live));
        }

        // Barrier two: the same key across instances.
        await using var lease = await _leases.TryAcquireAsync(
            LeaseKey(caller.Value.OwnerId, provider.Value, calendarId),
            IntegrationSyncRun.StaleAfter,
            cancellationToken);

        if (lease is null)
        {
            var running = await _runs.FindLatestAsync(
                caller.Value.OwnerId, provider.Value, calendarId, cancellationToken);

            // Another instance took it between the lookup and here. Reporting its run is the
            // contract's answer; reporting a refusal would make a race look like an error.
            return Result.Success(IntegrationMapper.ToSyncState(running));
        }

        var resolved = await _access.ResolveAsync(
            caller.Value.OwnerKind, caller.Value.OwnerId, provider, cancellationToken);

        if (resolved.IsFailure)
        {
            return Result.Failure<IntegrationSyncStateResponse>(resolved.Error);
        }

        var run = IntegrationSyncRun.Start(
            IntegrationSyncRunId.From(_guids.NewSequentialGuid()),
            resolved.Value.Connection.Id,
            caller.Value.OwnerId,
            provider.Value,
            calendarId,
            direction.Value,
            now);

        _runs.Add(run);

        IntegrationsTelemetry.SyncsStarted.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("direction", direction.Value.ToString()));

        await ReconcileAsync(adapter, resolved.Value, run, cancellationToken);

        if (run.Phase is not SyncPhase.Synced)
        {
            await _events.PublishAsync(
                new IntegrationSyncFailedIntegrationEvent
                {
                    UserId = caller.Value.OwnerId,
                    Provider = provider.Value,
                    ProviderDomain = provider.Domain,
                    Phase = run.Phase is SyncPhase.Partial ? "partial" : "failed",
                    ItemsSynced = run.ItemsSynced,
                    ItemsFailed = run.ItemsFailed,
                    NeedsAttention = run.NeedsAttention,
                    CompletedAt = run.CompletedAt ?? _clock.UtcNow
                },
                cancellationToken);

            IntegrationsTelemetry.SyncFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider.Value),
                new KeyValuePair<string, object?>("phase", run.Phase.ToString()));
        }

        _runs.Update(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.SyncDuration.Record(
            ((run.CompletedAt ?? _clock.UtcNow) - run.StartedAt).TotalMilliseconds,
            new KeyValuePair<string, object?>("provider", provider.Value),
            new KeyValuePair<string, object?>("phase", run.Phase.ToString()));

        return Result.Success(IntegrationMapper.ToSyncState(run));
    }

    /// <summary>
    /// Confirms that what this platform wrote is still there.
    /// <para>
    /// <b>The whole of the export direction's reconciliation, and it is deliberately narrow.</b> Each
    /// link this platform holds is read back; one the provider no longer has is marked
    /// <c>RemovedAtProvider</c> rather than rewritten, because a member deleting an exported entry in
    /// their own calendar is a legitimate act and putting it back would be the integration arguing
    /// with them.
    /// </para>
    /// <para>
    /// An adapter that cannot read an entry back closes the run as <c>failed</c> with the reason
    /// stated. It does not close as <c>synced</c> having checked nothing — that is precisely the
    /// fake success §49 forbids, and it is the easiest possible thing to write here.
    /// </para>
    /// </summary>
    private async Task ReconcileAsync(
        IExternalCalendarProvider adapter,
        IntegrationAccess access,
        IntegrationSyncRun run,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (!adapter.Capabilities.Contains(ExternalCalendarCapability.ReadEvent))
        {
            run.Fail(
                "This calendar provider cannot confirm whether an entry is still present, so a "
                + "synchronisation cannot report an outcome.",
                needsAttention: false,
                now);

            return;
        }

        var links = await _links.ListForConnectionAsync(access.Connection.Id, cancellationToken);

        var scoped = run.CalendarId is { } calendarId
            ? links.Where(link => string.Equals(link.CalendarId, calendarId, StringComparison.Ordinal))
            : links;

        var confirmed = 0;
        var reasons = new List<string>();

        foreach (var link in scoped.Where(link => link.State is CalendarLinkState.Written))
        {
            var outcome = await adapter.FindEventAsync(
                access.AccessToken, link.CalendarId, link.ExternalEventId, cancellationToken);

            if (!outcome.Succeeded)
            {
                // An authorization failure ends the run rather than accumulating one reason per
                // entry: the grant is gone, every remaining call would fail identically, and a
                // member does not need twelve copies of the same sentence.
                if (outcome.FailureKind is IntegrationFailureKind.Unauthorized)
                {
                    await _access.ObserveAsync(
                        access.Connection, outcome, _correlation.CorrelationId, cancellationToken);

                    run.Fail(
                        outcome.Message ?? "This calendar authorization has ended.",
                        needsAttention: true,
                        now);

                    return;
                }

                reasons.Add(outcome.Message ?? "An entry could not be checked.");

                continue;
            }

            if (outcome.Value is null)
            {
                link.RecordRemovalAtProvider(now);
                _links.Update(link);

                _logger.LogDebug(
                    "A calendar entry written for source {SourceKind} is no longer present at the "
                    + "provider and has been marked as removed there.",
                    link.SourceKind);

                continue;
            }

            link.RecordAttempt(now);
            _links.Update(link);
            confirmed++;
        }

        run.Complete(confirmed, reasons, needsAttention: false, now);

        access.Connection.RecordSuccess(now);
    }

    private static string LeaseKey(Guid ownerId, string provider, string? calendarId) =>
        $"integrations:sync:{ownerId:N}:{provider}:{calendarId ?? "*"}";

    internal static SyncDirection? ParseDirection(string? direction) =>
        direction?.Trim().ToLowerInvariant() switch
        {
            "export" => SyncDirection.Export,
            "import" => SyncDirection.Import,
            "both" => SyncDirection.Both,
            _ => null
        };
}

// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>The state of the most recent synchronisation for a key.</summary>
public sealed record GetCalendarSyncStatusQuery : IQuery<IntegrationSyncStateResponse>
{
    public required string Provider { get; init; }

    public string? CalendarId { get; init; }
}

public sealed class GetCalendarSyncStatusQueryHandler
    : IQueryHandler<GetCalendarSyncStatusQuery, IntegrationSyncStateResponse>
{
    private readonly IIntegrationSyncRunRepository _runs;
    private readonly ICurrentUser _currentUser;

    public GetCalendarSyncStatusQueryHandler(
        IIntegrationSyncRunRepository runs, ICurrentUser currentUser)
    {
        _runs = runs;
        _currentUser = currentUser;
    }

    public async Task<Result<IntegrationSyncStateResponse>> Handle(
        GetCalendarSyncStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = IntegrationCaller.From(_currentUser.UserId?.Value);

        if (caller.IsFailure)
        {
            return Result.Failure<IntegrationSyncStateResponse>(caller.Error);
        }

        if (!ProviderKey.TryParse(query.Provider, out var provider))
        {
            return Result.Failure<IntegrationSyncStateResponse>(
                IntegrationErrors.ProviderUnknown(query.Provider));
        }

        var run = await _runs.FindLatestAsync(
            caller.Value.OwnerId,
            provider.Value,
            string.IsNullOrWhiteSpace(query.CalendarId) ? null : query.CalendarId.Trim(),
            cancellationToken);

        // Null yields `never`, not a zeroed `synced`. The two render identically and mean opposite
        // things.
        return Result.Success(IntegrationMapper.ToSyncState(run));
    }
}
