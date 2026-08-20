using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Notifications;

/// <summary>
/// One message addressed to one member, and everything that happened to it.
/// <para>
/// <b>The aggregate boundary is one message, not one member's inbox.</b> An inbox holding two years
/// of messages would be loaded in full to mark one of them read, and two devices marking two
/// different messages read at the same moment would collide on a single row. The counters that make
/// the inbox screen fast live in <c>UserInbox</c>, which is a separate aggregate settled from here.
/// </para>
/// <para>
/// <b>The content is rendered once and frozen.</b> Everything a member sees was produced at creation
/// from a template and a set of variables, and neither is consulted again. Re-rendering on read
/// would let a template edit rewrite a message somebody was sent last month, and would make the
/// audit trail — which is the point of keeping notifications at all — a record of the present rather
/// than the past.
/// </para>
/// <para>
/// <b>Delivery is per channel and status is not.</b> The same message can be sitting unread in the
/// inbox, suppressed on push because the member is asleep, and waiting for tomorrow's digest on
/// email. <see cref="Status"/> answers "has the member dealt with this"; each
/// <see cref="NotificationDelivery"/> answers "did this transport manage it".
/// </para>
/// <para>
/// <b>Soft-deletable, and the delete is genuinely rare.</b> Members archive; the retention sweep
/// expires. Deletion exists for the one case that matters — a member exercising their right to have
/// a message removed — and it is soft so that the delivery record survives for the abuse
/// investigation that may need it. Note that <see cref="ISoftDeletable.Restore"/> is the
/// undelete; bringing a message back from the archive is <see cref="Unarchive"/>, and the two are
/// deliberately different names because conflating them is how a restore path ends up unable to
/// find its own row behind the global query filter.
/// </para>
/// </summary>
public sealed class Notification : AggregateRoot<NotificationId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<NotificationDelivery> _deliveries = [];
    private readonly List<NotificationDeliveryAttempt> _attempts = [];
    private readonly List<NotificationAction> _actions = [];

    private Notification(
        NotificationId id,
        UserId recipientUserId,
        string categoryKey,
        NotificationPriority priority,
        string templateKey,
        string? ruleKey,
        NotificationSource source,
        NotificationContent content,
        DeepLink? primaryLink,
        DateTimeOffset createdAt) : base(id)
    {
        RecipientUserId = recipientUserId;
        CategoryKey = categoryKey;
        Priority = priority;
        TemplateKey = templateKey;
        RuleKey = ruleKey;
        Source = source;
        Content = content;
        PrimaryLink = primaryLink;
        Status = NotificationStatus.Created;
        Engagement = NotificationEngagement.None();
        CreatedAt = createdAt;
    }

    private Notification() { }

    public UserId RecipientUserId { get; private set; } = default!;

    /// <summary>The catalogue key. Lower-cased, and the only classification the module stores.</summary>
    public string CategoryKey { get; private set; } = string.Empty;

    public NotificationPriority Priority { get; private set; }

    /// <summary>Which template produced the words. Kept as a key, not a reference — see the class summary.</summary>
    public string TemplateKey { get; private set; } = string.Empty;

    /// <summary>Which routing rule matched. Null when a command dispatched directly.</summary>
    public string? RuleKey { get; private set; }

    public NotificationSource Source { get; private set; } = default!;

    public NotificationContent Content { get; private set; } = default!;

    /// <summary>Where tapping the card goes. Already substituted.</summary>
    public DeepLink? PrimaryLink { get; private set; }

    public NotificationStatus Status { get; private set; }

    /// <summary>
    /// What it was before it was archived, so <see cref="Unarchive"/> can put it back correctly.
    /// <para>
    /// Without this, restoring would have to guess — and every reasonable guess marks a read message
    /// unread, which puts a badge back on the client for something the member has already dealt with.
    /// </para>
    /// </summary>
    public NotificationStatus? StatusBeforeArchive { get; private set; }

    public ArchiveReason? ArchivedFor { get; private set; }

    public NotificationEngagement Engagement { get; private set; } = default!;

    public bool IsPinned { get; private set; }

    public bool IsStarred { get; private set; }

    /// <summary>When the retention sweep may age it out. Null for messages that never expire.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// A key shared by messages that are really the same thing — three reactions to one post.
    /// Grouping and de-duplication both key on it.
    /// </summary>
    public string? GroupKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();

    public IReadOnlyList<NotificationDeliveryAttempt> Attempts =>
        _attempts.OrderBy(attempt => attempt.AttemptedAt).ToList().AsReadOnly();

    public IReadOnlyList<NotificationAction> Actions => _actions.AsReadOnly();

    // ── Auditing and soft deletion ──────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether it counts against the unread badge.</summary>
    public bool IsUnread =>
        !Engagement.IsOpened && NotificationStatusTransitions.CountsAsUnread(Status);

    public bool IsArchived => Status == NotificationStatus.Archived;

    public bool IsExpired => Status == NotificationStatus.Expired;

    /// <summary>Whether the member can still act on it.</summary>
    public bool IsLive => NotificationStatusTransitions.IsLive(Status);

    /// <summary>The delivery for one channel, or null when that channel was never asked.</summary>
    public NotificationDelivery? DeliveryFor(NotificationChannelKind channel) =>
        _deliveries.Find(delivery => delivery.Channel == channel);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Addresses a rendered message to a member.
    /// <para>
    /// Everything is already decided by this point — the rule matched, the template rendered, the
    /// preferences were consulted. This aggregate never resolves any of that, because a domain object
    /// that reached out to a preference repository to decide its own shape would be untestable and
    /// would put a database round trip inside a constructor.
    /// </para>
    /// </summary>
    public static Result<Notification> Create(
        NotificationId id,
        UserId recipientUserId,
        string categoryKey,
        NotificationPriority priority,
        string templateKey,
        string? ruleKey,
        NotificationSource source,
        NotificationContent content,
        DeepLink? primaryLink,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        string? groupKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(recipientUserId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return Result.Failure<Notification>(NotificationErrors.CategoryNotFound);
        }

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return Result.Failure<Notification>(NotificationErrors.TemplateNotFound);
        }

        if (expiresAt is { } expiry && expiry <= createdAt)
        {
            return Result.Failure<Notification>(NotificationErrors.NotificationExpired);
        }

        var notification = new Notification(
            id,
            recipientUserId,
            categoryKey.Trim().ToLowerInvariant(),
            priority,
            templateKey.Trim(),
            string.IsNullOrWhiteSpace(ruleKey) ? null : ruleKey.Trim(),
            source,
            content,
            primaryLink,
            createdAt)
        {
            ExpiresAt = expiresAt,
            GroupKey = string.IsNullOrWhiteSpace(groupKey) ? null : groupKey.Trim()
        };

        notification.RaiseDomainEvent(new NotificationCreatedDomainEvent
        {
            NotificationId = id,
            RecipientUserId = recipientUserId,
            CategoryKey = notification.CategoryKey,
            Priority = priority,
            TemplateKey = notification.TemplateKey,
            RuleKey = notification.RuleKey,
            SourceModule = source.Module,
            SourceEventName = source.EventName,
            SubjectId = source.SubjectId,
            CreatedAt = createdAt
        });

        return Result.Success(notification);
    }

    /// <summary>Attaches the actions the template offered, already substituted. Replaces any existing set.</summary>
    public void SetActions(IEnumerable<NotificationAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions.Clear();
        _actions.AddRange(actions.Take(NotificationLimits.MaxActions));

        Touch();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Delivery
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Asks a channel to carry the message.
    /// <para>
    /// Idempotent per channel: queueing the same channel twice returns the delivery that already
    /// exists rather than creating a second, because at-least-once event delivery means this method
    /// will genuinely be called twice for the same fact and two rows would mean two pushes.
    /// </para>
    /// </summary>
    public NotificationDelivery Queue(
        Guid deliveryId,
        NotificationChannelKind channel,
        DateTimeOffset deliverAfter,
        DateTimeOffset now)
    {
        if (DeliveryFor(channel) is { } existing)
        {
            return existing;
        }

        var delivery = NotificationDelivery.Pending(deliveryId, channel, deliverAfter, now);

        _deliveries.Add(delivery);

        if (deliverAfter > now)
        {
            delivery.Defer(deliverAfter);
        }

        if (NotificationStatusTransitions.CanTransition(Status, NotificationStatus.Queued))
        {
            Status = NotificationStatus.Queued;
        }

        Touch();

        RaiseDomainEvent(new NotificationQueuedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            Priority = Priority,
            Channel = channel,
            DeliverAfter = deliverAfter,
            QueuedAt = now
        });

        return delivery;
    }

    /// <summary>Records that a channel is being attempted now.</summary>
    public bool BeginDelivery(NotificationChannelKind channel, DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel);

        if (delivery is null || !delivery.BeginAttempt(now))
        {
            return false;
        }

        Touch();

        return true;
    }

    /// <summary>
    /// A channel succeeded.
    /// <para>
    /// The message becomes <see cref="NotificationStatus.Delivered"/> on the first channel that
    /// manages it and stays there; a second channel succeeding afterwards changes only its own row.
    /// A message the member has already opened is not walked backwards.
    /// </para>
    /// </summary>
    public Result RecordDelivered(
        NotificationChannelKind channel,
        Guid attemptId,
        string? providerMessageId,
        DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel);

        if (delivery is null)
        {
            return Result.Failure(NotificationErrors.DeliveryNotFound);
        }

        if (!delivery.Succeed(providerMessageId, now))
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        _attempts.Add(NotificationDeliveryAttempt.ForSuccess(
            attemptId, channel, delivery.AttemptCount, providerMessageId, now));

        if (NotificationStatusTransitions.CanTransition(Status, NotificationStatus.Delivered))
        {
            Status = NotificationStatus.Delivered;
        }

        Touch();

        RaiseDomainEvent(new NotificationDeliveredDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            Channel = channel,
            Attempt = delivery.AttemptCount,
            ProviderMessageId = providerMessageId,
            DeliveredAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// A channel failed.
    /// <para>
    /// The domain event is raised only when the ladder has ended, because a retry is not news. The
    /// message itself only becomes <see cref="NotificationStatus.Failed"/> when <em>every</em>
    /// channel has finished and none succeeded — a push that failed while the inbox row is sitting
    /// there perfectly readable has not failed the member.
    /// </para>
    /// </summary>
    public Result RecordFailure(
        NotificationChannelKind channel,
        Guid attemptId,
        string reasonCode,
        string? detail,
        bool isPermanent,
        DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel);

        if (delivery is null)
        {
            return Result.Failure(NotificationErrors.DeliveryNotFound);
        }

        var attemptNumber = delivery.AttemptCount;
        var exhausted = delivery.Fail(reasonCode, isPermanent, Priority, now);

        _attempts.Add(NotificationDeliveryAttempt.ForFailure(
            attemptId, channel, attemptNumber, reasonCode, detail, now));

        Touch();

        if (!exhausted)
        {
            return Result.Success();
        }

        RaiseDomainEvent(new NotificationFailedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            Channel = channel,
            Attempts = delivery.AttemptCount,
            ReasonCode = reasonCode,
            IsPermanent = isPermanent,
            FailedAt = now
        });

        if (_deliveries.TrueForAll(entry => entry.IsTerminal)
            && !_deliveries.Exists(entry => entry.Status == DeliveryStatus.Delivered)
            && NotificationStatusTransitions.CanTransition(Status, NotificationStatus.Failed))
        {
            Status = NotificationStatus.Failed;
        }

        return Result.Success();
    }

    /// <summary>The member's preference stopped this channel. Terminal for that channel only.</summary>
    public void Suppress(
        Guid deliveryId, NotificationChannelKind channel, string reasonCode, DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel)
            ?? AddSilently(NotificationDelivery.Pending(deliveryId, channel, now, now));

        delivery.Suppress(reasonCode, now);

        Touch();
    }

    /// <summary>Folded into a digest. The digest carries it from here.</summary>
    public void FoldIntoDigest(
        Guid deliveryId, NotificationChannelKind channel, Guid digestId, DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel)
            ?? AddSilently(NotificationDelivery.Pending(deliveryId, channel, now, now));

        delivery.Digest(digestId, now);

        Touch();
    }

    /// <summary>No transport is registered for the channel. Recorded rather than skipped.</summary>
    public void MarkChannelUnsupported(
        Guid deliveryId, NotificationChannelKind channel, DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel)
            ?? AddSilently(NotificationDelivery.Pending(deliveryId, channel, now, now));

        delivery.MarkUnsupported(now);

        Touch();
    }

    /// <summary>An operator asked for one more attempt on a failed channel.</summary>
    public Result Requeue(NotificationChannelKind channel, DateTimeOffset now)
    {
        var delivery = DeliveryFor(channel);

        if (delivery is null)
        {
            return Result.Failure(NotificationErrors.DeliveryNotFound);
        }

        if (!delivery.Requeue(now))
        {
            return Result.Failure(NotificationErrors.DeliveryNotRetryable);
        }

        Touch();

        return Result.Success();
    }

    private NotificationDelivery AddSilently(NotificationDelivery delivery)
    {
        _deliveries.Add(delivery);
        return delivery;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // What the member does with it
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The member read it.
    /// <para>
    /// Idempotent, and that matters more here than anywhere else in the module: a list screen marks
    /// everything visible as read, a client retries on a flaky connection, and a second open that
    /// moved the counter would leave every busy member's badge permanently negative.
    /// </para>
    /// </summary>
    public Result MarkRead(string channel, DateTimeOffset now)
    {
        if (IsExpired)
        {
            return Result.Failure(NotificationErrors.NotificationExpired);
        }

        if (Engagement.IsOpened)
        {
            return Result.Success();
        }

        var minutesToOpen = Math.Max(0, (now - CreatedAt).TotalMinutes);

        Engagement = Engagement.WithOpened(now, channel);

        if (NotificationStatusTransitions.CanTransition(Status, NotificationStatus.Opened))
        {
            Status = NotificationStatus.Opened;
        }

        Touch();

        RaiseDomainEvent(new NotificationOpenedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            TemplateKey = TemplateKey,
            Channel = channel,
            MinutesToOpen = minutesToOpen,
            OpenedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The member put it back to unread.
    /// <para>
    /// The one engagement fact a member may undo. The open <em>event</em> is not retracted — it
    /// happened, and the analytics keep it — but the badge comes back, which is what the member
    /// asked for.
    /// </para>
    /// </summary>
    public Result MarkUnread()
    {
        if (!IsLive)
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        if (!Engagement.IsOpened)
        {
            return Result.Success();
        }

        Engagement = Engagement.WithoutOpened();
        Status = NotificationStatus.Delivered;

        Touch();

        return Result.Success();
    }

    /// <summary>The member followed it somewhere. Backfills the read if the click arrived first.</summary>
    public Result RecordClick(string? actionKey, string channel, DateTimeOffset now)
    {
        if (IsExpired)
        {
            return Result.Failure(NotificationErrors.NotificationExpired);
        }

        if (Engagement.IsClicked)
        {
            return Result.Success();
        }

        var wasUnopened = !Engagement.IsOpened;
        var minutesToOpen = Math.Max(0, (now - CreatedAt).TotalMinutes);

        Engagement = Engagement.WithClicked(now, channel);

        if (NotificationStatusTransitions.CanTransition(Status, NotificationStatus.Clicked))
        {
            Status = NotificationStatus.Clicked;
        }

        Touch();

        // A click that arrived without a prior open is also the open. Without this, every open-rate
        // under-counts by exactly the members who tapped straight through from a push banner.
        if (wasUnopened)
        {
            RaiseDomainEvent(new NotificationOpenedDomainEvent
            {
                NotificationId = Id,
                RecipientUserId = RecipientUserId,
                CategoryKey = CategoryKey,
                TemplateKey = TemplateKey,
                Channel = channel,
                MinutesToOpen = minutesToOpen,
                OpenedAt = now
            });
        }

        RaiseDomainEvent(new NotificationClickedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            TemplateKey = TemplateKey,
            TargetRoute = PrimaryLink?.Route,
            ActionKey = actionKey,
            ClickedAt = now
        });

        return Result.Success();
    }

    /// <summary>The member refused it. Counts as read; does not leave the inbox on its own.</summary>
    public Result Dismiss(DateTimeOffset now)
    {
        if (!IsLive)
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        Engagement = Engagement.WithDismissed(now).WithOpened(now, nameof(NotificationChannelKind.InApp));
        StatusBeforeArchive = Status;
        Status = NotificationStatus.Dismissed;

        Touch();

        return Result.Success();
    }

    /// <summary>
    /// Files it, or records that the sweep aged it out.
    /// <para>
    /// The state before the archive is remembered so <see cref="Unarchive"/> can put it back
    /// exactly, rather than guessing and marking a read message unread.
    /// </para>
    /// </summary>
    public Result Archive(ArchiveReason reason, bool byRecipient, DateTimeOffset now)
    {
        if (IsArchived && reason != ArchiveReason.Expired)
        {
            return Result.Failure(NotificationErrors.NotificationAlreadyArchived);
        }

        var target = reason == ArchiveReason.Expired
            ? NotificationStatus.Expired
            : NotificationStatus.Archived;

        if (!NotificationStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        StatusBeforeArchive ??= Status;
        Status = target;
        ArchivedFor = reason;
        Engagement = Engagement.WithArchived(now);
        IsPinned = false;

        Touch();

        RaiseDomainEvent(new NotificationArchivedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            Reason = reason,
            ByRecipient = byRecipient,
            WasUnread = !Engagement.IsOpened,
            ArchivedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Brings a filed message back to the inbox.
    /// <para>
    /// Named <c>Unarchive</c> and not <c>Restore</c>: <see cref="ISoftDeletable.Restore"/> already
    /// owns that name for undeleting, and a codebase where the two are spelled the same is one where
    /// a restore command eventually calls the wrong one.
    /// </para>
    /// <para>
    /// An expired message does not come back. Expiry is the platform saying the message is no longer
    /// true — a reminder for a viewing that has happened, a code that no longer works — and handing
    /// it back would be worse than losing it.
    /// </para>
    /// </summary>
    public Result Unarchive()
    {
        if (IsExpired)
        {
            return Result.Failure(NotificationErrors.NotificationExpired);
        }

        if (!IsArchived && Status != NotificationStatus.Dismissed)
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        Status = NotificationStatusTransitions.RestoreTargetFor(StatusBeforeArchive);
        StatusBeforeArchive = null;
        ArchivedFor = null;
        Engagement = Engagement.WithoutArchived();

        Touch();

        return Result.Success();
    }

    /// <summary>Pins it to the top of the inbox. The ceiling is enforced by the caller, which can count.</summary>
    public Result Pin()
    {
        if (!IsLive)
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        IsPinned = true;
        Touch();

        return Result.Success();
    }

    public void Unpin()
    {
        IsPinned = false;
        Touch();
    }

    /// <summary>Stars it. Independent of pinning: a star is a bookmark, a pin is a position.</summary>
    public Result Star()
    {
        if (!IsLive && !IsArchived)
        {
            return Result.Failure(NotificationErrors.NotificationTransitionInvalid);
        }

        IsStarred = true;
        Touch();

        return Result.Success();
    }

    public void Unstar()
    {
        IsStarred = false;
        Touch();
    }

    /// <summary>Sets or clears the retention deadline. Used by the rule, and by an operator repairing one.</summary>
    public void SetExpiry(DateTimeOffset? expiresAt)
    {
        ExpiresAt = expiresAt;
        Touch();
    }

    /// <summary>Whether the retention sweep should age it out at this moment.</summary>
    public bool ShouldExpireAt(DateTimeOffset now) =>
        ExpiresAt is { } expiry && expiry <= now && !IsExpired && !IsDeleted;

    // ── Auditing and soft deletion ──────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new NotificationDeletedDomainEvent
        {
            NotificationId = Id,
            RecipientUserId = RecipientUserId,
            CategoryKey = CategoryKey,
            WasUnread = IsUnread,
            DeletedAt = timestamp
        });
    }

    /// <summary>
    /// Undeletes. The <see cref="ISoftDeletable"/> half of the pair, distinct from
    /// <see cref="Unarchive"/>. Callers must load the row with the global query filter disabled,
    /// because the filter hides exactly the row every restore needs.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Touch();
    }

    /// <summary>
    /// Bumps the concurrency token.
    /// <para>
    /// Called by every mutation, including the ones that only move an owned row — EF will not
    /// notice a change inside an owned collection as a change to the principal, so without this two
    /// concurrent channel results could each read revision 4, each write revision 4, and the second
    /// would win silently.
    /// </para>
    /// </summary>
    private void Touch() => Revision++;
}
