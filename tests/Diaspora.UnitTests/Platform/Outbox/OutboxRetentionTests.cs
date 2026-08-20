using Diaspora.Platform.Messaging.Outbox.Model;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// What is collected, and — more importantly — what is not.
/// <para>
/// An outbox with no retention is a table that grows for the lifetime of the product, and it
/// grows fastest on the deployments that can least afford it. Nothing about the growth looks
/// wrong: every message was published, every consumer ran, every metric is green, and the first
/// symptom is the claim query getting slower some months later.
/// </para>
/// <para>
/// The tests that matter most here are the negative ones. A sweep that collects an undelivered
/// message, or a dead letter nobody has replayed yet, is the permanent data loss this whole
/// phase exists to prevent — arriving by a different door.
/// </para>
/// </summary>
public sealed class OutboxRetentionTests
{
    [Fact]
    public async Task Delivered_messages_past_the_window_are_collected()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("delivered"));

        await host.Dispatcher().DispatchOnceAsync();

        host.Clock.Advance(TimeSpan.FromHours(80));

        using var context = host.NewContext();

        var purged = await OutboxTestHost.Maintenance(context).PurgeDeliveredAsync(
            host.Clock.UtcNow.AddHours(-72), batchSize: 100);

        purged.Should().Be(1);
        (await host.MessagesAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// An undelivered message is never collected, however old it is. An outbox row that survived
    /// its own retention window is a delivery problem; deleting it would turn that into a lost
    /// business event.
    /// </summary>
    [Fact]
    public async Task An_undelivered_message_is_never_collected_however_old()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("stuck"));

        host.Clock.Advance(TimeSpan.FromDays(400));

        using var context = host.NewContext();

        var purged = await OutboxTestHost.Maintenance(context).PurgeDeliveredAsync(
            host.Clock.UtcNow, batchSize: 100);

        purged.Should().Be(0);
        (await host.MessagesAsync()).Should().ContainSingle();
    }

    /// <summary>
    /// The cutoff is when the message was delivered, not when it was written. A message that
    /// spent three days in retries was delivered a minute ago, and purging it because it was
    /// enqueued on Monday would erase the record of the incident it belongs to.
    /// </summary>
    [Fact]
    public async Task The_cutoff_is_the_delivery_time_not_the_enqueue_time()
    {
        using var host = new OutboxTestHost(options => options.Retry.MaxAttempts = 20);

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("slow"));

        host.Transport.Failures[ids[0]] = new TimeoutException("down for days");

        await host.Dispatcher().DispatchOnceAsync();

        // Three days of retries.
        host.Clock.Advance(TimeSpan.FromHours(73));
        host.Transport.Failures.Clear();

        await host.Dispatcher().DispatchOnceAsync();

        using var context = host.NewContext();

        // A sweep with a 72-hour window runs immediately afterwards. The message is 73 hours
        // old and was delivered a moment ago; it must survive.
        var purged = await OutboxTestHost.Maintenance(context).PurgeDeliveredAsync(
            host.Clock.UtcNow.AddHours(-72), batchSize: 100);

        purged.Should().Be(0);
        (await host.MessagesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Dead_letters_past_their_window_are_collected()
    {
        using var host = new OutboxTestHost(options => options.Retry.MaxAttempts = 1);

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("dead"));

        host.Transport.Failures[ids[0]] = new TimeoutException("down");

        await host.Dispatcher().DispatchOnceAsync();

        (await host.DeadLettersAsync()).Should().ContainSingle();

        host.Clock.Advance(TimeSpan.FromDays(40));

        using var context = host.NewContext();

        var purged = await OutboxTestHost.Maintenance(context).PurgeDeadLettersAsync(
            host.Clock.UtcNow.AddHours(-720), batchSize: 100);

        purged.Should().Be(1);
        (await host.DeadLettersAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A dead letter inside its window survives. It is unfinished business, and the retention
    /// window is the last moment anybody could have replayed it.
    /// </summary>
    [Fact]
    public async Task A_recent_dead_letter_survives_the_sweep()
    {
        using var host = new OutboxTestHost(options => options.Retry.MaxAttempts = 1);

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("recent"));

        host.Transport.Failures[ids[0]] = new TimeoutException("down");

        await host.Dispatcher().DispatchOnceAsync();

        host.Clock.Advance(TimeSpan.FromHours(2));

        using var context = host.NewContext();

        var purged = await OutboxTestHost.Maintenance(context).PurgeDeadLettersAsync(
            host.Clock.UtcNow.AddHours(-720), batchSize: 100);

        purged.Should().Be(0);
        (await host.DeadLettersAsync()).Should().ContainSingle();
    }

    /// <summary>
    /// The purge is bounded, so a first sweep against a table that accumulated for a month takes
    /// a series of short locks rather than one long one on the busiest table in the platform.
    /// </summary>
    [Fact]
    public async Task A_purge_takes_a_bounded_bite()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(
            Enumerable.Range(0, 25)
                .Select(index => OutboxMessageBuilder.NewEvent($"event-{index}"))
                .Cast<Diaspora.Contracts.Events.IIntegrationEvent>()
                .ToArray());

        await host.Dispatcher().DispatchOnceAsync();

        host.Clock.Advance(TimeSpan.FromHours(80));

        using var context = host.NewContext();
        var maintenance = OutboxTestHost.Maintenance(context);
        var cutoff = host.Clock.UtcNow.AddHours(-72);

        (await maintenance.PurgeDeliveredAsync(cutoff, batchSize: 10)).Should().Be(10);
        (await maintenance.PurgeDeliveredAsync(cutoff, batchSize: 10)).Should().Be(10);
        (await maintenance.PurgeDeliveredAsync(cutoff, batchSize: 10)).Should().Be(5);
        (await maintenance.PurgeDeliveredAsync(cutoff, batchSize: 10)).Should().Be(0);
    }

    [Fact]
    public async Task Attempt_records_past_their_window_are_collected()
    {
        using var host = new OutboxTestHost(options => options.Dispatch.RecordDeliveryAttempts = true);

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("watched"));

        await host.Dispatcher().DispatchOnceAsync();

        (await host.AttemptsAsync()).Should().ContainSingle();

        host.Clock.Advance(TimeSpan.FromDays(10));

        using var context = host.NewContext();

        (await OutboxTestHost.Maintenance(context).PurgeAttemptsAsync(
            host.Clock.UtcNow.AddHours(-168), batchSize: 100)).Should().Be(1);

        (await host.AttemptsAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// Purging attempts must not touch the message they describe. The two tables have different
    /// lifetimes on purpose, which is exactly why there is no foreign key between them.
    /// </summary>
    [Fact]
    public async Task Purging_attempts_leaves_the_messages_alone()
    {
        using var host = new OutboxTestHost(options => options.Dispatch.RecordDeliveryAttempts = true);

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("watched"));

        await host.Dispatcher().DispatchOnceAsync();

        host.Clock.Advance(TimeSpan.FromDays(10));

        using var context = host.NewContext();

        await OutboxTestHost.Maintenance(context).PurgeAttemptsAsync(
            host.Clock.UtcNow, batchSize: 100);

        (await host.MessagesAsync()).Should().ContainSingle()
            .Which.State.Should().Be(OutboxState.Delivered);
    }
}
