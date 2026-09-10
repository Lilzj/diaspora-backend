using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Registry;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Extensibility.Scopes;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.Execution;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-002 · GAP-004 · GAP-005 · GAP-006 · GAP-026 — running, history, capabilities,
// the dry run and the audience estimate.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>A page of the caller's execution history.</summary>
public sealed record ListRunsQuery : IQuery<AutomationRunPageResponse>
{
    public Guid? AutomationId { get; init; }

    public string? Status { get; init; }

    /// <summary>B24 · §21. "Show me everything that timed out" — which no status filter can ask.</summary>
    public string? FailureKind { get; init; }

    /// <summary>B24 · §21. One trigger's history, across every automation that watches it.</summary>
    public string? TriggerKey { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

public sealed record AutomationRunPageResponse
{
    public required IReadOnlyList<AutomationRunResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public int? TotalCount { get; init; }
}

public sealed class ListRunsQueryHandler : IQueryHandler<ListRunsQuery, AutomationRunPageResponse>
{
    private const int DefaultLimit = 20;
    private const int MaximumLimit = 100;

    private readonly IAutomationRunRepository _runs;
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;

    public ListRunsQueryHandler(
        IAutomationRunRepository runs,
        IAutomationRepository automations,
        AutomationAccessService access)
    {
        _runs = runs;
        _automations = automations;
        _access = access;
    }

    public async Task<Result<AutomationRunPageResponse>> Handle(
        ListRunsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationRunPageResponse>(member.Error);
        }

        // The owner is the token's, always. §66: the route takes no owner parameter precisely so a
        // member cannot read another member's runs by supplying one.
        var owner = member.Value.Value;

        // ── The scope a run belongs to is not always the member's own — B23.x Wave 12 ────────────
        //
        // A run records the *automation's* owner, which for an organization automation is the
        // organization. Filtering history on the caller's own identifier therefore hid every run of
        // every scope-owned automation from the very people who administer it. That was invisible in
        // Wave 11 because `automations.organization` was granted to nobody, so no scope-owned
        // automation could exist; B12's authority made it reachable and made this visible.
        //
        // Narrowing to one automation the caller may read is the fix, and it is the same
        // authorization every detail route already performs: the scope authority answers whether they
        // may see it, and an identifier they may not read is refused rather than answered emptily.
        // With no automation named, the member's own runs are all they get — unchanged.
        if (query.AutomationId is { } automationId)
        {
            var automation = await _automations.GetAsync(
                AutomationId.From(automationId), cancellationToken);

            if (automation is null)
            {
                return Result.Failure<AutomationRunPageResponse>(AutomationErrors.NotFound());
            }

            var scope = await _access.AuthorizeScopeAsync(
                member.Value, automation.OwnerKind, automation.OwnerId, cancellationToken);

            if (scope.IsFailure)
            {
                // 404 rather than 403 on a foreign identifier, matching every other route here: a
                // distinguishable refusal is an existence oracle.
                return Result.Failure<AutomationRunPageResponse>(AutomationErrors.NotFound());
            }

            owner = automation.OwnerId;
        }

        var page = await _runs.ListAsync(
            owner,
            query.AutomationId is { } id ? AutomationId.From(id) : null,
            ParseRunStatus(query.Status),
            ParseFailureKind(query.FailureKind),
            NormaliseTriggerKey(query.TriggerKey),
            query.From,
            query.To,
            query.Cursor,
            Math.Clamp(query.Limit ?? DefaultLimit, 1, MaximumLimit),
            cancellationToken);

        return Result.Success(new AutomationRunPageResponse
        {
            Items = [.. page.Items.Select(AutomationMapper.ToResponse)],
            NextCursor = page.NextCursor,
            TotalCount = page.TotalCount
        });
    }

    private static AutomationRunStatus? ParseRunStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : Enum.TryParse<AutomationRunStatus>(
                value.Replace("_", string.Empty, StringComparison.Ordinal),
                ignoreCase: true,
                out var parsed)
                ? parsed
                : null;

    /// <summary>
    /// B24 · §21. The failure-kind filter.
    /// <para>
    /// An unrecognised value reads as no filter, exactly as an unrecognised status does. A refusal
    /// would be defensible and would break the client's own union the day it gains a thirteenth kind
    /// — and the request is a read, so the worst an unknown value produces is a wider page.
    /// </para>
    /// </summary>
    internal static AutomationFailureKind? ParseFailureKind(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : Enum.TryParse<AutomationFailureKind>(
                value.Replace("_", string.Empty, StringComparison.Ordinal),
                ignoreCase: true,
                out var parsed)
                ? parsed
                : null;

    /// <summary>
    /// B24 · §21. The trigger filter, checked against the catalogue rather than passed through.
    /// <para>
    /// <b>The one filter on this route that reaches a string column, so it is the one that is
    /// validated.</b> A key the catalogue does not have cannot match a stored run — every run's
    /// trigger came from the catalogue — so refusing it costs nothing and keeps an arbitrary
    /// caller-supplied string out of a predicate.
    /// </para>
    /// </summary>
    internal static string? NormaliseTriggerKey(string? value) =>
        string.IsNullOrWhiteSpace(value) || AutomationCatalog.FindTrigger(value.Trim()) is null
            ? null
            : value.Trim();
}

/// <summary>One run, with its per-condition and per-action outcomes.</summary>
public sealed record GetRunQuery : IQuery<AutomationRunResponse>
{
    public required Guid AutomationId { get; init; }

    public required Guid RunId { get; init; }
}

public sealed class GetRunQueryHandler : IQueryHandler<GetRunQuery, AutomationRunResponse>
{
    private readonly IAutomationRunRepository _runs;
    private readonly AutomationAccessService _access;

    public GetRunQueryHandler(IAutomationRunRepository runs, AutomationAccessService access)
    {
        _runs = runs;
        _access = access;
    }

    public async Task<Result<AutomationRunResponse>> Handle(
        GetRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationRunResponse>(member.Error);
        }

        var run = await _runs.GetWithOutcomesAsync(
            AutomationRunId.From(query.RunId), cancellationToken);

        // Ownership is checked on the run's own denormalised owner column rather than by joining the
        // definition, so a run whose definition was deleted is still refused to everybody else. A
        // mismatch answers not-found rather than forbidden: a 403 confirms the run id exists.
        if (run is null || run.AutomationId.Value != query.AutomationId)
        {
            return Result.Failure<AutomationRunResponse>(AutomationErrors.NotFound());
        }

        // The caller's own runs, or a scope's where they hold it — B23.x Wave 12.
        //
        // A run carries the *automation's* owner, which for an organization automation is the
        // organization rather than any member. Comparing it with the caller's identifier alone hid
        // every scope-owned run from the administrator who owns the scope. Invisible in Wave 11
        // because no scope-owned automation could exist; B12's authority made it reachable.
        //
        // The denormalised column still does the work for a personal run, so a run whose definition
        // was deleted is still refused to everybody else — the authority is consulted only when the
        // owner is not the caller.
        if (run.OwnerId != member.Value.Value)
        {
            var scope = await _access.AuthorizeScopeAsync(
                member.Value, run.OwnerKind, run.OwnerId, cancellationToken);

            if (scope.IsFailure)
            {
                return Result.Failure<AutomationRunResponse>(AutomationErrors.NotFound());
            }
        }

        return Result.Success(AutomationMapper.ToResponse(run));
    }
}

/// <summary>
/// Runs an automation the member asked to run.
/// <para>
/// Only ever for a <c>manual</c> trigger. "Run my community announcement now" is a broadcast with no
/// event behind it, and an engine that accepted it would let a client manufacture a trigger.
/// </para>
/// </summary>
public sealed record RunAutomationCommand : ICommand<AutomationRunResponse>
{
    public required Guid AutomationId { get; init; }

    public string? IdempotencyKey { get; init; }
}

public sealed class RunAutomationCommandHandler
    : ICommandHandler<RunAutomationCommand, AutomationRunResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly AutomationAccessService _access;
    private readonly AutomationEngine _engine;
    private readonly IClock _clock;

    public RunAutomationCommandHandler(
        IAutomationRepository automations,
        AutomationAccessService access,
        AutomationEngine engine,
        IClock clock)
    {
        _automations = automations;
        _access = access;
        _engine = engine;
        _clock = clock;
    }

    public async Task<Result<AutomationRunResponse>> Handle(
        RunAutomationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationRunResponse>(member.Error);
        }

        var automation = await _automations.GetWithRuleAsync(
            AutomationId.From(command.AutomationId), cancellationToken);

        if (automation is null)
        {
            return Result.Failure<AutomationRunResponse>(AutomationErrors.NotFound());
        }

        var authorized = await _access.AuthorizeAutomationAsync(
            member.Value, automation.OwnerKind, automation.OwnerId, automation.CreatedByUserId,
            requireManage: false, cancellationToken);

        if (authorized.IsFailure)
        {
            return Result.Failure<AutomationRunResponse>(authorized.Error);
        }

        var trigger = AutomationCatalog.FindTrigger(automation.TriggerKey);

        if (trigger is null || trigger.Delivery != AutomationTriggerDelivery.Manual)
        {
            return Result.Failure<AutomationRunResponse>(AutomationErrors.NotManuallyRunnable());
        }

        // The idempotency subject. A manual run inside the same key returns the run it already
        // produced rather than starting a second — the difference between one announcement and two.
        var key = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"manual:{Guid.NewGuid():N}"
            : $"manual:{command.IdempotencyKey}";

        var run = await _engine.RunOneAsync(
            automation,
            new AutomationTriggerContext
            {
                TriggerKey = automation.TriggerKey,
                TriggerEventId = key,
                ConcernedOwnerIds = [automation.OwnerId],
                Summary = "You ran this yourself.",
                OccurredAt = _clock.UtcNow,
                ManualActor = member.Value
            },
            AutomationRunReason.Manual,
            isSimulated: false,
            cancellationToken);

        return run is null
            ? Result.Failure<AutomationRunResponse>(
                AutomationErrors.RateLimited(
                    "This automation has run too many times recently. Try again shortly."))
            : Result.Success(AutomationMapper.ToResponse(run));
    }
}

/// <summary>What this member may do with automation.</summary>
public sealed record GetAutomationCapabilitiesQuery : IQuery<AutomationAccessResponse>
{
    public string? OwnerKind { get; init; }

    public Guid? OwnerId { get; init; }
}

public sealed class GetAutomationCapabilitiesQueryHandler
    : IQueryHandler<GetAutomationCapabilitiesQuery, AutomationAccessResponse>
{
    private readonly AutomationAccessService _access;

    public GetAutomationCapabilitiesQueryHandler(AutomationAccessService access) => _access = access;

    public async Task<Result<AutomationAccessResponse>> Handle(
        GetAutomationCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.DescribeAsync(
            ListAutomationsQueryHandler.ParseOwnerKind(query.OwnerKind),
            query.OwnerId,
            cancellationToken);

        return access.IsFailure
            ? Result.Failure<AutomationAccessResponse>(access.Error)
            : Result.Success(AutomationMapper.ToResponse(access.Value));
    }
}

/// <summary>
/// A dry run: evaluates against real state and performs nothing.
/// <para>
/// The refusal to act is enforced in the dispatcher rather than by this handler passing a flag —
/// a flag the dispatcher ignored once is a member's community receiving a test broadcast.
/// </para>
/// </summary>
public sealed record SimulateAutomationCommand : ICommand<AutomationSimulationResponse>
{
    public required SimulateAutomationRequest Request { get; init; }
}

public sealed class SimulateAutomationCommandHandler
    : ICommandHandler<SimulateAutomationCommand, AutomationSimulationResponse>
{
    private readonly AutomationAccessService _access;
    private readonly AutomationRuleValidator _validator;
    private readonly AutomationEngine _engine;
    private readonly AutomationScopeAuthorityResolver _scopes;
    private readonly AutomationSubjectContextService _subjects;
    private readonly ISubjectRateLimiter _rateLimiter;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;

    public SimulateAutomationCommandHandler(
        AutomationAccessService access,
        AutomationRuleValidator validator,
        AutomationEngine engine,
        AutomationScopeAuthorityResolver scopes,
        AutomationSubjectContextService subjects,
        ISubjectRateLimiter rateLimiter,
        IClock clock,
        IOptions<AutomationOptions> options)
    {
        _access = access;
        _validator = validator;
        _engine = engine;
        _scopes = scopes;
        _subjects = subjects;
        _rateLimiter = rateLimiter;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<AutomationSimulationResponse>> Handle(
        SimulateAutomationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationSimulationResponse>(member.Error);
        }

        // Rate limited even though it performs nothing: it reads authoritative state and estimates
        // audiences, and repeated queries with varying scopes are how an enumeration attack against a
        // segment is built.
        var lease = await _rateLimiter.AcquireAsync(
            $"automation.simulate:{member.Value.Value:N}",
            _options.Value.MaximumSimulationsPerHour,
            TimeSpan.FromHours(1),
            cancellationToken);

        if (!lease.IsAllowed)
        {
            return Result.Failure<AutomationSimulationResponse>(
                AutomationErrors.RateLimited(
                    "You have tested a lot of automations recently. Try again in a little while."));
        }

        var ownerKind = ListAutomationsQueryHandler.ParseOwnerKind(request.Scope?.OwnerKind)
                        ?? AutomationOwnerKind.User;
        var ownerId = request.Scope?.OwnerId ?? member.Value.Value;

        // The same capability check as creating it. A simulation that ran under weaker authorization
        // would be an audience-enumeration tool: "how many members would this reach" is a question
        // about a community that only its moderators should be able to ask.
        var scope = await _access.AuthorizeScopeAsync(
            member.Value, ownerKind, ownerId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<AutomationSimulationResponse>(scope.Error);
        }

        var validated = _validator.Validate(
            request.Trigger?.Key ?? string.Empty,
            request.Trigger?.Parameters,
            [.. (request.Conditions ?? []).Select(CreateAutomationCommandHandler.ToInput)],
            [.. (request.Actions ?? []).Select(CreateAutomationCommandHandler.ToInput)],
            ownerKind,
            scope.Value.Capabilities,

            // A dry run is validated as a draft, so a member can test a rule against a trigger this
            // deployment cannot yet observe and be told exactly that in the result.
            activating: false);

        if (validated.IsFailure)
        {
            return Result.Failure<AutomationSimulationResponse>(validated.Error);
        }

        var now = _clock.UtcNow;

        // Built in memory and never added to a repository. A simulation that touched the registry
        // would be a draft nobody asked to save.
        var draft = AutomationDefinition.Create(
            ownerKind,
            ownerId,
            scope.Value.Label,
            member.Value,
            string.IsNullOrWhiteSpace(request.Name) ? "Test" : request.Name,
            string.Empty,
            AutomationSource.Builder,
            null,
            request.Trigger!.Key,
            request.Trigger.Parameters ?? "{}",
            activateImmediately: false,
            now,
            null);

        draft.ReplaceConditions(
            (request.Conditions ?? []).Select(condition =>
                ((AutomationConditionId?)null, condition.Key, condition.Operator, condition.Value,
                    condition.SecondaryValue)));

        draft.ReplaceActions(
            (request.Actions ?? []).Select(action =>
                ((AutomationActionId?)null, action.Key, action.Parameters ?? "{}")));

        // ── The sample subject's real state — Phase 5 ───────────────────────────────────────────
        //
        // **This request has carried `SampleSubjectId` since the day it shipped, documented as "an
        // optional real subject to evaluate against, so a dry run borrows real state", and it
        // borrowed none.** The identifier was copied onto the context below and every other field
        // was left at its default, `Values` included — and `Values` is, in that record's own words,
        // "the entire surface a condition can be answered from". So every payload-carried condition
        // in every dry run this platform has ever served came back unevaluable.
        //
        // Confirmed against the deployed platform, 10 Sep 2026: an event whose country is MU, a rule
        // reading `country equals MU`, that event's identifier as the sample subject — SKIPPED, with
        // "This trigger does not carry the information the 'country' check needs". It carries it.
        // `EventCreatedAutomationConsumer` writes it, and so does the `event.startingSoon` horizon.
        //
        // Null when nothing can describe the subject, and the run then says so in those words rather
        // than blaming the trigger. See `AutomationConditionEvaluator`.
        var subject = request.SampleSubjectId is { } sampleId
            ? await _subjects.ReadAsync(draft.TriggerKey, sampleId, member.Value, cancellationToken)
            : null;

        var run = await _engine.RunOneAsync(
            draft,
            new AutomationTriggerContext
            {
                TriggerKey = draft.TriggerKey,
                TriggerEventId = $"simulation:{Guid.NewGuid():N}",
                ConcernedOwnerIds = [ownerId],
                SubjectType = subject?.SubjectKind,
                SubjectId = request.SampleSubjectId,
                SubjectTitle = subject?.Title,
                SubjectMoment = subject?.Moment,
                SubjectTimeZoneId = subject?.TimeZoneId,
                Summary = subject is null
                    ? "A test run. Nothing was sent, nothing was charged and nobody was notified."
                    : $"A test run against {subject.Title}. Nothing was sent, nothing was charged "
                      + "and nobody was notified.",
                OccurredAt = now,
                Values = subject?.Values ?? AutomationTriggerContext.NewValues()
            },
            AutomationRunReason.Simulation,
            isSimulated: true,
            cancellationToken);

        if (run is null)
        {
            return Result.Failure<AutomationSimulationResponse>(
                AutomationErrors.InvalidSchedule("This automation could not be tested."));
        }

        var estimate = await _scopes.For(ownerKind)
            .EstimateAudienceAsync(ownerKind, ownerId, cancellationToken);

        return Result.Success(new AutomationSimulationResponse
        {
            Run = AutomationMapper.ToResponse(run),
            WouldHaveDone =
            [
                .. draft.Actions.Select(action => new AutomationWouldHaveDoneResponse
                {
                    ActionKey = action.Key,
                    Label = action.Key,
                    Reason = "Nothing was done. This was a test."
                })
            ],
            Audience = AutomationMapper.ToResponse(
                estimate, ownerKind, _options.Value.AudienceSuppressionThreshold),
            Dependencies = Dependencies(draft)
        });
    }

    /// <summary>
    /// What this rule needs and whether the platform has it.
    /// <para>
    /// A <c>gap</c> is the platform's problem and never rendered as an error against the member; a
    /// <c>missing</c> is something they can act on. Only the second blocks activation.
    /// </para>
    /// </summary>
    private static List<AutomationDependencyResponse> Dependencies(AutomationDefinition draft)
    {
        var dependencies = new List<AutomationDependencyResponse>();
        var trigger = AutomationCatalog.FindTrigger(draft.TriggerKey);

        dependencies.Add(new AutomationDependencyResponse
        {
            Kind = "trigger",
            Label = draft.TriggerKey,
            State = trigger?.IsAvailable == true ? "met" : "gap",
            Detail = trigger?.UnavailableReason ?? "The platform can observe this.",
            GapId = trigger?.IsAvailable == true ? null : draft.TriggerKey,
            BlocksActivation = trigger?.IsAvailable != true
        });

        foreach (var action in draft.Actions)
        {
            var definition = AutomationCatalog.FindAction(action.Key);

            dependencies.Add(new AutomationDependencyResponse
            {
                Kind = "action",
                Label = action.Key,
                State = definition?.IsAvailable == true ? "met" : "gap",
                Detail = definition?.UnavailableReason ?? "The platform can do this.",
                GapId = definition?.IsAvailable == true ? null : action.Key,
                BlocksActivation = definition?.IsAvailable != true
            });
        }

        foreach (var condition in draft.Conditions)
        {
            var definition = AutomationCatalog.FindCondition(condition.Key);

            dependencies.Add(new AutomationDependencyResponse
            {
                Kind = "condition",
                Label = condition.Key,
                State = definition?.IsServerEvaluable == true ? "met" : "gap",
                Detail = definition?.UnavailableReason ?? "The platform can check this.",
                GapId = definition?.IsServerEvaluable == true ? null : condition.Key,

                // A condition the platform cannot answer does not block activation — it makes every
                // run skip, which is the honest and visible outcome, and a member may still want the
                // rule saved for the day the source ships.
                BlocksActivation = false
            });
        }

        return dependencies;
    }
}

/// <summary>How many people an automation would reach. A count and prose, never a member list.</summary>
public sealed record GetAudienceQuery : IQuery<AutomationAudienceResponse>
{
    public required AudienceRequest Request { get; init; }
}

public sealed class GetAudienceQueryHandler : IQueryHandler<GetAudienceQuery, AutomationAudienceResponse>
{
    private readonly AutomationAccessService _access;
    private readonly AutomationScopeAuthorityResolver _scopes;
    private readonly ISubjectRateLimiter _rateLimiter;
    private readonly IOptions<AutomationOptions> _options;

    public GetAudienceQueryHandler(
        AutomationAccessService access,
        AutomationScopeAuthorityResolver scopes,
        ISubjectRateLimiter rateLimiter,
        IOptions<AutomationOptions> options)
    {
        _access = access;
        _scopes = scopes;
        _rateLimiter = rateLimiter;
        _options = options;
    }

    public async Task<Result<AutomationAudienceResponse>> Handle(
        GetAudienceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationAudienceResponse>(member.Error);
        }

        // Rate limited, because repeated queries with varying scopes are how an enumeration attack
        // against a segment is built.
        var lease = await _rateLimiter.AcquireAsync(
            $"automation.audience:{member.Value.Value:N}", 120, TimeSpan.FromHours(1), cancellationToken);

        if (!lease.IsAllowed)
        {
            return Result.Failure<AutomationAudienceResponse>(
                AutomationErrors.RateLimited("Too many audience checks. Try again in a little while."));
        }

        var ownerKind = ListAutomationsQueryHandler.ParseOwnerKind(query.Request.Scope?.OwnerKind)
                        ?? AutomationOwnerKind.User;
        var ownerId = query.Request.Scope?.OwnerId ?? member.Value.Value;

        var scope = await _access.AuthorizeScopeAsync(
            member.Value, ownerKind, ownerId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<AutomationAudienceResponse>(scope.Error);
        }

        var estimate = await _scopes.For(ownerKind)
            .EstimateAudienceAsync(ownerKind, ownerId, cancellationToken);

        return Result.Success(
            AutomationMapper.ToResponse(
                estimate, ownerKind, _options.Value.AudienceSuppressionThreshold));
    }
}

/// <summary>The next occurrence of a scheduled automation, and anything worth warning about.</summary>
public sealed record GetScheduleQuery : IQuery<AutomationScheduleStateResponse>
{
    public required Guid AutomationId { get; init; }
}

public sealed class GetScheduleQueryHandler
    : IQueryHandler<GetScheduleQuery, AutomationScheduleStateResponse>
{
    private readonly IAutomationRepository _automations;
    private readonly IAutomationOccurrenceRepository _occurrences;
    private readonly AutomationAccessService _access;
    private readonly AutomationScheduleService _schedule;

    public GetScheduleQueryHandler(
        IAutomationRepository automations,
        IAutomationOccurrenceRepository occurrences,
        AutomationAccessService access,
        AutomationScheduleService schedule)
    {
        _automations = automations;
        _occurrences = occurrences;
        _access = access;
        _schedule = schedule;
    }

    public async Task<Result<AutomationScheduleStateResponse>> Handle(
        GetScheduleQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationScheduleStateResponse>(member.Error);
        }

        var automation = await _automations.GetAsync(
            AutomationId.From(query.AutomationId), cancellationToken);

        if (automation is null)
        {
            return Result.Failure<AutomationScheduleStateResponse>(AutomationErrors.NotFound());
        }

        var authorized = await _access.AuthorizeAutomationAsync(
            member.Value, automation.OwnerKind, automation.OwnerId, automation.CreatedByUserId,
            requireManage: false, cancellationToken);

        if (authorized.IsFailure)
        {
            return Result.Failure<AutomationScheduleStateResponse>(authorized.Error);
        }

        var pending = await _occurrences.ListPendingForAutomationAsync(automation.Id, cancellationToken);
        var plan = _schedule.Preview(automation);

        return Result.Success(AutomationMapper.ToResponse(plan, pending.Count));
    }
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Trigger context — Phase 5.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What a trigger would carry about a subject the member named.</summary>
public sealed record GetTriggerContextQuery : IQuery<AutomationTriggerContextResponse>
{
    public string TriggerKey { get; init; } = string.Empty;

    public Guid? SubjectId { get; init; }
}

public sealed class GetTriggerContextQueryHandler
    : IQueryHandler<GetTriggerContextQuery, AutomationTriggerContextResponse>
{
    private readonly AutomationAccessService _access;
    private readonly AutomationSubjectContextService _subjects;

    public GetTriggerContextQueryHandler(
        AutomationAccessService access,
        AutomationSubjectContextService subjects)
    {
        _access = access;
        _subjects = subjects;
    }

    public async Task<Result<AutomationTriggerContextResponse>> Handle(
        GetTriggerContextQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationTriggerContextResponse>(member.Error);
        }

        var trigger = AutomationCatalog.FindTrigger(query.TriggerKey);

        if (trigger is null)
        {
            return Result.Failure<AutomationTriggerContextResponse>(
                AutomationErrors.UnknownTrigger(query.TriggerKey));
        }

        var kind = AutomationSubjectContextService.SubjectKindFor(query.TriggerKey);

        var subject = query.SubjectId is { } subjectId
            ? await _subjects.ReadAsync(query.TriggerKey, subjectId, member.Value, cancellationToken)
            : null;

        if (subject is null)
        {
            return Result.Success(new AutomationTriggerContextResponse
            {
                TriggerKey = query.TriggerKey,
                SubjectKind = kind,
                SubjectId = query.SubjectId,
                IsObservable = false,
                Summary = Unobservable(query, trigger, kind),
                Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                Answers = []
            });
        }

        // A run record never quotes a sensitive condition's value, and this must not become the way
        // to read one. The key stays visible so a member sees the check will be made.
        var quotable = subject.Values
            .Where(pair => AutomationCatalog.FindCondition(pair.Key)?.IsSensitive != true)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return Result.Success(new AutomationTriggerContextResponse
        {
            TriggerKey = query.TriggerKey,
            SubjectKind = subject.SubjectKind,
            SubjectId = subject.SubjectId,
            Title = subject.Title,
            Moment = subject.Moment,
            TimeZoneId = subject.TimeZoneId,
            IsObservable = true,
            Summary = Describe(subject),
            Values = quotable,
            Answers = [.. subject.Values.Keys.OrderBy(key => key, StringComparer.Ordinal)]
        });
    }

    /// <summary>
    /// Why nothing could be described, in words a member can act on.
    /// <para>
    /// Three different absences, and a member can do something about two of them. Collapsing them
    /// into one sentence is what made the dry run unhelpful for as long as it was.
    /// </para>
    /// </summary>
    private static string Unobservable(
        GetTriggerContextQuery query, TriggerCatalogEntry trigger, string? kind)
    {
        if (!trigger.IsAvailable)
        {
            return trigger.UnavailableReason
                ?? "Diaspora cannot watch for this yet.";
        }

        if (kind is null)
        {
            return "This trigger is not about one particular thing, so there is nothing to preview. "
                + "It will still run.";
        }

        return query.SubjectId is null
            ? $"Choose {Article(kind)} {kind} to see what this would be checked against."
            : $"Diaspora could not read that {kind}. It may have been removed, or it may not be one "
                + "you can see.";
    }

    /// <summary>
    /// The subject in one line, in the subject's own zone.
    /// <para>
    /// <b>The moment is stored as an instant and must be converted before it is labelled.</b>
    /// Printing the instant beside the subject's zone name — "17:00 (Africa/Lagos)" for an event
    /// that starts at 18:00 in Lagos — states something false while looking like §42 being honoured.
    /// A zone this deployment does not recognise falls back to the instant, marked UTC, rather than
    /// throwing inside a builder screen.
    /// </para>
    /// </summary>
    private static string Describe(AutomationSubjectSnapshot subject)
    {
        if (subject.Moment is not { } moment)
        {
            return subject.Title;
        }

        if (string.IsNullOrWhiteSpace(subject.TimeZoneId))
        {
            return $"{subject.Title} — {moment:dd MMM yyyy, HH:mm} (UTC)";
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(subject.TimeZoneId);
            var local = TimeZoneInfo.ConvertTime(moment, zone);

            return $"{subject.Title} — {local:dd MMM yyyy, HH:mm} ({subject.TimeZoneId})";
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return $"{subject.Title} — {moment:dd MMM yyyy, HH:mm} (UTC)";
        }
    }

    /// <summary>"an event", "a job". A member reads this sentence.</summary>
    private static string Article(string noun) =>
        noun.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(noun[0]))
            ? "an"
            : "a";
}
