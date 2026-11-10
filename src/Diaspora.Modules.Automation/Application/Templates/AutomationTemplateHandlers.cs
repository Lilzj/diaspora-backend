using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Automation.Application.Templates;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-030 — server-published templates, and the catalogue behind them.
//
// A template is a composition of catalogue entries, so it is derived from the catalogue rather than
// stored beside it. That is what makes the register's client-side rule — "the client must ignore any
// template referencing a trigger or action key this build does not know" — impossible to violate from
// the server: a template naming something the catalogue does not have could not be built here.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What the builder may offer, filtered to what this member may create.</summary>
[Authorize(Policy = AutomationPolicies.Read)]
public sealed record ListTemplatesQuery : IQuery<IReadOnlyList<AutomationTemplateResponse>>
{
    public string? Category { get; init; }

    public string? OwnerKind { get; init; }
}

public sealed class ListTemplatesQueryHandler
    : IQueryHandler<ListTemplatesQuery, IReadOnlyList<AutomationTemplateResponse>>
{
    private readonly AutomationAccessService _access;

    public ListTemplatesQueryHandler(AutomationAccessService access) => _access = access;

    public async Task<Result<IReadOnlyList<AutomationTemplateResponse>>> Handle(
        ListTemplatesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var access = await _access.DescribeAsync(
            Enum.TryParse<AutomationOwnerKind>(query.OwnerKind, ignoreCase: true, out var kind)
                ? kind
                : null,
            null,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AutomationTemplateResponse>>(access.Error);
        }

        var granted = access.Value.GrantedCapabilities;

        // Filtered rather than annotated. A template whose capabilities the member lacks is not shown
        // at all: a greyed-out "Create a community-wide announcement" tells an ordinary member that
        // such a thing exists, that somebody can send them, and that they are not that person — which
        // maps the community's governance for anybody who opens the screen.
        var templates = AutomationTemplateCatalog.All
            .Where(template =>
                template.RequiredCapabilities.All(
                    capability => granted.Contains(capability, StringComparer.Ordinal)))
            .Where(template =>
                string.IsNullOrWhiteSpace(query.Category) ||
                string.Equals(template.Category, query.Category, StringComparison.OrdinalIgnoreCase))
            .Where(template =>
                query.OwnerKind is null ||
                template.AllowedOwners.Contains(
                    AutomationMapper.OwnerKindName(kind), StringComparer.Ordinal))
            .ToArray();

        return Result.Success<IReadOnlyList<AutomationTemplateResponse>>(templates);
    }
}

/// <summary>The trigger, condition and action vocabulary, and what this deployment can do with it.</summary>
[Authorize(Policy = AutomationPolicies.Read)]
public sealed record GetCatalogQuery : IQuery<AutomationCatalogResponse>;

public sealed class GetCatalogQueryHandler : IQueryHandler<GetCatalogQuery, AutomationCatalogResponse>
{
    private readonly IReadOnlySet<string> _answeredExternally;

    /// <summary>
    /// Reads which condition seams this deployment has claimed.
    /// <para>
    /// The same <c>IAutomationConditionSource</c> set the engine resolves from, so the catalogue and
    /// the evaluator cannot disagree about whether a balance can be checked. They did disagree: the
    /// catalogue read a static flag that predated the seam and told every member "nothing in this
    /// deployment answers for it" about five conditions three modules answer.
    /// </para>
    /// </summary>
    public GetCatalogQueryHandler(IEnumerable<IAutomationConditionSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _answeredExternally = sources
            .SelectMany(source => source.Answers)
            .ToHashSet(StringComparer.Ordinal);
    }

    public Task<Result<AutomationCatalogResponse>> Handle(
        GetCatalogQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(AutomationMapper.Catalog(_answeredExternally)));
}

/// <summary>
/// The templates this deployment publishes.
/// <para>
/// <b>Derived from the catalogue, so support is a property rather than a claim.</b> Every template
/// below is built from keys the catalogue holds, and its support is computed from those keys — a
/// template whose trigger became unobservable reports <c>BACKEND_GAP</c> without anybody editing it.
/// </para>
/// <para>
/// <b>Six, and every one is a shape members actually asked for</b> in the frontend's own template
/// gallery. There is no money template and no messaging template, because there is no action that
/// could carry either.
/// </para>
/// </summary>
internal static class AutomationTemplateCatalog
{
    public static readonly IReadOnlyList<AutomationTemplateResponse> All =
    [
        Template(
            "saved-search-alert",
            "Tell me when something new matches",
            "Watch a search you saved and get a notification the moment something new matches it.",
            "alerts",
            "search",
            "savedSearch.newMatch",
            "notify.push",
            ["user"],
            []),

        Template(
            "event-reminder",
            "Remind me before an event",
            "Get a reminder before an event you are going to starts, in the event's own time zone.",
            "reminders",
            "events",
            "event.rsvpConfirmed",
            "reminder.schedule",
            ["user"],
            []),

        Template(
            "event-changed",
            "Tell me when an event changes",
            "Get told when an event you are going to is moved or cancelled.",
            "alerts",
            "events",
            "event.updated",
            "notify.self",
            ["user"],
            []),

        Template(
            "application-status",
            "Tell me when my application moves",
            "Get a notification when the status of a job application of yours changes.",
            "alerts",
            "jobs",
            "job.applicationStatusChanged",
            "notify.push",
            ["user"],
            []),

        Template(
            "payment-receipt",
            "Tell me when a payment completes",
            "Get told when a payment of yours goes through. This never moves any money.",
            "money",
            "wallet",
            "payment.completed",
            "notify.self",
            ["user"],
            [AutomationCapabilities.Financial]),

        Template(
            "community-announcement",
            "Tell the community about an announcement",
            "When an announcement is published, tell the community's members about it.",
            "community",
            "community",
            "community.announcementPublished",
            "notify.community",
            ["community"],
            [AutomationCapabilities.Community])
    ];

    private static AutomationTemplateResponse Template(
        string id,
        string name,
        string description,
        string category,
        string domain,
        string triggerKey,
        string actionKey,
        IReadOnlyList<string> owners,
        IReadOnlyList<string> capabilities)
    {
        var trigger = AutomationCatalog.FindTrigger(triggerKey);
        var action = AutomationCatalog.FindAction(actionKey);
        var gaps = new List<string>();

        if (trigger?.IsAvailable != true)
        {
            gaps.Add(triggerKey);
        }

        if (action?.IsAvailable != true)
        {
            gaps.Add(actionKey);
        }

        return new AutomationTemplateResponse
        {
            Id = id,
            Name = name,
            Description = description,
            Category = category,
            Domain = domain,
            Trigger = new AutomationTriggerResponse
            {
                Key = triggerKey,
                Parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            },
            Conditions = [],
            Actions =
            [
                new AutomationActionResponse
                {
                    // A stable identifier per template action, so a client that fills a template in
                    // and posts it back carries the same one and an edit does not renumber it.
                    Id = Guid.Empty.ToString("D"),
                    Key = actionKey,
                    Parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                }
            ],
            AllowedOwners = owners,
            RequiredCapabilities = capabilities,

            // Derived from the parts, never declared. A template is only as executable as its weakest
            // component, and one that claimed more than its trigger could deliver would be a row a
            // member could open and not use.
            Support = gaps.Count == 0 ? "CONNECTED" : "BACKEND_GAP",
            GapKeys = gaps
        };
    }
}
