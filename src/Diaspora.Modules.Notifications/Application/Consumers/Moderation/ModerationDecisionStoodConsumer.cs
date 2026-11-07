using System.Globalization;
using Diaspora.Contracts.Moderation.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Moderation;

/// <summary>
/// Tells the owner what a removal cost them, once it stood (7 Nov 2026): the warning or strike, what it
/// blocks and until when, and the reputation points it took. The owner asked for exactly this: "give
/// the person warning and tell them if their XP has gone down".
/// </summary>
public sealed class ModerationDecisionStoodConsumer : ModuleEventConsumer<ModerationDecisionStoodIntegrationEvent>
{
    public const string Owner = "owner-stood";

    public ModerationDecisionStoodConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "moderation";

    protected override IEnumerable<NotificationTrigger> Translate(ModerationDecisionStoodIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.OwnerUserId == Guid.Empty)
        {
            yield break;
        }

        var until = integrationEvent.BlockedUntil?.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

        var values = Values();
        values["title"] = string.IsNullOrWhiteSpace(integrationEvent.ContentTitle)
            ? "Your content"
            : integrationEvent.ContentTitle;
        values["standstext"] = integrationEvent.AfterAppeal
            ? "Your appeal was not upheld, so the removal stands."
            : "The time to appeal has passed, so the removal stands.";
        values["striketext"] = integrationEvent.StrikeLevel switch
        {
            "Warning" => "This is a warning. Nothing else happens now, but another removal within twelve "
                + "months would stop you posting in that part of Diaspora for a week.",
            "First" => $"This is a first strike: you cannot post in {integrationEvent.Area} until {until}. "
                + "Everywhere else is unaffected.",
            "Second" => $"This is a second strike within twelve months: you cannot post in {integrationEvent.Area} "
                + $"until {until}. Everywhere else is unaffected.",
            "Third" => "This is a third strike within twelve months. Diaspora staff will now consider suspending "
                + "your account; two of them must agree, and you will be told either way.",
            _ => string.Empty
        };
        values["pointstext"] = integrationEvent.PointsLost > 0
            ? $"Your reputation went down by {integrationEvent.PointsLost} points."
            : string.Empty;
        values["caseid"] = integrationEvent.CaseId.ToString();

        yield return Trigger(
            UserId.From(integrationEvent.OwnerUserId),
            Owner,
            integrationEvent.StoodAt,
            values,
            subjectType: "moderation-case",
            subjectId: integrationEvent.CaseId.ToString());
    }
}
