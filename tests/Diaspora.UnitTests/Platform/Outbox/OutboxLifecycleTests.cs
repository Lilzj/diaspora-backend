using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// A message from commit to completion, driven through the real dispatcher and the real store.
/// <para>
/// The tests above prove the pieces; these prove the sequence. A retry curve that is correct in
/// isolation and a claim that is correct in isolation can still combine into a message that is
/// retried once and then never seen again, and only running the whole loop finds that.
/// </para>
/// </summary>
public sealed class OutboxLifecycleTests
{
    [Fact]
    public async Task A_message_is_published_once_and_marked_delivered()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("published"));

        var result = await host.Dispatcher().DispatchOnceAsync();

        result.Published.Should().Be(1);
        host.Transport.Published.Should().ContainSingle();

        var messages = await host.MessagesAsync();

        messages.Should().ContainSingle();
        messages[0].State.Should().Be(OutboxState.Delivered);
        messages[0].DeliveredAt.Should().Be(host.Clock.UtcNow);

        // And a second pass finds nothing: delivered means delivered.
        (await host.Dispatcher().DispatchOnceAsync()).Claimed.Should().Be(0);
    }

    /// <summary>
    /// The full failure arc: retried on the curve, then given up on, with the payload intact and
    /// replayable at the end of it.
    /// </summary>
    [Fact]
    public async Task A_message_that_keeps_failing_is_retried_then_dead_lettered()
    {
        using var host = new OutboxTestHost(options =>
        {
            options.Retry.MaxAttempts = 3;
            options.Retry.ImmediateAttempts = 1;
        });

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("doomed"));

        host.Transport.Failures[ids[0]] = new TimeoutException("the consumer is down");

        // Attempt one, immediate retry scheduled.
        (await host.Dispatcher().DispatchOnceAsync()).Failed.Should().Be(1);

        // Attempt two, backing off.
        (await host.Dispatcher().DispatchOnceAsync()).Failed.Should().Be(1);

        host.Clock.Advance(TimeSpan.FromMinutes(5));

        // Attempt three exhausts the budget.
        (await host.Dispatcher().DispatchOnceAsync()).DeadLettered.Should().Be(1);

        (await host.MessagesAsync()).Should().BeEmpty();

        var deadLetters = await host.DeadLettersAsync();

        deadLetters.Should().ContainSingle();
        deadLetters[0].Reason.Should().Be(OutboxDeadLetterReason.MaxAttemptsExceeded);
        deadLetters[0].AttemptCount.Should().Be(3);
        deadLetters[0].Payload.Should().Contain("doomed");
    }

    /// <summary>
    /// The recovery story an operator actually runs: the dependency comes back, the dead letters
    /// go back on the queue, and they are delivered under their original identifiers so any
    /// consumer that already saw one can recognise it.
    /// </summary>
    [Fact]
    public async Task A_replayed_dead_letter_is_delivered_under_its_original_identifier()
    {
        using var host = new OutboxTestHost(options => options.Retry.MaxAttempts = 1);

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("recoverable"));

        host.Transport.Failures[ids[0]] = new TimeoutException("the consumer is down");

        (await host.Dispatcher().DispatchOnceAsync()).DeadLettered.Should().Be(1);

        var deadLetter = (await host.DeadLettersAsync()).Single();

        // The dependency comes back.
        host.Transport.Failures.Clear();
        host.Clock.Advance(TimeSpan.FromMinutes(30));

        using (var context = host.NewContext())
        {
            (await OutboxTestHost.Maintenance(context)
                .ReplayDeadLetterAsync(deadLetter.Id, host.Clock.UtcNow)).Should().BeTrue();
        }

        (await host.Dispatcher().DispatchOnceAsync()).Published.Should().Be(1);

        host.Transport.Published.Should().ContainSingle()
            .Which.MessageId.Should().Be(ids[0], "a replay is the same fact, not a new one");

        // The dead letter is stamped rather than deleted, so "we already tried that" is
        // answerable from the table.
        (await host.DeadLettersAsync())[0].ReplayedAt.Should().Be(host.Clock.UtcNow);
    }

    /// <summary>
    /// Replaying the same dead letter twice must not enqueue it twice. Running the bulk replay a
    /// second time because the first appeared not to work is exactly what an operator does.
    /// </summary>
    [Fact]
    public async Task A_bulk_replay_does_not_re_enqueue_what_it_already_replayed()
    {
        using var host = new OutboxTestHost(options => options.Retry.MaxAttempts = 1);

        var ids = await host.EnqueueAsync(
            OutboxMessageBuilder.NewEvent("first"),
            OutboxMessageBuilder.NewEvent("second"));

        foreach (var id in ids)
        {
            host.Transport.Failures[id] = new TimeoutException("down");
        }

        await host.Dispatcher().DispatchOnceAsync();

        (await host.DeadLettersAsync()).Should().HaveCount(2);

        using var context = host.NewContext();
        var maintenance = OutboxTestHost.Maintenance(context);

        (await maintenance.ReplayDeadLettersAsync(new OutboxQuery(Limit: 50), host.Clock.UtcNow))
            .Should().Be(2);

        (await maintenance.ReplayDeadLettersAsync(new OutboxQuery(Limit: 50), host.Clock.UtcNow))
            .Should().Be(0, "the first replay marked them, and a second run must be a no-op");

        (await host.MessagesAsync()).Should().HaveCount(2);
    }

    /// <summary>
    /// The operator's escape hatch for a message blocking its partition. It moves the message,
    /// never deletes it: a decision made in a hurry has to be reversible.
    /// </summary>
    [Fact]
    public async Task Skipping_a_message_moves_it_to_the_dead_letter_table_intact()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("blocking"));

        using var context = host.NewContext();
        var sequence = (await host.MessagesAsync())[0].Sequence;

        await OutboxTestHost.Store(context).DeadLetterAsync(
            sequence,
            OutboxDeadLetterReason.Skipped,
            "Skipped by an operator: the consumer will never accept this",
            host.Clock.UtcNow);

        (await host.MessagesAsync()).Should().BeEmpty();

        var deadLetter = (await host.DeadLettersAsync()).Single();

        deadLetter.Reason.Should().Be(OutboxDeadLetterReason.Skipped);
        deadLetter.Payload.Should().Contain("blocking");
    }

    /// <summary>
    /// A message a worker takes and never finishes must come back. Simulated the only honest
    /// way: claim it and then walk away, exactly as a killed process does.
    /// </summary>
    [Fact]
    public async Task A_message_abandoned_by_a_crashed_worker_is_delivered_by_another()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("abandoned"));

        using (var crashing = host.NewContext())
        {
            await OutboxTestHost.Store(crashing).ClaimAsync(new OutboxClaimRequest(
                ClaimId: Guid.NewGuid(),
                WorkerId: "worker-that-died",
                BatchSize: 10,
                Now: host.Clock.UtcNow,
                LeaseExpiresAt: host.Clock.UtcNow.AddSeconds(60),
                Ordering: host.Options.Dispatch.Ordering,
                TenantIds: []));
        }

        // Nothing else can take it while the lease holds.
        (await host.Dispatcher().DispatchOnceAsync()).Claimed.Should().Be(0);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        var survivor = host.CompetingDispatcher("worker-2");

        (await survivor.RecoverExpiredLeasesAsync()).Should().Be(1);
        (await survivor.DispatchOnceAsync()).Published.Should().Be(1);

        (await host.MessagesAsync())[0].State.Should().Be(OutboxState.Delivered);
    }

    /// <summary>
    /// Statistics are what a health check and every gauge read. The one that matters is the age
    /// of the oldest undelivered message: it is the only number that tells a deep queue from a
    /// stopped one.
    /// </summary>
    [Fact]
    public async Task Statistics_report_depth_age_and_the_per_module_breakdown()
    {
        using var host = new OutboxTestHost();

        await host.EnqueueAsync(
            OutboxMessageBuilder.NewEvent("one"),
            OutboxMessageBuilder.NewEvent("two"));

        host.Clock.Advance(TimeSpan.FromMinutes(3));

        using var context = host.NewContext();

        var statistics = await OutboxTestHost.Queries(context).GetStatisticsAsync(host.Clock.UtcNow);

        statistics.Pending.Should().Be(2);
        statistics.DeadLettered.Should().Be(0);
        statistics.OldestPendingAge.Should().Be(TimeSpan.FromMinutes(3));
        statistics.ByModule.Should().ContainSingle().Which.Pending.Should().Be(2);
        statistics.ByEventType.Should().ContainSingle().Which.Pending.Should().Be(2);
    }

    /// <summary>
    /// The attempt log, when it is switched on. Its value during an incident is answering "was
    /// this failing all afternoon or only since the deploy", which no counter can.
    /// </summary>
    [Fact]
    public async Task Delivery_attempts_are_recorded_with_their_outcome_and_worker()
    {
        using var host = new OutboxTestHost(options =>
        {
            options.Dispatch.RecordDeliveryAttempts = true;
            options.Retry.MaxAttempts = 5;
        });

        var ids = await host.EnqueueAsync(OutboxMessageBuilder.NewEvent("watched"));

        host.Transport.Failures[ids[0]] = new TimeoutException("down");

        await host.Dispatcher().DispatchOnceAsync();

        host.Transport.Failures.Clear();

        await host.Dispatcher().DispatchOnceAsync();

        var attempts = await host.AttemptsAsync();

        attempts.Should().HaveCount(2);
        attempts[0].Succeeded.Should().BeFalse();
        attempts[0].Error.Should().Contain("TimeoutException");
        attempts[1].Succeeded.Should().BeTrue();
        attempts.Should().OnlyContain(attempt => attempt.WorkerId == "test-worker/1");
    }
}
