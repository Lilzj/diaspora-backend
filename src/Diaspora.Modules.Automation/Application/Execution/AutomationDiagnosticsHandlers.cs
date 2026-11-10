using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Automation.Application.Execution;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B24 · §21 · §23 · §24 · §25 — the aggregate over execution history.
//
// **This is a query, not a store.** §2 forbids competing analytics storage without a demonstrated
// requirement, and there is none: `automation_runs` already holds every field §7 asks for, already
// has a stated 90-day retention, and is already owner-scoped. What was missing was the aggregate —
// "how is this working?" — which the client could previously only approximate by counting the page
// of runs it happened to have fetched.
//
// **It is not B19.** §2's boundary is that B19 owns analysis across the platform and over time, and
// B24 owns whether the automation system is behaving. The distinction shows in what this returns:
// counts and durations over a bounded window, for one owner, with no trend, no comparison period, no
// segmentation and no export. A question like "which automation triggers grew fastest this quarter"
// is B19's, is answered from the analytics event this wave registers, and is deliberately not
// answerable here.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How one owner's automations have been behaving.</summary>
[Authorize(Policy = AutomationPolicies.Read)]
public sealed record GetAutomationDiagnosticsQuery : IQuery<AutomationDiagnosticsResponse>
{
    /// <summary>Narrow to one automation the caller may read. Omitted means all of their own.</summary>
    public Guid? AutomationId { get; init; }

    /// <summary>Narrow to one trigger. Validated against the catalogue.</summary>
    public string? TriggerKey { get; init; }

    /// <summary>How far back. Clamped to the run store's own retention — see the handler.</summary>
    public int? Days { get; init; }
}

public sealed class GetAutomationDiagnosticsQueryHandler
    : IQueryHandler<GetAutomationDiagnosticsQuery, AutomationDiagnosticsResponse>
{
    private const int DefaultDays = 7;
    private const int TopFailingActions = 5;

    private readonly IAutomationRunRepository _runs;
    private readonly IAutomationOccurrenceRepository _occurrences;
    private readonly AutomationDiagnosticsService _diagnostics;
    private readonly AutomationAccessService _access;
    private readonly IClock _clock;

    public GetAutomationDiagnosticsQueryHandler(
        IAutomationRunRepository runs,
        IAutomationOccurrenceRepository occurrences,
        AutomationDiagnosticsService diagnostics,
        AutomationAccessService access,
        IClock clock)
    {
        _runs = runs;
        _occurrences = occurrences;
        _diagnostics = diagnostics;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<AutomationDiagnosticsResponse>> Handle(
        GetAutomationDiagnosticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<AutomationDiagnosticsResponse>(member.Error);
        }

        var scope = await _diagnostics.ResolveAsync(
            member.Value, query.AutomationId, cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<AutomationDiagnosticsResponse>(scope.Error);
        }

        // Clamped against the retention horizon rather than left open. A window wider than the store
        // keeps produces a figure that silently means "since retention", and a member comparing a
        // 90-day figure with a 365-day one would be comparing the same number twice.
        var days = Math.Clamp(query.Days ?? DefaultDays, 1, AutomationRunRetentionDays);
        var to = _clock.UtcNow;
        var from = to.AddDays(-days);

        var trigger = ListRunsQueryHandler.NormaliseTriggerKey(query.TriggerKey);

        var summary = await _runs.SummariseAsync(
            scope.Value.OwnerId,
            scope.Value.AutomationId,
            trigger,
            from,
            to,
            TopFailingActions,
            cancellationToken);

        // Dead letters come from the occurrence store because a moment that dead-lettered never
        // produced a run — which is precisely why it needs reporting beside the runs that did.
        var deadLettered = await _occurrences.CountDeadLetteredForOwnerAsync(
            scope.Value.OwnerId, from, to, cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(
            summary with { DeadLettered = deadLettered }, from, to, days));
    }

    /// <summary>
    /// The retention horizon, as the ceiling on a window.
    /// <para>
    /// Deliberately a constant rather than a read of <c>AutomationOptions.RunRetentionDays</c>. A
    /// deployment that shortened its retention would otherwise silently shorten the widest window a
    /// member can ask for, and a deployment that lengthened it would widen this route's cost without
    /// anybody choosing to. The two settings answer different questions and are allowed to differ;
    /// what they must not do is drift apart unnoticed, which is what the comment on
    /// <c>RunRetentionDays</c> now says.
    /// </para>
    /// </summary>
    private const int AutomationRunRetentionDays = 90;
}
