using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// Tells a member's open app, over the socket, that their access changed.
/// <para>
/// <b>Added 14 Oct 2026.</b> A member waiting on verification learned of the decision only when
/// the app asked again, so an approval reached them 10 to 15 seconds late, and the first fix
/// polled every ten seconds. This replaces the polling: the moment Identity moves the account,
/// the member's own group gets a <see cref="RealtimeEvents.ResourceChanged"/> frame for
/// <c>account</c>, and the app fetches its new access once.
/// </para>
/// <para>
/// <b>Called by <see cref="UserAccessStateChangedConsumer"/>, not a handler of its own:</b> one
/// module reacts to one published fact once (an architecture rule). It runs before the
/// notification is rendered and regardless of it, because a notification may be held back by the
/// member's preferences or quiet hours, and this is not a message to the member; it is the
/// account telling the app it changed. It carries no state names: the app asks for the access
/// decision itself, so a frame can never be the thing that grants anything.
/// </para>
/// </summary>
public sealed class AccessStateRealtimeRelay
{
    /// <summary>The resource type a client switches on.</summary>
    public const string ResourceType = "account";

    /// <summary>The change kind a client switches on.</summary>
    public const string ChangeKind = "access.changed";

    private readonly IRealtimeNotifier _realtime;

    public AccessStateRealtimeRelay(IRealtimeNotifier realtime) => _realtime = realtime;

    public Task SendAsync(
        UserAccessStateChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _realtime.SendToUserAsync(
            UserId.From(integrationEvent.UserId),
            RealtimeEvents.ResourceChanged,
            new ResourceChanged(
                ResourceType,
                integrationEvent.UserId.ToString(),
                ChangeKind,
                Reason: null,
                integrationEvent.OccurredAt),
            cancellationToken);
    }
}
