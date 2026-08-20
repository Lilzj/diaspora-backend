using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Modules.Automation.Extensibility.Scopes;

namespace Diaspora.Modules.Automation.Application.Mapping;

/// <summary>
/// Turns the module's aggregates into the shapes the client already holds.
/// <para>
/// <b>Static, and every method is.</b> A mapper with no state that took a constructor dependency would
/// be a scoped allocation per request for a pure function, and CA1822 reports every stateless instance
/// method as one in Release — a cascade this platform has already paid for once.
/// </para>
/// <para>
/// <b>Enum values are written as the client's strings, not as this module's names.</b>
/// <c>PARTIALLY_COMPLETED</c>, <c>succeeded</c>, <c>needs_attention</c> — the client switches on them
/// and a case difference maps every one to an unknown state with nothing failing anywhere.
/// </para>
/// </summary>
public static class AutomationMapper
{
    public static AutomationDefinitionResponse ToResponse(AutomationDefinition automation)
    {
        ArgumentNullException.ThrowIfNull(automation);

        var (support, gaps) = DeriveSupport(automation);

        return new AutomationDefinitionResponse
        {
            Id = automation.Id.Value.ToString("D"),
            Name = automation.Name,
            Description = automation.Description,
            Status = StatusName(automation.Status),
            Scope = new AutomationScopeResponse
            {
                OwnerKind = OwnerKindName(automation.OwnerKind),
                OwnerId = automation.OwnerId.ToString("D"),
                OwnerLabel = automation.OwnerLabel,
                CommunityIds = [.. automation.CommunityIdList().Select(id => id.ToString("D"))],
                IsCrossCommunity = automation.IsCrossCommunity,
                CountryCodes = automation.CountryCodeList(),
                NeighbourhoodIds = [.. automation.NeighbourhoodIdList().Select(id => id.ToString("D"))]
            },
            Source = automation.Source.ToString().ToLowerInvariant(),
            TemplateId = automation.TemplateId,
            Trigger = new AutomationTriggerResponse
            {
                Key = automation.TriggerKey,
                Parameters = ReadParameters(automation.TriggerParameters)
            },
            Conditions =
            [
                .. automation.Conditions
                    .OrderBy(condition => condition.Position)
                    .Select(condition => new AutomationConditionResponse
                    {
                        Id = condition.Id.Value.ToString("D"),
                        Key = condition.Key,
                        Operator = condition.Operator,
                        Value = condition.Value,
                        SecondaryValue = condition.SecondaryValue
                    })
            ],
            Actions =
            [
                .. automation.Actions
                    .OrderBy(action => action.Position)
                    .Select(action => new AutomationActionResponse
                    {
                        Id = action.Id.Value.ToString("D"),
                        Key = action.Key,
                        Parameters = ReadParameters(action.Parameters)
                    })
            ],
            Schedule = automation.HasSchedule ? ToScheduleResponse(automation) : null,
            Support = support,
            GapIds = gaps,
            CreatedAt = Iso(automation.CreatedAt),
            UpdatedAt = Iso(automation.UpdatedAt),
            LastRunAt = automation.LastRunAt is { } last ? Iso(last) : null,
            NextRunAt = automation.NextRunAt is { } next ? Iso(next) : null,
            ExecutionCount = automation.ExecutionCount,
            LastRunStatus = automation.LastRunStatus is { } status ? RunStatusName(status) : null,
            AttentionReason = automation.AttentionReason,
            Audit =
            [
                .. automation.Audit
                    .OrderByDescending(entry => entry.At)
                    .Select(entry => new AutomationAuditEntryResponse
                    {
                        Action = entry.Action.ToString().ToLowerInvariant(),
                        At = Iso(entry.At),
                        ActorLabel = entry.ActorLabel,
                        Provenance = "server"
                    })
            ],

            // Always false. Nothing in this module can produce a simulated definition — a simulation
            // returns a run, never a stored rule — and §57 requires the marker to be unmissable rather
            // than inferred.
            IsSimulated = false
        };
    }

    public static AutomationRunResponse ToResponse(AutomationRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new AutomationRunResponse
        {
            Id = run.Id.Value.ToString("D"),
            AutomationId = run.AutomationId.Value.ToString("D"),
            AutomationName = run.AutomationName,
            Status = RunStatusName(run.Status),
            StartedAt = Iso(run.StartedAt),
            CompletedAt = run.CompletedAt is { } completed ? Iso(completed) : null,
            DurationMs = run.DurationMs,
            TriggerSummary = run.TriggerSummary,
            TriggeredAt = Iso(run.TriggeredAt),
            ConditionOutcomes =
            [
                .. run.ConditionOutcomes.Select(outcome => new AutomationConditionOutcomeResponse
                {
                    ConditionId = outcome.ConditionId.Value.ToString("D"),
                    Label = outcome.Label,
                    Matched = outcome.Matched,
                    EvaluatedBy = outcome.EvaluatedByServer ? "server" : "client",
                    Explanation = outcome.Explanation
                })
            ],
            ActionOutcomes =
            [
                .. run.ActionOutcomes.Select(outcome => new AutomationActionOutcomeResponse
                {
                    ActionId = outcome.ActionId.Value.ToString("D"),
                    ActionKey = outcome.ActionKey,
                    Label = outcome.Label,
                    Status = outcome.Status.ToString().ToLowerInvariant(),
                    SuppressionReason = outcome.SuppressionReason,
                    FailureKind = outcome.FailureKind == AutomationFailureKind.None
                        ? null
                        : Camel(outcome.FailureKind.ToString()),
                    Message = outcome.Message,
                    ResultRef = outcome.ResultKind is null || outcome.ResultId is null
                        ? null
                        : new AutomationResultRefResponse
                        {
                            Kind = outcome.ResultKind,
                            Id = outcome.ResultId,
                            DeepLink = outcome.DeepLink
                        },
                    CompletedAt = outcome.CompletedAt is { } at ? Iso(at) : null
                })
            ],
            FailureKind = run.FailureKind == AutomationFailureKind.None
                ? null
                : Camel(run.FailureKind.ToString()),
            FailureMessage = run.FailureMessage,
            RecoveryHint = run.RecoveryHint,
            CorrelationId = run.CorrelationId,
            IsSimulated = run.IsSimulated,
            WasDeduplicated = run.WasDeduplicated
        };
    }

    public static AutomationAccessResponse ToResponse(AutomationAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        return new AutomationAccessResponse
        {
            CanViewAutomations = access.CanViewAutomations,
            CanCreatePersonal = access.CanCreatePersonal,
            CanCreateForCommunity = access.CanCreateForCommunity,
            CanCreateForOrganization = access.CanCreateForOrganization,
            CanCreateForCreator = access.CanCreateForCreator,
            CanCreateForBusiness = access.CanCreateForBusiness,
            CanCreatePlatformWide = access.CanCreatePlatformWide,
            CanNotifyOtherMembers = access.CanNotifyOtherMembers,
            CanUseFinancialTriggers = access.CanUseFinancialTriggers,
            CanUseLocationTriggers = access.CanUseLocationTriggers,
            CanViewOthersRuns = access.CanViewOthersRuns,
            CanManageOthersAutomations = access.CanManageOthersAutomations,
            MaxActiveAutomations = access.MaxActiveAutomations,
            ActiveAutomations = access.ActiveAutomations,
            Provenance = access.Provenance,
            GrantedCapabilities = access.GrantedCapabilities
        };
    }

    /// <summary>
    /// The audience, banded where the count is small.
    /// <para>
    /// "3 members match" in a small community identifies people. Below the threshold the response
    /// carries a null count and prose that says so, matching the suppression rule B19 applies to small
    /// analytics cohorts.
    /// </para>
    /// </summary>
    public static AutomationAudienceResponse ToResponse(
        AutomationAudienceEstimate estimate,
        AutomationOwnerKind ownerKind,
        int suppressionThreshold)
    {
        var suppressed = estimate.EstimatedRecipients is { } count &&
                         count is > 0 &&
                         count < suppressionThreshold;

        return new AutomationAudienceResponse
        {
            Kind = ownerKind switch
            {
                AutomationOwnerKind.Community => "community",
                AutomationOwnerKind.Organization => "organization",
                AutomationOwnerKind.Creator => "followers",
                _ => "self"
            },
            EstimatedRecipients = suppressed ? null : estimate.EstimatedRecipients,
            Provenance = estimate.EstimatedRecipients is null ? "unavailable" : "server",
            Description = suppressed
                ? $"Fewer than {suppressionThreshold} people. {estimate.Description}"
                : estimate.Description,
            ServerSideFilters =
            [
                "Notification preferences",
                "Quiet hours, in each recipient's own timezone",
                "Members who have blocked the sender",
                "Members who have left the scope"
            ]
        };
    }

    public static AutomationScheduleStateResponse ToResponse(
        AutomationOccurrencePlan plan, int missedCount) =>
        new()
        {
            NextRunAt = plan.NextRunAt is { } next ? Iso(next) : null,
            NextRunLocal = plan.LocalWallClock,
            TimeZone = plan.TimeZoneId,
            Upcoming = plan.NextRunAt is { } upcoming ? [Iso(upcoming)] : [],
            MissedCount = missedCount,
            IsSchedulable = plan.NextRunAt is not null,
            Reason = plan.Reason,
            Warnings = plan.Warnings
        };

    public static SavedSearchResponse ToResponse(SavedSearch search)
    {
        ArgumentNullException.ThrowIfNull(search);

        return new SavedSearchResponse
        {
            Id = search.Id.Value.ToString("D"),
            Subject = search.Subject.ToString().ToLowerInvariant(),
            Name = search.Name,
            Criteria = ReadParameters(search.Criteria),
            QuerySummary = search.QuerySummary,
            AlertsEnabled = search.AlertsEnabled,
            AlertFrequency = search.AlertFrequency.ToString().ToLowerInvariant(),
            WatchFor = search.WatchForList(),
            CreatedAt = Iso(search.CreatedAt),
            LastMatchedAt = search.LastMatchedAt is { } matched ? Iso(matched) : null,
            NewMatchCount = search.NewMatchCount,
            AutomationId = search.AutomationId?.Value.ToString("D"),
            IsSimulated = false
        };
    }

    public static ReminderResponse ToResponse(AutomationReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        return new ReminderResponse
        {
            ReminderId = reminder.Id.Value.ToString("D"),
            SubjectKind = reminder.SubjectKind,
            SubjectId = reminder.SubjectId.ToString("D"),
            OffsetMinutes = reminder.OffsetMinutes,
            Anchor = reminder.Anchor,
            State = reminder.State.ToString().ToLowerInvariant(),
            ScheduledFor = reminder.ScheduledFor is { } at ? Iso(at) : null,
            TimeZoneId = reminder.TimeZoneId,
            IsSimulated = false
        };
    }

    /// <summary>The catalogue, so the builder offers only what the deployment can do.</summary>
    public static AutomationCatalogResponse Catalog() => new()
    {
        Triggers =
        [
            .. AutomationCatalog.Triggers.Values
                .OrderBy(trigger => trigger.Key, StringComparer.Ordinal)
                .Select(trigger => new AutomationCatalogEntryResponse
                {
                    Key = trigger.Key,
                    Domain = trigger.Domain,
                    IsAvailable = trigger.IsAvailable,
                    UnavailableReason = trigger.UnavailableReason,
                    RequiredCapability = trigger.RequiredCapability,
                    AllowedOwners = [.. trigger.AllowedOwners.Select(OwnerKindName)]
                })
        ],
        Conditions =
        [
            .. AutomationCatalog.Conditions.Values
                .OrderBy(condition => condition.Key, StringComparer.Ordinal)
                .Select(condition => new AutomationCatalogEntryResponse
                {
                    Key = condition.Key,
                    Domain = condition.Domain,
                    IsAvailable = condition.IsServerEvaluable,
                    UnavailableReason = condition.UnavailableReason,
                    RequiredCapability = null,
                    AllowedOwners = [],
                    RequiresServerEvaluation = condition.RequiresServerEvaluation,
                    IsSensitive = condition.IsSensitive
                })
        ],
        Actions =
        [
            .. AutomationCatalog.Actions.Values
                .OrderBy(action => action.Key, StringComparer.Ordinal)
                .Select(action => new AutomationCatalogEntryResponse
                {
                    Key = action.Key,
                    Domain = action.Domain,
                    IsAvailable = action.IsAvailable,
                    UnavailableReason = action.UnavailableReason,
                    RequiredCapability = action.RequiredCapability,
                    AllowedOwners = [.. action.AllowedOwners.Select(OwnerKindName)],
                    ReachesOtherPeople = action.ReachesOtherPeople
                })
        ]
    };

    /// <summary>
    /// What the deployment can do with this rule, derived from its parts.
    /// <para>
    /// Never declared. An automation is only as executable as its least-supported component, so a
    /// <c>CONNECTED</c> trigger feeding an unavailable action reports <c>PARTIAL</c> and an
    /// unobservable trigger reports <c>BACKEND_GAP</c> whatever its actions say.
    /// </para>
    /// </summary>
    private static (string Support, IReadOnlyList<string> Gaps) DeriveSupport(AutomationDefinition automation)
    {
        var gaps = new List<string>();
        var trigger = AutomationCatalog.FindTrigger(automation.TriggerKey);

        if (trigger is null || !trigger.IsAvailable)
        {
            gaps.Add(automation.TriggerKey);
            return ("BACKEND_GAP", gaps);
        }

        foreach (var action in automation.Actions)
        {
            var definition = AutomationCatalog.FindAction(action.Key);

            if (definition is null || !definition.IsAvailable)
            {
                gaps.Add(action.Key);
            }
        }

        foreach (var condition in automation.Conditions)
        {
            var definition = AutomationCatalog.FindCondition(condition.Key);

            if (definition is null || !definition.IsServerEvaluable)
            {
                gaps.Add(condition.Key);
            }
        }

        return gaps.Count == 0 ? ("CONNECTED", gaps) : ("PARTIAL", gaps);
    }

    private static AutomationScheduleResponse ToScheduleResponse(AutomationDefinition automation) => new()
    {
        Frequency = Camel(automation.Frequency.ToString()),
        Timezone = new AutomationTimezoneResponse
        {
            Mode = Camel(automation.TimezoneMode.ToString()),
            IanaId = automation.TimeZoneId,
            Label = automation.TimeZoneId ?? "UTC",
            IsExact = automation.TimeZoneId is not null
        },
        Hour = automation.Hour,
        Minute = automation.Minute,
        DaysOfWeek = automation.DayOfWeekList(),
        DayOfMonth = automation.DayOfMonth,
        StartsAt = automation.StartsAt is { } starts ? Iso(starts) : null,
        EndsAt = automation.EndsAt is { } ends ? Iso(ends) : null,
        MaxExecutions = automation.MaxExecutions,
        EventOffset = automation.EventOffsetMinutes is { } minutes
            ? new AutomationEventOffsetResponse
            {
                MinutesBefore = minutes,
                Anchor = automation.EventOffsetAnchor ?? "start"
            }
            : null,
        CustomDescription = automation.CustomDescription
    };

    /// <summary>
    /// Reads a parameter bag back for the client.
    /// <para>
    /// A malformed bag reads as empty rather than throwing. A rule stored months ago must not be able
    /// to make a list screen 500.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ReadParameters(string json)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return values;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => string.Join(
                        ',', property.Value.EnumerateArray().Select(element => element.ToString())),
                    _ => null
                };
            }
        }
        catch (JsonException)
        {
            return values;
        }

        return values;
    }

    /// <summary>
    /// B24: the diagnostics aggregate, in the client's own vocabulary.
    /// <para>
    /// The success rate is computed here rather than by the caller, and over <i>settled</i> runs
    /// rather than over all of them. A rate whose denominator included runs still awaiting a
    /// notification receipt would fall every time the platform got busy and rise every time it went
    /// quiet, which is the opposite of what it is read for.
    /// </para>
    /// <para>
    /// <b>Suppressed counts as success, and partially-completed does not.</b> A member's own
    /// preferences deciding nothing should be sent is the feature working — the run aggregate already
    /// takes that position where it derives a terminal status, and a success rate that disagreed with
    /// the history beside it would be the more visible of the two wrong answers.
    /// </para>
    /// </summary>
    public static AutomationDiagnosticsResponse ToResponse(
        AutomationRunSummary summary, DateTimeOffset from, DateTimeOffset to, int days)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var settled = summary.ByStatus
            .Where(entry => entry.Key != AutomationRunStatus.Running)
            .Sum(entry => entry.Value);

        var succeeded = summary.ByStatus
            .Where(entry => entry.Key is AutomationRunStatus.Completed or AutomationRunStatus.Skipped)
            .Sum(entry => entry.Value);

        return new AutomationDiagnosticsResponse
        {
            From = Iso(from),
            To = Iso(to),
            Days = days,
            Total = summary.Total,
            ByStatus = summary.ByStatus.ToDictionary(
                entry => RunStatusName(entry.Key), entry => entry.Value, StringComparer.Ordinal),
            ByFailureKind = summary.ByFailureKind.ToDictionary(
                entry => Camel(entry.Key.ToString()), entry => entry.Value, StringComparer.Ordinal),
            SuccessRate = settled == 0 ? null : Math.Round((double)succeeded / settled, 4),
            AverageDurationMs = summary.AverageDurationMs is { } average
                ? Math.Round(average, 1)
                : null,
            SlowestDurationMs = summary.SlowestDurationMs,
            Deduplicated = summary.Deduplicated,
            DeadLettered = summary.DeadLettered,
            TopFailingActions =
            [
                .. summary.TopFailingActions.Select(entry => new AutomationActionFailureResponse
                {
                    ActionKey = entry.ActionKey,
                    Failures = entry.Failures
                })
            ]
        };
    }

    /// <summary>The client's status strings, which are upper snake case.</summary>
    public static string StatusName(AutomationStatus status) => status switch
    {
        AutomationStatus.NeedsAttention => "NEEDS_ATTENTION",
        _ => status.ToString().ToUpperInvariant()
    };

    public static string RunStatusName(AutomationRunStatus status) => status switch
    {
        AutomationRunStatus.PartiallyCompleted => "PARTIALLY_COMPLETED",
        AutomationRunStatus.TimedOut => "TIMED_OUT",
        _ => status.ToString().ToUpperInvariant()
    };

    public static string OwnerKindName(AutomationOwnerKind kind) => kind.ToString().ToLowerInvariant();

    private static string Camel(string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Iso(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
