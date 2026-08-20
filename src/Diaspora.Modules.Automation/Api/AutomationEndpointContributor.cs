using Diaspora.Modules.Automation.Application.Execution;
using Diaspora.Modules.Automation.Application.Registry;
using Diaspora.Modules.Automation.Application.Reminders;
using Diaspora.Modules.Automation.Application.SavedSearches;
using Diaspora.Modules.Automation.Application.Templates;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Automation.Api;

/// <summary>
/// The module's HTTP surface.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are byte-for-byte identical to every
/// other module's. Nothing here is known to the host.
/// </para>
/// <para>
/// <b>Route shapes come from the frontend, not from this module.</b> The client's
/// <c>automation-gap-api.ts</c> declares every one of these against <c>/automation-gap/v0</c> with the
/// note that "when a backend phase ships automation, the hook swaps which arm it calls and the request
/// shapes already match". They match. Only the prefix changes, to <c>/api/v1/automations</c>.
/// </para>
/// <para>
/// <b>Two routes are additive, and both are controls rather than features.</b> <c>GET /catalog</c>
/// exists because "the platform cannot observe this yet" has to be something a client can read rather
/// than infer from a refusal, and <c>GET /{id}/schedule</c> is the register's own contract, which the
/// development adapter did not need because it invented no times.
/// </para>
/// <para>
/// <b>There is no dispatch route and no execute-one-action route.</b> A member-callable dispatch
/// endpoint is a spam cannon with an audit trail, and a caller who could dispatch one action would be
/// a caller who could notify somebody without a rule, without an audience check and without a run
/// record.
/// </para>
/// </summary>
public sealed class AutomationEndpointContributor : IEndpointContributor
{
    public string Module => AutomationModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(AutomationModuleMetadata.RoutePrefix)
            .WithTags("Automation");

        MapRegistry(group);
        MapExecution(group);
        MapCapabilities(group);
        MapSchedule(group);
        MapReminders(group);
        MapSavedSearches(group);
        MapTemplates(group);
    }

    // ── The registry — GAP-001 · GAP-027 · GAP-029 ──────────────────────────────────────────────

    private static void MapRegistry(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
                string? ownerKind,
                Guid? ownerId,
                string? status,
                string? domain,
                Guid? communityId,
                string? search,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListAutomationsQuery
                    {
                        OwnerKind = ownerKind,
                        OwnerId = ownerId,
                        Status = status,
                        Domain = domain,
                        CommunityId = communityId,
                        Search = search,
                        Cursor = cursor,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("ListAutomations")
            .WithSummary(
                "This member's automations. Naming an owner scope you have no role on answers 403, "
                + "never an empty list.");

        group.MapGet("/{automationId:guid}", async (
                Guid automationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAutomationQuery { AutomationId = automationId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomation")
            .WithSummary("One automation, its rule and its audit trail.");

        group.MapPost("/", async (
                CreateAutomationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateAutomationCommand { Request = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Write)
            .WithName("CreateAutomation")
            .WithSummary(
                "Creates an automation. The idempotency key is mandatory: without it a retried "
                + "request gives the member two identical automations and every notification twice.");

        group.MapPatch("/{automationId:guid}", async (
                Guid automationId,
                UpdateAutomationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UpdateAutomationCommand { AutomationId = automationId, Request = request },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Write)
            .WithName("UpdateAutomation")
            .WithSummary(
                "Changes an automation. Answers 409 when expectedUpdatedAt does not match, so an edit "
                + "on a phone cannot silently overwrite one made on a tablet.");

        group.MapPatch("/{automationId:guid}/status", async (
                Guid automationId,
                ChangeAutomationStatusRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ChangeAutomationStatusCommand
                    {
                        AutomationId = automationId,
                        Status = request.Status
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Write)
            .WithName("ChangeAutomationStatus")
            .WithSummary(
                "Activates, pauses, disables or drafts an automation, and reports how many pending "
                + "runs were cancelled.");

        group.MapDelete("/{automationId:guid}", async (
                Guid automationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeleteAutomationCommand { AutomationId = automationId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Write)
            .WithName("DeleteAutomation")
            .WithSummary(
                "Deletes an automation. Idempotent: a second call answers 200 with the same body, "
                + "never 404.");

        group.MapGet("/{automationId:guid}/audit", async (
                Guid automationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAutomationAuditQuery { AutomationId = automationId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationAudit")
            .WithSummary("Who created, changed, activated, paused or deleted it, and when.");
    }

    // ── Execution — GAP-002 · GAP-004 · GAP-006 ─────────────────────────────────────────────────

    private static void MapExecution(RouteGroupBuilder group)
    {
        group.MapGet("/runs", async (
                Guid? automationId,
                string? status,
                string? failureKind,
                string? triggerKey,
                DateTimeOffset? from,
                DateTimeOffset? to,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListRunsQuery
                    {
                        AutomationId = automationId,
                        Status = status,
                        FailureKind = failureKind,
                        TriggerKey = triggerKey,
                        From = from,
                        To = to,
                        Cursor = cursor,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("ListAutomationRuns")
            .WithSummary(
                "Execution history. The route takes no owner parameter: the subject is the token's, "
                + "so a member cannot read another member's runs by supplying one.");

        // B24 · §21 · §23. The aggregate over the same history, under the same scope rule.
        group.MapGet("/diagnostics", async (
                Guid? automationId,
                string? triggerKey,
                int? days,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAutomationDiagnosticsQuery
                    {
                        AutomationId = automationId,
                        TriggerKey = triggerKey,
                        Days = days
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationDiagnostics")
            .WithSummary(
                "How the caller's automations have been behaving: executions, success rate, failure "
                + "breakdown, durations, dead letters and the actions that fail most. Owner-scoped "
                + "exactly as the history is — no owner parameter, and an automation the caller may "
                + "not read answers 404 rather than a denial.");

        group.MapGet("/{automationId:guid}/runs/{runId:guid}", async (
                Guid automationId,
                Guid runId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetRunQuery { AutomationId = automationId, RunId = runId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationRun")
            .WithSummary("One run, with what each condition decided and what each action produced.");

        group.MapPost("/{automationId:guid}/run", async (
                Guid automationId,
                RunAutomationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RunAutomationCommand
                    {
                        AutomationId = automationId,
                        IdempotencyKey = request?.IdempotencyKey
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Execute)
            .WithName("RunAutomation")
            .WithSummary(
                "Runs a manual automation. Refuses an event-driven one: a client must not be able to "
                + "manufacture a trigger.");

        group.MapPost("/simulate", async (
                SimulateAutomationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SimulateAutomationCommand { Request = request }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Simulate)
            .WithName("SimulateAutomation")
            .WithSummary(
                "A dry run against real state. Nothing is sent, nothing is charged and nobody is "
                + "notified — enforced at the dispatcher, not by a flag.");

        group.MapPost("/audience", async (
                AudienceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAudienceQuery { Request = request }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Simulate)
            .WithName("EstimateAutomationAudience")
            .WithSummary(
                "How many people an automation would reach, as a count and a sentence. Never a member "
                + "list, and small counts are banded.");
    }

    // ── Capabilities — GAP-005 ──────────────────────────────────────────────────────────────────

    private static void MapCapabilities(RouteGroupBuilder group) =>
        group.MapGet("/capabilities", async (
                string? ownerKind,
                Guid? ownerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetAutomationCapabilitiesQuery { OwnerKind = ownerKind, OwnerId = ownerId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationCapabilities")
            .WithSummary(
                "What this member may do. Self only; there is no read-another-member's-capabilities "
                + "variant.");

    // ── Schedule — GAP-003 ──────────────────────────────────────────────────────────────────────

    private static void MapSchedule(RouteGroupBuilder group) =>
        group.MapGet("/{automationId:guid}/schedule", async (
                Guid automationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetScheduleQuery { AutomationId = automationId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationSchedule")
            .WithSummary(
                "When this next runs, in the zone it was set in, with any daylight-saving warning "
                + "that applies to it.");

    // ── Reminders — GAP-008 ─────────────────────────────────────────────────────────────────────

    private static void MapReminders(RouteGroupBuilder group)
    {
        group.MapGet("/reminders", async (
                string subjectKind,
                Guid subjectId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListRemindersQuery { SubjectKind = subjectKind, SubjectId = subjectId },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("ListAutomationReminders")
            .WithSummary("This member's reminders on one subject. Self only.");

        group.MapPost("/reminders", async (
                CreateReminderRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateReminderCommand { Request = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(AutomationPolicies.RemindersWrite)
            .WithName("CreateAutomationReminder")
            .WithSummary(
                "Sets a reminder, in the subject's own time zone. Idempotent on member, subject, "
                + "offset and anchor.");

        group.MapDelete("/reminders/{reminderId:guid}", async (
                Guid reminderId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CancelReminderCommand { ReminderId = reminderId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.RemindersWrite)
            .WithName("CancelAutomationReminder")
            .WithSummary("Cancels a reminder. Idempotent.");
    }

    // ── Saved searches — GAP-009 ────────────────────────────────────────────────────────────────

    private static void MapSavedSearches(RouteGroupBuilder group)
    {
        group.MapGet("/saved-searches", async (
                string? subject,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListSavedSearchesQuery { Subject = subject }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("ListSavedSearches")
            .WithSummary("This member's saved searches. Self only; criteria are personal.");

        group.MapPost("/saved-searches", async (
                CreateSavedSearchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateSavedSearchCommand { Request = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(AutomationPolicies.SavedSearchesWrite)
            .WithName("CreateSavedSearch")
            .WithSummary(
                "Saves a search. Idempotent on the normalised criteria: saving the same search twice "
                + "returns the first rather than doubling every alert.");

        group.MapPatch("/saved-searches/{savedSearchId:guid}", async (
                Guid savedSearchId,
                UpdateSavedSearchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UpdateSavedSearchCommand
                    {
                        SavedSearchId = savedSearchId,
                        Request = request
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.SavedSearchesWrite)
            .WithName("UpdateSavedSearch")
            .WithSummary(
                "Changes a saved search's name or alerting. The criteria are not editable: changing "
                + "them would make every item already reported a new match.");

        group.MapDelete("/saved-searches/{savedSearchId:guid}", async (
                Guid savedSearchId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeleteSavedSearchCommand { SavedSearchId = savedSearchId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.SavedSearchesWrite)
            .WithName("DeleteSavedSearch")
            .WithSummary("Deletes a saved search. Idempotent.");
    }

    // ── Templates and the catalogue — GAP-030 ───────────────────────────────────────────────────

    private static void MapTemplates(RouteGroupBuilder group)
    {
        group.MapGet("/templates", async (
                string? category,
                string? ownerKind,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListTemplatesQuery { Category = category, OwnerKind = ownerKind },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("ListAutomationTemplates")
            .WithSummary(
                "Templates, filtered to what this member may create. A template whose capability they "
                + "lack is not shown at all.");

        group.MapGet("/catalog", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetCatalogQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(AutomationPolicies.Read)
            .WithName("GetAutomationCatalog")
            .WithSummary(
                "Every trigger, condition and action, and whether this deployment can actually do it. "
                + "'Not available' is a first-class answer with a reason attached.");
    }
}
