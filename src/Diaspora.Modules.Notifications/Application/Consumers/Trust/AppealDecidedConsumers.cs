using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// Tells a member their appeal was upheld (7 Nov 2026). Appeal decisions were published with the
/// reviewer's explanation and nobody was told: the member found out, if at all, from their timeline.
/// </summary>
public sealed class AppealApprovedConsumer : ModuleEventConsumer<AppealApprovedIntegrationEvent>
{
    public AppealApprovedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(AppealApprovedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "appellant",
            integrationEvent.DecidedAt,
            AppealValues.For(integrationEvent.AppealType, integrationEvent.Explanation),
            subjectType: "appeal",
            subjectId: integrationEvent.AppealId.ToString());
    }
}

/// <summary>Tells a member their appeal was not upheld, with the reviewer's explanation (7 Nov 2026).</summary>
public sealed class AppealRejectedConsumer : ModuleEventConsumer<AppealRejectedIntegrationEvent>
{
    public AppealRejectedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(AppealRejectedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "appellant",
            integrationEvent.DecidedAt,
            AppealValues.For(integrationEvent.AppealType, integrationEvent.Explanation),
            subjectType: "appeal",
            subjectId: integrationEvent.AppealId.ToString());
    }
}

internal static class AppealValues
{
    public static Dictionary<string, object?> For(string appealType, string? explanation)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["appealtype"] = Describe(appealType),
            ["explanationtext"] = string.IsNullOrWhiteSpace(explanation)
                ? string.Empty
                : $"The reviewer's explanation: {explanation.Trim().TrimEnd('.')}. "
        };

        return values;
    }

    private static string Describe(string appealType) => appealType switch
    {
        "Verification" => "verification",
        "Content" => "content",
        "Suspension" => "suspension",
        "Publishing" => "publishing",
        "RewardHold" => "reward",
        "Money" => "wallet or refund",
        _ => appealType.ToLowerInvariant()
    };
}
