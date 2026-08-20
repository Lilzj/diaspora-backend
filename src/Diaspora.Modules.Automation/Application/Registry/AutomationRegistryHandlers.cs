using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Registry;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-001 · GAP-027 · GAP-029 — the registry, its audit trail and its concurrency.
//
// Every handler here re-derives authority rather than trusting a scope the client sent. §49: frontend
// state is never a permission, an audience or a scope, and a caller who edits an automation id, an
// owner id or a scope in a request receives a refusal rather than being stopped by the client.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A page of the caller's automations.</summary>
public sealed record ListAutomationsQuery : IQuery<AutomationPageResponse>
{
    public string? OwnerKind { get; init; }

    public Guid? OwnerId { get; init; }

    public string? Status { get; init; }

    public string? Domain { get; init; }

    public Guid? CommunityId { get; init; }

    public string? Search { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

/// <summary>The envelope's <c>data</c> and <c>meta</c>, joined the way the client reads them.</summary>
public sealed record AutomationPageResponse
{
    public required IReadOnlyList<AutomationDefinitionResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public int? TotalCount { get; init; }
}

public sealed class ListAutomationsQueryHandler
    : IQueryHandler<ListAutomationsQuery, AutomationPageResponse>
{
    private const int DefaultLimit = 20;
    private const int MaximumLimit = 100;

    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;

    public ListAutomationsQueryHandler(
        IAutomationRepository automations, AutomationAccessService access)
    {
        _automations = automations;
        _access = access;
    }

    public async Task<Result<AutomationPageResponse>> Handle(
        ListAutomationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationPageResponse>(member.Error);
        }

        // Self by default. A caller naming an owner scope they have no role on receives 403 rather
        // than an empty list: an empty list is indistinguishable from "there are none" and teaches
        // the client the wrong thing about a community it already knows exists.
        var ownerKind = ParseOwnerKind(query.OwnerKind) ?? AutomationOwnerKind.User;
        var ownerId = query.OwnerId ?? member.Value.Value;

        if (ownerKind != AutomationOwnerKind.User || ownerId != member.Value.Value)
        {
            var authorized = await _access.AuthorizeScopeAsync(
                member.Value, ownerKind, ownerId, cancellationToken);

            if (authorized.IsFailure)
            {
                return Result.Failure<AutomationPageResponse>(authorized.Error);
            }
        }

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaximumLimit);

        var page = await _automations.ListAsync(
            ownerKind,
            ownerId,
            ParseStatus(query.Status),
            query.Domain,
            query.CommunityId,
            query.Search,
            query.Cursor,
            limit,
            cancellationToken);

        return Result.Success(new AutomationPageResponse
        {
            Items = [.. page.Items.Select(AutomationMapper.ToResponse)],
            NextCursor = page.NextCursor,
            TotalCount = page.TotalCount
        });
    }

    internal static AutomationOwnerKind? ParseOwnerKind(string? value) =>
        Enum.TryParse<AutomationOwnerKind>(value, ignoreCase: true, out var parsed) ? parsed : null;

    internal static AutomationStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The client's filter tabs use its own vocabulary. `attention` is the tab; the state is
        // NEEDS_ATTENTION, and matching only the enum name would make the tab silently empty.
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);

        return string.Equals(normalized, "attention", StringComparison.OrdinalIgnoreCase)
            ? AutomationStatus.NeedsAttention
            : Enum.TryParse<AutomationStatus>(normalized, ignoreCase: true, out var parsed)
                ? parsed
                : null;
    }
}

/// <summary>One automation, with its rule and its audit trail.</summary>
public sealed record GetAutomationQuery : IQuery<AutomationDefinitionResponse>
{
    public required Guid AutomationId { get; init; }
}

public sealed class GetAutomationQueryHandler
    : IQueryHandler<GetAutomationQuery, AutomationDefinitionResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;

    public GetAutomationQueryHandler(
        IAutomationRepository automations, AutomationAccessService access)
    {
        _automations = automations;
        _access = access;
    }

    public async Task<Result<AutomationDefinitionResponse>> Handle(
        GetAutomationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await AutomationLoader.LoadAsync(
            _automations, _access, AutomationId.From(query.AutomationId), requireManage: false,
            withAudit: true, cancellationToken);

        return loaded.IsFailure
            ? Result.Failure<AutomationDefinitionResponse>(loaded.Error)
            : Result.Success(AutomationMapper.ToResponse(loaded.Value.Automation));
    }
}

/// <summary>An automation's audit trail. Owner or manager only.</summary>
public sealed record GetAutomationAuditQuery : IQuery<IReadOnlyList<AutomationAuditEntryResponse>>
{
    public required Guid AutomationId { get; init; }
}

public sealed class GetAutomationAuditQueryHandler
    : IQueryHandler<GetAutomationAuditQuery, IReadOnlyList<AutomationAuditEntryResponse>>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;

    public GetAutomationAuditQueryHandler(
        IAutomationRepository automations, AutomationAccessService access)
    {
        _automations = automations;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<AutomationAuditEntryResponse>>> Handle(
        GetAutomationAuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var loaded = await AutomationLoader.LoadAsync(
            _automations, _access, AutomationId.From(query.AutomationId), requireManage: false,
            withAudit: true, cancellationToken);

        return loaded.IsFailure
            ? Result.Failure<IReadOnlyList<AutomationAuditEntryResponse>>(loaded.Error)
            : Result.Success(AutomationMapper.ToResponse(loaded.Value.Automation).Audit);
    }
}

/// <summary>
/// Creates an automation.
/// <para>
/// <c>[Transactional]</c> on the command rather than the handler, per DSP4003: the behaviour reads the
/// attribute off the request type, and on a handler it compiles, reads as a guarantee and opens
/// nothing. It is needed because the handler writes the automation and then synchronises the
/// occurrence store, and a failure between them would leave a rule that is active and never due.
/// </para>
/// </summary>
[Transactional(Reason = "An automation and its first scheduled occurrence are written together, or neither.")]
public sealed record CreateAutomationCommand : ICommand<AutomationDefinitionResponse>
{
    public required CreateAutomationRequest Request { get; init; }
}

public sealed class CreateAutomationCommandHandler
    : ICommandHandler<CreateAutomationCommand, AutomationDefinitionResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;
    private readonly AutomationRuleValidator _validator;
    private readonly AutomationScheduleService _schedule;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;

    public CreateAutomationCommandHandler(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationRuleValidator validator,
        AutomationScheduleService schedule,
        IIntegrationEventPublisher publisher,
        IClock clock,
        IOptions<AutomationOptions> options)
    {
        _automations = automations;
        _access = access;
        _validator = validator;
        _schedule = schedule;
        _publisher = publisher;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<AutomationDefinitionResponse>> Handle(
        CreateAutomationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(member.Error);
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Result.Failure<AutomationDefinitionResponse>(
                AutomationErrors.InvalidSchedule(
                    "Creating an automation needs an idempotency key, so a retried request cannot "
                    + "give you two identical automations."));
        }

        // The idempotency read, scoped to the member as well as the key. A key alone would let one
        // member's retry return another member's automation.
        var existing = await _automations.FindByIdempotencyKeyAsync(
            member.Value, request.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(AutomationMapper.ToResponse(existing));
        }

        var ownerKind = ListAutomationsQueryHandler.ParseOwnerKind(request.Scope?.OwnerKind)
                        ?? AutomationOwnerKind.User;
        var ownerId = request.Scope?.OwnerId ?? member.Value.Value;

        var scope = await _access.AuthorizeScopeAsync(
            member.Value, ownerKind, ownerId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(scope.Error);
        }

        var activating = string.Equals(request.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);

        var validated = _validator.Validate(
            request.Trigger?.Key ?? string.Empty,
            request.Trigger?.Parameters,
            [.. (request.Conditions ?? []).Select(ToInput)],
            [.. (request.Actions ?? []).Select(ToInput)],
            ownerKind,
            scope.Value.Capabilities,
            activating);

        if (validated.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(validated.Error);
        }

        if (activating)
        {
            var active = await _automations.CountActiveAsync(ownerKind, ownerId, cancellationToken);

            if (active >= _options.Value.MaximumActiveAutomationsPerOwner)
            {
                return Result.Failure<AutomationDefinitionResponse>(
                    AutomationErrors.TooManyActive(_options.Value.MaximumActiveAutomationsPerOwner));
            }
        }

        var now = _clock.UtcNow;

        var automation = AutomationDefinition.Create(
            ownerKind,
            ownerId,
            scope.Value.Label,
            member.Value,
            request.Name,
            request.Description ?? string.Empty,
            ParseSource(request.Source),
            request.TemplateId,
            request.Trigger!.Key,
            request.Trigger.Parameters ?? "{}",
            activating,
            now,
            request.IdempotencyKey);

        automation.SetScope(
            request.Scope?.CommunityIds ?? [],
            request.Scope?.IsCrossCommunity ?? false,
            request.Scope?.CountryCodes ?? [],
            request.Scope?.NeighbourhoodIds ?? []);

        automation.ReplaceConditions(
            (request.Conditions ?? []).Select(condition =>
                ((AutomationConditionId?)null, condition.Key, condition.Operator, condition.Value,
                    condition.SecondaryValue)));

        automation.ReplaceActions(
            (request.Actions ?? []).Select(action =>
                ((AutomationActionId?)null, action.Key, action.Parameters ?? "{}")));

        var scheduled = AutomationScheduleBinder.Apply(automation, request.Schedule);

        if (scheduled.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(scheduled.Error);
        }

        _automations.Add(automation);

        // B24-G15 · B24-G19. Published inside the write's own transaction, through the outbox, so the
        // fact and the row it describes commit together or not at all — §31, and the reason B24 adds
        // no second delivery path of its own. The counter below is recorded after the save for the
        // mirror-image reason: a metric is not transactional, so it must not be moved until the thing
        // it counts is real.
        await _publisher.PublishAsync(
            AutomationLifecycle.Changed(
                automation, AutomationTelemetry.Tags.Change.Created, _clock.UtcNow),
            cancellationToken);

        await _automations.SaveChangesAsync(cancellationToken);

        AutomationTelemetry.LifecycleChanged(
            AutomationTelemetry.Tags.Change.Created, automation.OwnerKind.ToString());

        await _schedule.SynchroniseAsync(automation, cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(automation));
    }

    internal static ConditionInput ToInput(AutomationConditionRequest condition) =>
        new(condition.Id, condition.Key, condition.Operator, condition.Value, condition.SecondaryValue);

    internal static ActionInput ToInput(AutomationActionRequest action) =>
        new(action.Id, action.Key, action.Parameters);

    private static AutomationSource ParseSource(string? value) =>
        Enum.TryParse<AutomationSource>(value, ignoreCase: true, out var parsed)
            ? parsed
            : AutomationSource.Builder;
}

/// <summary>Changes an automation's name, rule or schedule, under a concurrency precondition.</summary>
[Transactional(Reason = "The rule and its rescheduled occurrence change together, or neither does.")]
public sealed record UpdateAutomationCommand : ICommand<AutomationDefinitionResponse>
{
    public required Guid AutomationId { get; init; }

    public required UpdateAutomationRequest Request { get; init; }
}

public sealed class UpdateAutomationCommandHandler
    : ICommandHandler<UpdateAutomationCommand, AutomationDefinitionResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;
    private readonly AutomationRuleValidator _validator;
    private readonly AutomationScheduleService _schedule;
    private readonly IClock _clock;

    public UpdateAutomationCommandHandler(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationRuleValidator validator,
        AutomationScheduleService schedule,
        IClock clock)
    {
        _automations = automations;
        _access = access;
        _validator = validator;
        _schedule = schedule;
        _clock = clock;
    }

    public async Task<Result<AutomationDefinitionResponse>> Handle(
        UpdateAutomationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loaded = await AutomationLoader.LoadAsync(
            _automations, _access, AutomationId.From(command.AutomationId), requireManage: false,
            withAudit: false, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(loaded.Error);
        }

        var (automation, grant, member) = loaded.Value;
        var request = command.Request;

        // The concurrency precondition. A 409 carries the current definition so the client can show
        // the difference rather than a generic error, and a blind retry is never correct — it means
        // somebody else changed something.
        //
        // Compared at millisecond resolution rather than with a tolerance. A tolerance wide enough to
        // absorb a round-trip is wide enough to absorb a second writer: two edits inside the window
        // would both be accepted and the first one's change would vanish, which is precisely the
        // failure the precondition exists to make loud. Milliseconds survive the ISO-8601 round trip
        // the client makes, and sub-millisecond ticks do not, so that is where the comparison sits.
        if (request.ExpectedUpdatedAt is { } expected &&
            ToMilliseconds(automation.UpdatedAt) != ToMilliseconds(expected))
        {
            return Result.Failure<AutomationDefinitionResponse>(
                AutomationErrors.ConcurrencyConflict());
        }

        var triggerKey = request.Trigger?.Key ?? automation.TriggerKey;

        var conditions = request.Conditions is null
            ? [.. automation.Conditions.Select(condition =>
                new ConditionInput(
                    condition.Id.Value, condition.Key, condition.Operator, condition.Value,
                    condition.SecondaryValue))]
            : (IReadOnlyList<ConditionInput>)
                [.. request.Conditions.Select(CreateAutomationCommandHandler.ToInput)];

        var actions = request.Actions is null
            ? [.. automation.Actions.Select(action =>
                new ActionInput(action.Id.Value, action.Key, action.Parameters))]
            : (IReadOnlyList<ActionInput>)
                [.. request.Actions.Select(CreateAutomationCommandHandler.ToInput)];

        var validated = _validator.Validate(
            triggerKey,
            request.Trigger?.Parameters ?? automation.TriggerParameters,
            conditions,
            actions,
            automation.OwnerKind,
            grant.Capabilities,

            // An edit to an already-active automation is an activation as far as availability goes:
            // it must not be possible to switch a rule to an unobservable trigger while it is running.
            automation.Status == AutomationStatus.Active);

        if (validated.IsFailure)
        {
            return Result.Failure<AutomationDefinitionResponse>(validated.Error);
        }

        automation.Rename(request.Name ?? automation.Name, request.Description ?? automation.Description);

        if (request.Trigger is not null)
        {
            automation.SetTrigger(request.Trigger.Key, request.Trigger.Parameters ?? "{}");
        }

        if (request.Conditions is not null)
        {
            automation.ReplaceConditions(
                request.Conditions.Select(condition =>
                    (condition.Id is { } id ? AutomationConditionId.From(id) : null,
                        condition.Key, condition.Operator, condition.Value, condition.SecondaryValue)));
        }

        if (request.Actions is not null)
        {
            automation.ReplaceActions(
                request.Actions.Select(action =>
                    (action.Id is { } id ? AutomationActionId.From(id) : null,
                        action.Key, action.Parameters ?? "{}")));
        }

        if (request.Scope is not null)
        {
            // The owner is never re-assigned by an edit. A rule that could change hands would be a
            // rule whose authority was checked against one scope and executed against another.
            automation.SetScope(
                request.Scope.CommunityIds ?? [],
                request.Scope.IsCrossCommunity,
                request.Scope.CountryCodes ?? [],
                request.Scope.NeighbourhoodIds ?? []);
        }

        if (request.Schedule is not null)
        {
            var scheduled = AutomationScheduleBinder.Apply(automation, request.Schedule);

            if (scheduled.IsFailure)
            {
                return Result.Failure<AutomationDefinitionResponse>(scheduled.Error);
            }
        }

        automation.RecordUpdate(member, grant.Label, _clock.UtcNow);

        await _automations.SaveChangesAsync(cancellationToken);

        AutomationTelemetry.LifecycleChanged(
            AutomationTelemetry.Tags.Change.Updated, automation.OwnerKind.ToString());

        await _schedule.SynchroniseAsync(automation, cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(automation));
    }

    /// <summary>The precondition's resolution: what survives a JSON round trip, and no more.</summary>
    private static long ToMilliseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
}

/// <summary>Activates, pauses, disables or drafts an automation.</summary>
[Transactional(Reason = "A lifecycle change and its cancelled occurrences settle together.")]
public sealed record ChangeAutomationStatusCommand : ICommand<AutomationStatusChangedResponse>
{
    public required Guid AutomationId { get; init; }

    public required string Status { get; init; }
}

public sealed class ChangeAutomationStatusCommandHandler
    : ICommandHandler<ChangeAutomationStatusCommand, AutomationStatusChangedResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;
    private readonly AutomationRuleValidator _validator;
    private readonly AutomationScheduleService _schedule;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;

    public ChangeAutomationStatusCommandHandler(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationRuleValidator validator,
        AutomationScheduleService schedule,
        IIntegrationEventPublisher publisher,
        IClock clock,
        IOptions<AutomationOptions> options)
    {
        _automations = automations;
        _access = access;
        _validator = validator;
        _schedule = schedule;
        _publisher = publisher;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<AutomationStatusChangedResponse>> Handle(
        ChangeAutomationStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<AutomationStatus>(
                command.Status.Replace("_", string.Empty, StringComparison.Ordinal),
                ignoreCase: true,
                out var target))
        {
            return Result.Failure<AutomationStatusChangedResponse>(
                AutomationErrors.InvalidSchedule($"'{command.Status}' is not a state to move to."));
        }

        var loaded = await AutomationLoader.LoadAsync(
            _automations, _access, AutomationId.From(command.AutomationId), requireManage: false,
            withAudit: false, cancellationToken);

        if (loaded.IsFailure)
        {
            return Result.Failure<AutomationStatusChangedResponse>(loaded.Error);
        }

        var (automation, grant, member) = loaded.Value;

        // Switching on is where availability is enforced. A rule may be kept as a draft against a
        // capability that has not shipped; it may not be switched on to never fire.
        if (target == AutomationStatus.Active)
        {
            var validated = _validator.Validate(
                automation.TriggerKey,
                automation.TriggerParameters,
                [.. automation.Conditions.Select(condition =>
                    new ConditionInput(
                        condition.Id.Value, condition.Key, condition.Operator, condition.Value,
                        condition.SecondaryValue))],
                [.. automation.Actions.Select(action =>
                    new ActionInput(action.Id.Value, action.Key, action.Parameters))],
                automation.OwnerKind,
                grant.Capabilities,
                activating: true);

            if (validated.IsFailure)
            {
                return Result.Failure<AutomationStatusChangedResponse>(validated.Error);
            }

            var active = await _automations.CountActiveAsync(
                automation.OwnerKind, automation.OwnerId, cancellationToken);

            if (active >= _options.Value.MaximumActiveAutomationsPerOwner)
            {
                return Result.Failure<AutomationStatusChangedResponse>(
                    AutomationErrors.TooManyActive(_options.Value.MaximumActiveAutomationsPerOwner));
            }
        }

        var from = automation.Status;

        if (!automation.TransitionTo(target, member, grant.Label, _clock.UtcNow))
        {
            return Result.Failure<AutomationStatusChangedResponse>(
                AutomationErrors.InvalidStatusTransition(
                    AutomationMapper.StatusName(from), AutomationMapper.StatusName(target)));
        }

        var change = ChangeNameFor(target);

        // Only the three transitions the vocabulary names produce a fact. A move back to draft is a
        // real change and is counted below, but nothing downstream distinguishes it from an edit, and
        // an outbox row nobody consumes is cost with no reader.
        if (change is not AutomationTelemetry.Tags.Change.Updated)
        {
            await _publisher.PublishAsync(
                AutomationLifecycle.Changed(automation, change, _clock.UtcNow), cancellationToken);
        }

        await _automations.SaveChangesAsync(cancellationToken);

        AutomationTelemetry.LifecycleChanged(change, automation.OwnerKind.ToString());

        // A pause must cancel pending occurrences rather than merely stop new ones being created, and
        // the count is what lets the UI say what happened rather than imply it.
        var cancelled = await _schedule.SynchroniseAsync(automation, cancellationToken);

        return Result.Success(new AutomationStatusChangedResponse
        {
            Automation = AutomationMapper.ToResponse(automation),
            CancelledRuns = cancelled
        });
    }

    /// <summary>
    /// The lifecycle tag for a status transition.
    /// <para>
    /// <c>Paused</c> and <c>Disabled</c> stay apart because members use both and mean different
    /// things by them — "stop for now" against "stop, and stop counting" — and a metric that merged
    /// them would answer neither of the two questions anybody asks of it. Draft, expired and
    /// needs-attention are reached by the platform rather than by this route and are counted where
    /// they happen.
    /// </para>
    /// </summary>
    private static string ChangeNameFor(AutomationStatus target) => target switch
    {
        AutomationStatus.Active => AutomationTelemetry.Tags.Change.Activated,
        AutomationStatus.Paused => AutomationTelemetry.Tags.Change.Paused,
        AutomationStatus.Disabled => AutomationTelemetry.Tags.Change.Disabled,
        _ => AutomationTelemetry.Tags.Change.Updated
    };
}

/// <summary>
/// Deletes an automation.
/// <para>
/// Idempotent: deleting an already-deleted automation answers 200 with the same body, not 404. §40
/// requires the UI never be left showing an automation that is gone, and a 404 on the retry is exactly
/// how it gets left there.
/// </para>
/// </summary>
[Transactional(Reason = "The automation and its pending occurrences are removed together.")]
public sealed record DeleteAutomationCommand : ICommand<AutomationDeletedResponse>
{
    public required Guid AutomationId { get; init; }
}

public sealed class DeleteAutomationCommandHandler
    : ICommandHandler<DeleteAutomationCommand, AutomationDeletedResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;
    private readonly AutomationScheduleService _schedule;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IClock _clock;

    public DeleteAutomationCommandHandler(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationScheduleService schedule,
        IIntegrationEventPublisher publisher,
        IClock clock)
    {
        _automations = automations;
        _access = access;
        _schedule = schedule;
        _publisher = publisher;
        _clock = clock;
    }

    public async Task<Result<AutomationDeletedResponse>> Handle(
        DeleteAutomationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationDeletedResponse>(member.Error);
        }

        var automation = await _automations.GetWithRuleAsync(
            AutomationId.From(command.AutomationId), cancellationToken);

        if (automation is null)
        {
            // The idempotent answer. A retry after a timeout must not leave the client showing an
            // automation that is gone.
            return Result.Success(new AutomationDeletedResponse
            {
                Id = command.AutomationId.ToString("D"),
                Deleted = true,
                CancelledRuns = 0
            });
        }

        var authorized = await _access.AuthorizeAutomationAsync(
            member.Value, automation.OwnerKind, automation.OwnerId, automation.CreatedByUserId,
            requireManage: false, cancellationToken);

        if (authorized.IsFailure)
        {
            return Result.Failure<AutomationDeletedResponse>(authorized.Error);
        }

        var cancelled = await _schedule.CancelPendingAsync(
            automation, "The automation was deleted.", cancellationToken);

        automation.RecordDeletion(member.Value, authorized.Value.Label, _clock.UtcNow);
        _automations.Remove(automation);

        await _publisher.PublishAsync(
            AutomationLifecycle.Changed(
                automation, AutomationTelemetry.Tags.Change.Deleted, _clock.UtcNow),
            cancellationToken);

        await _automations.SaveChangesAsync(cancellationToken);

        AutomationTelemetry.LifecycleChanged(
            AutomationTelemetry.Tags.Change.Deleted, automation.OwnerKind.ToString());

        return Result.Success(new AutomationDeletedResponse
        {
            Id = command.AutomationId.ToString("D"),
            Deleted = true,
            CancelledRuns = cancelled
        });
    }
}

/// <summary>
/// The load-and-authorize step every handler above shares.
/// <para>
/// One place, so a handler cannot forget it, and one refusal — <see cref="AutomationErrors.NotFound"/>
/// — for both "does not exist" and "not yours", because a 403 on somebody else's automation id
/// confirms the id exists.
/// </para>
/// </summary>
internal static class AutomationLoader
{
    internal static async Task<Result<(AutomationDefinition Automation,
        AutomationScopeGrant Grant, SharedKernel.Identity.UserId Member)>> LoadAsync(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationId id,
        bool requireManage,
        bool withAudit,
        CancellationToken cancellationToken)
    {
        var member = access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<(AutomationDefinition, AutomationScopeGrant,
                SharedKernel.Identity.UserId)>(member.Error);
        }

        var automation = withAudit
            ? await automations.GetWithAuditAsync(id, cancellationToken)
            : await automations.GetWithRuleAsync(id, cancellationToken);

        if (automation is null)
        {
            return Result.Failure<(AutomationDefinition, AutomationScopeGrant,
                SharedKernel.Identity.UserId)>(AutomationErrors.NotFound());
        }

        var authorized = await access.AuthorizeAutomationAsync(
            member.Value, automation.OwnerKind, automation.OwnerId, automation.CreatedByUserId,
            requireManage, cancellationToken);

        return authorized.IsFailure
            ? Result.Failure<(AutomationDefinition, AutomationScopeGrant,
                SharedKernel.Identity.UserId)>(authorized.Error)
            : Result.Success((automation, authorized.Value, member.Value));
    }
}

/// <summary>
/// Applies a schedule request to an automation, refusing what cannot be run.
/// <para>
/// The timezone is checked here rather than only at execution: a schedule saved with an identifier
/// this deployment cannot resolve is a schedule that will never fire, and the member should be told at
/// the moment they set it rather than by its silence.
/// </para>
/// </summary>
internal static class AutomationScheduleBinder
{
    internal static Result Apply(AutomationDefinition automation, AutomationScheduleRequest? request)
    {
        if (request is null)
        {
            automation.ClearSchedule();
            return Result.Success();
        }

        if (!Enum.TryParse<AutomationFrequency>(request.Frequency, ignoreCase: true, out var frequency))
        {
            return Result.Failure(
                AutomationErrors.InvalidSchedule($"'{request.Frequency}' is not a frequency."));
        }

        if (!Enum.TryParse<AutomationTimezoneMode>(
                request.TimezoneMode, ignoreCase: true, out var mode))
        {
            mode = AutomationTimezoneMode.Device;
        }

        if (request.TimeZoneId is { Length: > 0 } zone && !IsKnownZone(zone))
        {
            return Result.Failure(AutomationErrors.UnknownTimeZone(zone));
        }

        if (request.StartsAt is { } starts && request.EndsAt is { } ends && ends <= starts)
        {
            return Result.Failure(
                AutomationErrors.InvalidSchedule("The end date has to be after the start date."));
        }

        if (request.DayOfMonth is { } day and (< 1 or > 31))
        {
            return Result.Failure(
                AutomationErrors.InvalidSchedule($"There is no {day}th in any month."));
        }

        if (request.Hour is { } hour and (< 0 or > 23))
        {
            return Result.Failure(AutomationErrors.InvalidSchedule("That is not an hour of the day."));
        }

        if (request.Minute is { } minute and (< 0 or > 59))
        {
            return Result.Failure(AutomationErrors.InvalidSchedule("That is not a minute of the hour."));
        }

        automation.SetSchedule(
            frequency,
            mode,
            request.TimeZoneId,
            request.Hour,
            request.Minute,
            request.DaysOfWeek,
            request.DayOfMonth,
            request.StartsAt,
            request.EndsAt,
            request.MaxExecutions,
            request.EventOffsetMinutes,
            request.EventOffsetAnchor,
            request.CustomDescription);

        return Result.Success();
    }

    private static bool IsKnownZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
