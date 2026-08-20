using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Dispatch;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Retry;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Messaging.Outbox.Tenancy;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// What the dispatcher does with a batch, and — mostly — what it does when things go wrong.
/// <para>
/// The invariant every test here circles is that a claimed message always ends the pass in a
/// state some future pass can act on: delivered, rescheduled, dead-lettered or released. Never
/// left leased by a worker that has moved on, and never dropped. That is why the class under
/// test has more error handling than logic, and why most of these tests are about failures.
/// </para>
/// </summary>
public sealed class OutboxDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly IOutboxQueries _queries = Substitute.For<IOutboxQueries>();
    private readonly RecordingOutboxTransport _transport = new();
    private readonly FakeClock _clock = new(Now);

    private OutboxDispatcher Dispatcher(Action<OutboxOptions>? configure = null)
    {
        var options = new OutboxOptions();
        options.Retry.BackoffJitter = 0;
        configure?.Invoke(options);

        var wrapped = Options.Create(options);

        return new OutboxDispatcher(
            _store,
            _queries,
            new JsonOutboxSerializer(
                new IntegrationEventTypeResolver([typeof(WidgetChangedIntegrationEvent).Assembly])),
            _transport,
            new ExponentialBackoffRetryPolicy(wrapped),
            new SingleTenantOutboxTenantResolver(),
            new FakeOutboxWorkerIdentity(),
            new OutboxStatisticsSnapshot(),
            _clock,
            wrapped,
            NullLogger<OutboxDispatcher>.Instance);
    }

    private void Claims(params OutboxMessage[] messages) =>
        _store.ClaimAsync(Arg.Any<OutboxClaimRequest>(), Arg.Any<CancellationToken>())
            .Returns(messages);

    [Fact]
    public async Task An_empty_queue_costs_one_claim_and_nothing_else()
    {
        Claims();

        var result = await Dispatcher().DispatchOnceAsync();

        result.Should().Be(OutboxDispatchResult.Empty);
        _transport.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task A_claimed_message_is_published_and_marked_delivered()
    {
        var message = new OutboxMessageBuilder().WithSequence(7).Build();

        Claims(message);

        var result = await Dispatcher().DispatchOnceAsync();

        result.Published.Should().Be(1);
        _transport.Published.Should().ContainSingle()
            .Which.MessageId.Should().Be(message.MessageId);

        await _store.Received(1).MarkDeliveredAsync(7, Now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The transport is handed the producing request's correlation identifier and the message's
    /// own identifier as causation, so a consumer's log line minutes later still joins the trail
    /// of the request that caused it.
    /// </summary>
    [Fact]
    public async Task The_transport_receives_the_delivery_context()
    {
        var message = new OutboxMessageBuilder().WithSequence(3).InPartition("WidgetId:a").Build();

        Claims(message);

        await Dispatcher().DispatchOnceAsync();

        var context = _transport.Published.Single();

        context.CorrelationId.Should().Be("corr-test");
        context.PartitionKey.Should().Be("WidgetId:a");
        context.AttemptNumber.Should().Be(1);
        context.Sequence.Should().Be(3);
    }

    [Fact]
    public async Task A_failed_publish_is_rescheduled_rather_than_dropped()
    {
        var message = new OutboxMessageBuilder().WithSequence(11).Build();

        _transport.Failures[message.MessageId] = new TimeoutException("the consumer was slow");

        Claims(message);

        var result = await Dispatcher().DispatchOnceAsync();

        result.Failed.Should().Be(1);

        // The first retry is immediate by default: most publish failures in a modular monolith
        // are a transient blip that is over before a backoff would have started.
        await _store.Received(1).MarkFailedAsync(
            11, Now, Now, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_second_failure_backs_off_before_the_next_attempt()
    {
        var message = new OutboxMessageBuilder().WithSequence(11).WithAttempts(1).Build();

        _transport.Failures[message.MessageId] = new TimeoutException("still slow");

        Claims(message);

        await Dispatcher().DispatchOnceAsync();

        await _store.Received(1).MarkFailedAsync(
            11, Now, Arg.Is<DateTimeOffset>(next => next > Now), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_message_out_of_retries_is_dead_lettered()
    {
        var message = new OutboxMessageBuilder().WithSequence(12).WithAttempts(7).Build();

        _transport.Failures[message.MessageId] = new TimeoutException("still failing");

        Claims(message);

        var result = await Dispatcher(options => options.Retry.MaxAttempts = 8).DispatchOnceAsync();

        result.DeadLettered.Should().Be(1);

        await _store.Received(1).DeadLetterAsync(
            12, OutboxDeadLetterReason.MaxAttemptsExceeded, Arg.Any<string>(), Now,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A payload naming a contract this deployment does not have is what a rolled-back release
    /// looks like from the outbox's side. It must dead-letter immediately with a reason that
    /// says so, rather than spending forty minutes proving the assembly is still missing.
    /// </summary>
    [Fact]
    public async Task An_unresolvable_contract_dead_letters_immediately_with_its_own_reason()
    {
        var message = new OutboxMessageBuilder().WithSequence(13).BuildWithUnknownContract();

        Claims(message);

        var result = await Dispatcher().DispatchOnceAsync();

        result.DeadLettered.Should().Be(1);

        await _store.Received(1).DeadLetterAsync(
            13, OutboxDeadLetterReason.UnknownEventType, Arg.Any<string>(), Now,
            Arg.Any<CancellationToken>());

        _transport.Published.Should().BeEmpty();
    }

    /// <summary>
    /// The head-of-line rule. Delivering the second and third events about a provider after the
    /// first has failed is "in order" only in the most useless sense — the created event would
    /// be missing and the consumer would act on an update to something it has never heard of.
    /// </summary>
    [Fact]
    public async Task A_failure_stops_its_own_partition_and_releases_what_is_behind_it()
    {
        var first = new OutboxMessageBuilder().WithSequence(1).InPartition("WidgetId:a").Build();
        var second = new OutboxMessageBuilder().WithSequence(2).InPartition("WidgetId:a").Build();
        var third = new OutboxMessageBuilder().WithSequence(3).InPartition("WidgetId:a").Build();

        _transport.Failures[first.MessageId] = new TimeoutException("the consumer refused");

        Claims(first, second, third);

        var result = await Dispatcher().DispatchOnceAsync();

        result.Published.Should().Be(0);
        result.Failed.Should().Be(1);
        result.Deferred.Should().Be(2);

        _transport.Published.Should().BeEmpty();

        // Released, not charged an attempt against a failure that was not theirs.
        await _store.Received(1).MarkFailedAsync(
            2, Now, Now, Arg.Is<string>(error => error.StartsWith("Deferred")),
            Arg.Any<CancellationToken>());
        await _store.Received(1).MarkFailedAsync(
            3, Now, Now, Arg.Is<string>(error => error.StartsWith("Deferred")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// And the other half of the guarantee: a wedged consumer costs its own aggregate's latency
    /// and nobody else's.
    /// </summary>
    [Fact]
    public async Task A_failure_in_one_partition_does_not_hold_up_another()
    {
        var blocked = new OutboxMessageBuilder().WithSequence(1).InPartition("WidgetId:a").Build();
        var behindIt = new OutboxMessageBuilder().WithSequence(2).InPartition("WidgetId:a").Build();
        var unrelated = new OutboxMessageBuilder().WithSequence(3).InPartition("WidgetId:b").Build();

        _transport.Failures[blocked.MessageId] = new TimeoutException("refused");

        Claims(blocked, behindIt, unrelated);

        var result = await Dispatcher().DispatchOnceAsync();

        result.Published.Should().Be(1);
        _transport.PublishedSequences.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public async Task Messages_in_a_partition_are_published_in_write_order()
    {
        var messages = Enumerable.Range(1, 5)
            .Select(sequence => new OutboxMessageBuilder()
                .WithSequence(sequence)
                .InPartition("WidgetId:a")
                .Build())
            .ToArray();

        // Handed back out of order, as a database is entitled to do without an ORDER BY.
        Claims([.. messages.Reverse()]);

        await Dispatcher().DispatchOnceAsync();

        _transport.PublishedSequences.Should().Equal(1, 2, 3, 4, 5);
    }

    /// <summary>
    /// A transport that hangs would otherwise hold its lease — and every message behind it in
    /// the partition — until the process stopped.
    /// </summary>
    [Fact]
    public async Task A_publish_that_outlives_its_timeout_is_treated_as_a_failure()
    {
        var message = new OutboxMessageBuilder().WithSequence(21).Build();

        // Blocks until cancelled rather than racing a five-second delay against the one-second
        // timeout below. The only thing that can complete this publish is the dispatcher's own
        // timeout firing, which is exactly what the test is about — see BlockUntilCancelled for
        // why the wall-clock version was flaky (DEBT-058, closed in PSR-1 Final Certification).
        _transport.BlockUntilCancelled = true;

        Claims(message);

        var result = await Dispatcher(options =>
        {
            options.Dispatch.PublishTimeoutSeconds = 1;
            options.Dispatch.LeaseSeconds = 30;
        }).DispatchOnceAsync();

        result.Failed.Should().Be(1);

        await _store.Received(1).MarkFailedAsync(
            21, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Is<string>(error => error.Contains("TimeoutException")),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The database being unreachable is the one failure the dispatcher cannot record. The
    /// message stays leased and the lease expiry recovers it — which is why leases exist and
    /// are not optional.
    /// </summary>
    [Fact]
    public async Task A_bookkeeping_failure_does_not_take_down_the_pass()
    {
        var failing = new OutboxMessageBuilder().WithSequence(1).InPartition("WidgetId:a").Build();
        var healthy = new OutboxMessageBuilder().WithSequence(2).InPartition("WidgetId:b").Build();

        _transport.Failures[failing.MessageId] = new TimeoutException("refused");

        _store.MarkFailedAsync(
                1, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("the database is gone")));

        Claims(failing, healthy);

        var act = async () => await Dispatcher().DispatchOnceAsync();

        await act.Should().NotThrowAsync();

        _transport.PublishedSequences.Should().Contain(2);
    }

    [Fact]
    public async Task Recovering_leases_reports_how_many_were_taken_back()
    {
        _store.RecoverExpiredLeasesAsync(Now, Arg.Any<CancellationToken>()).Returns(4);

        (await Dispatcher().RecoverExpiredLeasesAsync()).Should().Be(4);
    }

    /// <summary>
    /// Delivery attempts are recorded only when asked for. The table grows at attempt rate, and
    /// a retry storm writes into it faster than anything else in the platform writes anywhere.
    /// </summary>
    [Fact]
    public async Task Attempts_are_recorded_only_when_recording_is_switched_on()
    {
        Claims(new OutboxMessageBuilder().WithSequence(1).Build());

        await Dispatcher(options => options.Dispatch.RecordDeliveryAttempts = false)
            .DispatchOnceAsync();

        await _store.DidNotReceive().RecordAttemptAsync(
            Arg.Any<OutboxDeliveryAttempt>(), Arg.Any<CancellationToken>());

        Claims(new OutboxMessageBuilder().WithSequence(2).Build());

        await Dispatcher(options => options.Dispatch.RecordDeliveryAttempts = true)
            .DispatchOnceAsync();

        await _store.Received(1).RecordAttemptAsync(
            Arg.Any<OutboxDeliveryAttempt>(), Arg.Any<CancellationToken>());
    }
}
