using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// Per-channel delivery, the retry ladder and what priority does to it.
/// </summary>
public class NotificationDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Queueing the same channel twice returns the row that exists. At-least-once event delivery
    /// means this genuinely happens, and two rows would mean two pushes.
    /// </summary>
    [Fact]
    public void Queueing_a_channel_twice_produces_one_delivery()
    {
        var notification = NotificationLifecycleTests.Build();

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);
        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);

        notification.Deliveries.Should().ContainSingle();
    }

    /// <summary>
    /// A push failing while the inbox row sits there perfectly readable has not failed the member.
    /// The message is only <c>Failed</c> when every channel is finished and none succeeded.
    /// </summary>
    [Fact]
    public void One_channel_failing_does_not_fail_the_message()
    {
        var notification = NotificationLifecycleTests.Build();

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.InApp, Now, Now);
        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);

        notification.BeginDelivery(NotificationChannelKind.InApp, Now);
        notification.RecordDelivered(NotificationChannelKind.InApp, Guid.NewGuid(), null, Now);

        notification.BeginDelivery(NotificationChannelKind.Push, Now);
        notification.RecordFailure(
            NotificationChannelKind.Push, Guid.NewGuid(), "Rejected", null, isPermanent: true, Now);

        notification.Status.Should().Be(NotificationStatus.Delivered);
        notification.DeliveryFor(NotificationChannelKind.Push)!.Status
            .Should().Be(DeliveryStatus.Failed);
    }

    [Fact]
    public void Every_channel_failing_fails_the_message()
    {
        var notification = NotificationLifecycleTests.Build();

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.InApp, Now, Now);
        notification.BeginDelivery(NotificationChannelKind.InApp, Now);
        notification.RecordFailure(
            NotificationChannelKind.InApp, Guid.NewGuid(), "Rejected", null, isPermanent: true, Now);

        notification.Status.Should().Be(NotificationStatus.Failed);
    }

    /// <summary>
    /// A permanent failure ends the ladder on the first attempt. Retrying a dead token seven more
    /// times costs the platform and helps nobody.
    /// </summary>
    [Fact]
    public void A_permanent_failure_does_not_retry()
    {
        var notification = NotificationLifecycleTests.Build(NotificationPriority.Critical);

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);
        notification.BeginDelivery(NotificationChannelKind.Push, Now);
        notification.RecordFailure(
            NotificationChannelKind.Push, Guid.NewGuid(), "Unregistered", null, isPermanent: true, Now);

        var delivery = notification.DeliveryFor(NotificationChannelKind.Push)!;

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.NextAttemptAt.Should().BeNull();
    }

    /// <summary>
    /// A transient failure schedules another attempt. The ladder length is the priority's, which is
    /// how a fraud alert gets eight tries and a photograph reaction gets one.
    /// </summary>
    [Fact]
    public void A_transient_failure_schedules_a_retry()
    {
        var notification = NotificationLifecycleTests.Build(NotificationPriority.High);

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);
        notification.BeginDelivery(NotificationChannelKind.Push, Now);
        notification.RecordFailure(
            NotificationChannelKind.Push, Guid.NewGuid(), "Timeout", null, isPermanent: false, Now);

        var delivery = notification.DeliveryFor(NotificationChannelKind.Push)!;

        delivery.Status.Should().Be(DeliveryStatus.Retrying);
        delivery.NextAttemptAt.Should().NotBeNull().And.BeAfter(Now);
    }

    [Theory]
    [InlineData(NotificationPriority.Critical, 8)]
    [InlineData(NotificationPriority.High, 5)]
    [InlineData(NotificationPriority.Normal, 3)]
    [InlineData(NotificationPriority.Low, 1)]
    [InlineData(NotificationPriority.Silent, 0)]
    public void The_attempt_budget_follows_the_priority(NotificationPriority priority, int expected)
    {
        NotificationPriorityPolicy.MaxAttempts(priority).Should().Be(expected);
    }

    /// <summary>
    /// A suppressed delivery is not retryable. Suppression was the member's own decision, and an
    /// operations console that could override it is precisely the console nobody should build.
    /// </summary>
    [Fact]
    public void A_suppressed_delivery_cannot_be_retried()
    {
        var notification = NotificationLifecycleTests.Build();

        notification.Suppress(Guid.NewGuid(), NotificationChannelKind.Push, "CategoryMuted", Now);

        notification.Requeue(NotificationChannelKind.Push, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_failed_delivery_can_be_requeued()
    {
        var notification = NotificationLifecycleTests.Build(NotificationPriority.Low);

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);
        notification.BeginDelivery(NotificationChannelKind.Push, Now);
        notification.RecordFailure(
            NotificationChannelKind.Push, Guid.NewGuid(), "Timeout", null, isPermanent: false, Now);

        notification.Requeue(NotificationChannelKind.Push, Now).IsSuccess.Should().BeTrue();
        notification.DeliveryFor(NotificationChannelKind.Push)!.Status
            .Should().Be(DeliveryStatus.Pending);
    }

    /// <summary>Every attempt is kept, so a failure timeline is explicable during an incident.</summary>
    [Fact]
    public void Every_attempt_is_recorded()
    {
        var notification = NotificationLifecycleTests.Build(NotificationPriority.High);

        notification.Queue(Guid.NewGuid(), NotificationChannelKind.Push, Now, Now);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            notification.BeginDelivery(NotificationChannelKind.Push, Now);
            notification.RecordFailure(
                NotificationChannelKind.Push, Guid.NewGuid(), "Timeout", null, false, Now);
        }

        notification.Attempts.Should().HaveCount(3);
        notification.Attempts.Should().OnlyContain(attempt => !attempt.Succeeded);
    }
}
