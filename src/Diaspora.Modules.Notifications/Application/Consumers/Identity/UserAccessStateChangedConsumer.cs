using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Identity;

/// <summary>
/// An account was suspended, disabled, restricted or reinstated.
/// <para>
/// <b>The archetypal Critical message, and the reason Critical exists.</b> A member whose account
/// has just been restricted must be told, whatever their quiet hours say, whatever they have muted,
/// and whether or not they have any channel switched on — because the alternative is discovering it
/// when something they were relying on stops working, with no explanation and no route to appeal.
/// </para>
/// <para>
/// The urgency is not decided here. It is a property of the rule, which sets Critical for this
/// event, and an operator can see that in a row rather than having to read this file.
/// </para>
/// </summary>
public sealed class UserAccessStateChangedConsumer
    : ModuleEventConsumer<UserAccessStateChangedIntegrationEvent>
{
    public UserAccessStateChangedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Identity";

    protected override IEnumerable<NotificationTrigger> Translate(
        UserAccessStateChangedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["previousState"] = integrationEvent.FromState;
        values["currentState"] = integrationEvent.ToState;
        values["reason"] = integrationEvent.Reason;
        values["changedAt"] = integrationEvent.OccurredAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.OccurredAt,
            values,
            subjectType: "account",
            subjectId: integrationEvent.UserId.ToString());
    }
}
