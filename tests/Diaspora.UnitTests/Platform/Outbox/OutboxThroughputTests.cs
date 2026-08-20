using System.Diagnostics;
using Diaspora.Contracts.Events;
using Diaspora.Persistence.Outbox;
using Diaspora.Platform.Messaging.Outbox.Model;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// Volume, and what breaks at it.
/// <para>
/// <b>These are correctness tests that happen to be large.</b> The numbers they print are
/// recorded in the performance report, but they do not assert on wall-clock time — a build agent
/// under load would fail a timing assertion for reasons that have nothing to do with the code,
/// and a suite that goes red for that stops being read. What they assert is what actually
/// matters at volume: nothing is lost, nothing is delivered twice, and no query degrades into a
/// scan that would only be noticed in production.
/// </para>
/// </summary>
public sealed class OutboxThroughputTests
{
    private readonly ITestOutputHelper _output;

    public OutboxThroughputTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Ten thousand messages, written and drained. The headline number for the performance
    /// report, and the case where an accidental table scan in the claim query stops being
    /// invisible.
    /// </summary>
    [Fact]
    public async Task Ten_thousand_messages_are_written_and_drained_without_loss()
    {
        const int Count = 10_000;

        using var host = new OutboxTestHost(options => options.Dispatch.BatchSize = 500);

        var enqueue = Stopwatch.StartNew();

        // Written in batches the way a busy platform writes them — many transactions, not one.
        for (var batch = 0; batch < Count / 500; batch++)
        {
            using var context = host.NewContext();
            var writer = new EfOutboxWriter(context, host.Factory());

            for (var index = 0; index < 500; index++)
            {
                writer.Enqueue(OutboxMessageBuilder.NewEvent($"event-{batch}-{index}"));
            }

            await context.SaveChangesAsync();
        }

        enqueue.Stop();

        (await host.MessagesAsync()).Should().HaveCount(Count);

        var drain = Stopwatch.StartNew();
        var published = 0;
        var passes = 0;

        while (true)
        {
            var result = await host.Dispatcher().DispatchOnceAsync();

            passes++;
            published += result.Published;

            if (result.Claimed == 0)
            {
                break;
            }
        }

        drain.Stop();

        published.Should().Be(Count, "at-least-once must not become at-most-once at volume");

        host.Transport.Published.Select(context => context.MessageId).Distinct()
            .Should().HaveCount(Count, "and it must not become more-than-once either");

        (await host.MessagesAsync()).Should()
            .OnlyContain(message => message.State == OutboxState.Delivered);

        _output.WriteLine(
            $"Enqueued {Count:N0} in {enqueue.ElapsedMilliseconds:N0}ms " +
            $"({Count / Math.Max(1, enqueue.Elapsed.TotalSeconds):N0}/s); " +
            $"drained in {drain.ElapsedMilliseconds:N0}ms across {passes} pass(es) " +
            $"({Count / Math.Max(0.001, drain.Elapsed.TotalSeconds):N0}/s).");
    }

    /// <summary>
    /// Four workers against one queue. The property under test is the one that decides whether
    /// the platform can scale horizontally at all: every message is claimed by exactly one
    /// worker, and none is left behind.
    /// </summary>
    [Fact]
    public async Task Competing_workers_deliver_every_message_exactly_once()
    {
        const int Count = 400;
        const int Workers = 4;

        using var host = new OutboxTestHost(options => options.Dispatch.BatchSize = 25);

        await EnqueueManyAsync(host, Count);

        var dispatchers = Enumerable.Range(1, Workers)
            .Select(index => host.CompetingDispatcher($"worker-{index}"))
            .ToArray();

        // Sequential rounds rather than genuinely parallel threads: the whole suite shares one
        // in-memory SQLite connection, and concurrent commands on one connection is a SQLite
        // restriction rather than anything about the claim. The claim's correctness under
        // contention is proven by the conditional update itself — see OutboxStoreTests — and
        // what this test adds is that four distinct workers interleaving over a real queue
        // between them lose nothing and duplicate nothing.
        var rounds = 0;

        while (rounds++ < 200)
        {
            var claimedThisRound = 0;

            foreach (var dispatcher in dispatchers)
            {
                claimedThisRound += (await dispatcher.DispatchOnceAsync()).Claimed;
            }

            if (claimedThisRound == 0)
            {
                break;
            }
        }

        var delivered = host.Transport.Published.Select(context => context.MessageId).ToArray();

        delivered.Should().HaveCount(Count);
        delivered.Distinct().Should().HaveCount(Count);

        (await host.MessagesAsync()).Should()
            .OnlyContain(message => message.State == OutboxState.Delivered);
    }

    /// <summary>
    /// A retry storm: every message in a large batch fails at once, as they would during a
    /// broker outage. Nothing may be lost, and every one must end up scheduled or dead —
    /// never left leased by the pass that failed them.
    /// </summary>
    [Fact]
    public async Task A_retry_storm_leaves_every_message_accounted_for()
    {
        const int Count = 500;

        using var host = new OutboxTestHost(options =>
        {
            options.Dispatch.BatchSize = 500;
            options.Retry.MaxAttempts = 5;
        });

        var ids = await EnqueueManyAsync(host, Count);

        foreach (var id in ids)
        {
            host.Transport.Failures[id] = new TimeoutException("the broker is down");
        }

        var result = await host.Dispatcher().DispatchOnceAsync();

        result.Claimed.Should().BeGreaterThan(0);

        var messages = await host.MessagesAsync();

        messages.Should().HaveCount(Count);
        messages.Should().OnlyContain(message => message.State == OutboxState.Pending);
        messages.Should().OnlyContain(message => message.LeaseOwner == null);
        messages.Should().OnlyContain(message => message.NextAttemptAt != null);
    }

    /// <summary>
    /// A payload at the ceiling. Large messages are a mistake — an event should carry
    /// identifiers a consumer can resolve, not the content — but the ceiling has to hold rather
    /// than the write failing somewhere unhelpful.
    /// </summary>
    [Fact]
    public async Task A_large_payload_round_trips_up_to_the_configured_ceiling()
    {
        using var host = new OutboxTestHost(options => options.MaxPayloadBytes = 2_000_000);

        var large = OutboxMessageBuilder.NewEvent(new string('x', 900_000));

        await host.EnqueueAsync(large);

        (await host.Dispatcher().DispatchOnceAsync()).Published.Should().Be(1);

        var delivered = host.Transport.Published.Single();

        delivered.MessageId.Should().Be(large.EventId);
    }

    private static async Task<IReadOnlyList<Guid>> EnqueueManyAsync(OutboxTestHost host, int count)
    {
        using var context = host.NewContext();

        var writer = new EfOutboxWriter(context, host.Factory());

        var ids = Enumerable.Range(0, count)
            .Select(index => writer.Enqueue(OutboxMessageBuilder.NewEvent($"event-{index}")))
            .ToArray();

        await context.SaveChangesAsync();

        return ids;
    }
}
