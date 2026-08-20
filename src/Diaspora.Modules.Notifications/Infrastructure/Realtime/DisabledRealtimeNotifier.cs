using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// The notifier a deployment with real-time switched off gets. It does nothing, successfully.
/// <para>
/// <b>Registered rather than omitted, and the reason is a defect this file exists to prevent.</b>
/// With the runtime off, <c>AddSignalR</c> is never called, so <c>IHubContext&lt;RealtimeHub&gt;</c>
/// is not in the container — and <see cref="SignalRRealtimeNotifier"/> depends on it. Leaving that
/// registration in place would make every in-app delivery throw a resolution error, on a deployment
/// whose only sin was setting one flag to false. Nothing at startup would catch it: the port
/// resolves fine in the validator's eyes because the validator does not build it, and the first
/// symptom would be the delivery sweep failing in production.
/// </para>
/// <para>
/// <b>It reports <see cref="IsEnabled"/> as false</b>, so callers that check first — the broadcaster
/// does — skip their work rather than doing it and discarding the result. The three send methods
/// still exist and still succeed, because a caller that forgot to check must not be the reason a
/// notification fails to reach an inbox.
/// </para>
/// </summary>
public sealed class DisabledRealtimeNotifier : IRealtimeNotifier
{
    public bool IsEnabled => false;

    public Task SendToUserAsync(
        UserId userId, string eventName, object payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendToGroupAsync(
        string group, string eventName, object payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendToGroupExceptAsync(
        string group, string exceptConnectionId, string eventName, object payload,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
