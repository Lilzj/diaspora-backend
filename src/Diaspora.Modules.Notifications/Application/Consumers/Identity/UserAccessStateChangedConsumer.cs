using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Realtime;
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
    private readonly AccessStateRealtimeRelay _realtime;

    public UserAccessStateChangedConsumer(INotificationDispatcher dispatcher, AccessStateRealtimeRelay realtime)
        : base(dispatcher) => _realtime = realtime;

    /// <summary>
    /// The open app hears first (14 Oct 2026, see <see cref="AccessStateRealtimeRelay"/>), then the
    /// notification is rendered as before.
    /// </summary>
    protected override async Task HandleAsync(
        UserAccessStateChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await _realtime.SendAsync(integrationEvent, cancellationToken);
        await base.HandleAsync(integrationEvent, cancellationToken);
    }

    protected override string SourceModule => "Identity";

    protected override IEnumerable<NotificationTrigger> Translate(
        UserAccessStateChangedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["previousState"] = integrationEvent.FromState;
        values["currentState"] = integrationEvent.ToState;
        values["reason"] = integrationEvent.Reason;
        values["changedAt"] = integrationEvent.OccurredAt;

        // The reason staff gave, and when it ends, in the words the member reads (7 Nov 2026).
        values["reasontext"] = string.IsNullOrWhiteSpace(integrationEvent.Note)
            ? string.Empty
            : $"The reason Diaspora gave: {integrationEvent.Note.Trim().TrimEnd('.')}. ";
        values["untiltext"] = integrationEvent.Until is { } until
            ? $"It ends on {until.UtcDateTime.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}. "
            : string.Empty;
        values["nextsteps"] = integrationEvent.ToState is "Suspended" or "Disabled"
            ? "If you think this is a mistake, open Diaspora and appeal from the screen you see."
            : string.Empty;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.OccurredAt,
            values,
            subjectType: "account",
            subjectId: integrationEvent.UserId.ToString());
    }
}
