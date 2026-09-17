using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Realtime;

/// <summary>
/// Who may watch a map cell's alert channel: any signed-in member, for any real cell.
/// <para>
/// <b>The documented rule, per channel family.</b> <c>geo:alerts:{cell}</c> carries signals about
/// public alerts only — the publisher announces nothing a public map would not draw — so there is
/// nothing on it a member could see here and not see by opening the map. The only check is that the
/// name is a cell: an arbitrary string would join a group nothing publishes to, which is harmless and
/// also a way to hold server memory for nothing.
/// </para>
/// <para>
/// <b>No subscriber list.</b> <see cref="IRealtimeChannelAuthorizer.ListSubscribersAsync"/> stays at
/// its default, which names nobody: an alert channel is for whoever has a map open, and nobody is
/// notified through it who is not looking.
/// </para>
/// <para>
/// <b>Backfill on subscribe is the client's.</b> The server does not replay what a cell announced
/// while a phone was away. A client that joins — or rejoins after a reconnect — refetches its viewport
/// once it has joined, which is both simpler and more correct than a replay: the viewport query is
/// the source of truth, and a replay could only ever be a partial copy of it.
/// </para>
/// </summary>
public sealed class AlertCellRealtimeChannelAuthorizer : IRealtimeChannelAuthorizer
{
    public string Module => CommunityModuleMetadata.Name;

    public string ChannelPrefix => RealtimeChannels.AlertCellPrefix;

    public Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default) =>
        Task.FromResult(userId is not null && RealtimeChannels.AlertCellOf(channel) is not null);
}
