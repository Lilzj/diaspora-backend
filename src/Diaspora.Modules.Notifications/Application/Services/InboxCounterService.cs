using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// Settles a member's counters, from wherever the fact happened.
/// <para>
/// <b>Every counter movement in the module goes through here.</b> The counters are maintained rather
/// than computed — see <c>UserInbox</c> for why — and a maintained counter is only correct if every
/// write path settles it. One class means one place to check when a new write path is added, and one
/// place that knows a category counter needs an identifier the first time it is touched.
/// </para>
/// <para>
/// It runs inside the same unit of work as the notification, because the badge and the message must
/// commit together: a member whose badge says one and whose inbox shows nothing has lost confidence
/// in both.
/// </para>
/// </summary>
public sealed class InboxCounterService
{
    private readonly IUserInboxRepository _inboxes;
    private readonly IGuidGenerator _guids;

    public InboxCounterService(IUserInboxRepository inboxes, IGuidGenerator guids)
    {
        _inboxes = inboxes;
        _guids = guids;
    }

    public async Task RecordArrivalAsync(
        UserId userId, string categoryKey, DateTimeOffset arrivedAt,
        CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordArrival(categoryKey, _guids.NewSequentialGuid(), arrivedAt);

        _inboxes.Update(inbox);
    }

    public async Task RecordReadAsync(
        UserId userId, string categoryKey, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordRead(categoryKey, _guids.NewSequentialGuid());

        _inboxes.Update(inbox);
    }

    public async Task RecordUnreadAsync(
        UserId userId, string categoryKey, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordUnread(categoryKey, _guids.NewSequentialGuid());

        _inboxes.Update(inbox);
    }

    public async Task RecordArchivedAsync(
        UserId userId, string categoryKey, bool wasUnread, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordArchived(categoryKey, _guids.NewSequentialGuid(), wasUnread);

        _inboxes.Update(inbox);
    }

    public async Task RecordUnarchivedAsync(
        UserId userId, string categoryKey, bool isUnread, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordUnarchived(categoryKey, _guids.NewSequentialGuid(), isUnread);

        _inboxes.Update(inbox);
    }

    public async Task RecordDeletedAsync(
        UserId userId, string categoryKey, bool wasUnread, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordDeleted(categoryKey, _guids.NewSequentialGuid(), wasUnread);

        _inboxes.Update(inbox);
    }

    public async Task RecordPinnedAsync(
        UserId userId, int delta, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordPinned(delta);

        _inboxes.Update(inbox);
    }

    public async Task RecordStarredAsync(
        UserId userId, int delta, CancellationToken cancellationToken = default)
    {
        var inbox = await _inboxes.GetOrCreateAsync(userId, cancellationToken);

        inbox.RecordStarred(delta);

        _inboxes.Update(inbox);
    }
}
