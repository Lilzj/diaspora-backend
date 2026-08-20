using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// The notification lifecycle, and the two places it is deliberately not a ladder.
/// </summary>
public class NotificationLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_notification_is_created_and_unread()
    {
        var notification = Build();

        notification.Status.Should().Be(NotificationStatus.Created);
        notification.IsUnread.Should().BeTrue();
        notification.Engagement.IsOpened.Should().BeFalse();
    }

    /// <summary>
    /// <b>A member can open a message no channel ever delivered.</b> They opened the app and
    /// scrolled. A strict ladder would refuse this, and it is the normal case.
    /// </summary>
    [Fact]
    public void A_message_can_be_opened_without_ever_being_delivered()
    {
        var notification = Build();

        var result = notification.MarkRead("InApp", Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Opened);
        notification.IsUnread.Should().BeFalse();
    }

    /// <summary>
    /// <b>A click backfills the open.</b> Without this, every open rate under-counts by exactly the
    /// members who tapped straight through from a push banner — the ones who acted fastest.
    /// </summary>
    [Fact]
    public void A_click_without_a_prior_open_records_both()
    {
        var notification = Build();

        notification.RecordClick("view", "Push", Now.AddMinutes(2));

        notification.Engagement.IsClicked.Should().BeTrue();
        notification.Engagement.IsOpened.Should().BeTrue();
        notification.Engagement.OpenedOnChannel.Should().Be("Push");
    }

    /// <summary>
    /// Reading twice is one open. A list screen marks everything visible as read and a client
    /// retries on a flaky connection; a second open moving the counter would leave every busy
    /// member's badge permanently wrong.
    /// </summary>
    [Fact]
    public void Reading_twice_records_one_open()
    {
        var notification = Build();

        notification.MarkRead("InApp", Now.AddMinutes(5));
        var opened = notification.Engagement.OpenedAt;

        notification.MarkRead("InApp", Now.AddMinutes(30));

        notification.Engagement.OpenedAt.Should().Be(opened);
    }

    /// <summary>
    /// Restoring puts the message back to what it was, not to "delivered". Collapsing every restore
    /// would mark a read message unread and put a badge back for something already dealt with.
    /// </summary>
    [Fact]
    public void Restoring_an_archived_message_keeps_it_read()
    {
        var notification = Build();

        notification.MarkRead("InApp", Now.AddMinutes(5));
        notification.Archive(ArchiveReason.Archived, byRecipient: true, Now.AddMinutes(10));
        notification.Unarchive();

        notification.Status.Should().Be(NotificationStatus.Opened);
        notification.IsUnread.Should().BeFalse();
    }

    /// <summary>
    /// Expiry is the platform saying the message is no longer true — a reminder for a viewing that
    /// has happened. Handing it back would be worse than losing it.
    /// </summary>
    [Fact]
    public void An_expired_message_cannot_be_restored()
    {
        var notification = Build();

        notification.Archive(ArchiveReason.Expired, byRecipient: false, Now.AddDays(2));

        notification.Unarchive().IsFailure.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Expired);
    }

    [Fact]
    public void Archiving_clears_a_pin()
    {
        var notification = Build();

        notification.Pin();
        notification.Archive(ArchiveReason.Archived, byRecipient: true, Now.AddMinutes(10));

        notification.IsPinned.Should().BeFalse();
    }

    /// <summary>
    /// Dismissing counts as reading. A dismissed message that still counted as unread would leave a
    /// badge nobody could clear without un-dismissing it.
    /// </summary>
    [Fact]
    public void Dismissing_marks_the_message_read()
    {
        var notification = Build();

        notification.Dismiss(Now.AddMinutes(1));

        notification.Engagement.IsDismissed.Should().BeTrue();
        notification.IsUnread.Should().BeFalse();
    }

    internal static Notification Build(
        NotificationPriority priority = NotificationPriority.Normal,
        DateTimeOffset? expiresAt = null)
    {
        var result = Notification.Create(
            NotificationId.New(),
            UserId.New(),
            "jobs",
            priority,
            "jobs.interview.scheduled",
            "jobs.interview.scheduled",
            NotificationSource.Create("Jobs", "InterviewScheduledIntegrationEvent", "interview", "42"),
            NotificationContent.Create("Interview confirmed", "Your interview is on Thursday."),
            DeepLink.TryCreate("/jobs/42", "job", "42"),
            Now,
            expiresAt);

        result.IsSuccess.Should().BeTrue();

        return result.Value;
    }
}
